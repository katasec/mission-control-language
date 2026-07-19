using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using ForgeMission.Billing;
using ForgeMission.Runner.Contracts;
using Microsoft.Extensions.Logging;

namespace ForgeMission.Api;

/// <summary>
/// API A's core operation (42.6 task 5a) — <c>ExecuteMission</c>: resolve the handle via
/// <see cref="IMissionCatalog"/>, run it on the internal runner, settle billing, and shape the
/// result. Signature intentionally takes no <c>HttpContext</c> (M5) — <see cref="MissionEndpoints"/>
/// is the HTTP adapter that resolves the principal and passes it in.
///
/// <para>Buffered (<see cref="ExecuteAsync"/>) and streaming (<see cref="ExecuteStreamAsync"/>) share
/// one code path (<see cref="RunCoreAsync"/>); streaming forwards the runner's per-step progress as
/// it happens via a channel + background task, mirroring the runner's own
/// <c>MissionRunHandler.RunStreamAsync</c> pattern (channel reader loop has no try/catch around
/// <c>yield return</c> — C# forbids that; the background writer catches instead).</para>
/// </summary>
public sealed class MissionExecutionService(
    IMissionCatalog catalog,
    IRunStore runStore,
    IHttpClientFactory httpClientFactory,
    BillingService billing,
    ILogger<MissionExecutionService> logger)
{
    public Task<ExecuteMissionResponse> ExecuteAsync(
        ExecuteMission msg, PlatformKeyContext principal, CancellationToken ct) =>
        RunCoreAsync(msg, principal, onProgress: null, NewRunId(), ct);

    public async IAsyncEnumerable<MissionRunEvent> ExecuteStreamAsync(
        ExecuteMission msg, PlatformKeyContext principal, [EnumeratorCancellation] CancellationToken ct)
    {
        var runId = NewRunId();
        var channel = Channel.CreateUnbounded<MissionRunEvent>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        _ = Task.Run(() => DrainIntoChannelAsync(msg, principal, runId, channel.Writer, ct), ct);

        var reader = channel.Reader;
        while (await reader.WaitToReadAsync(ct))
            while (reader.TryRead(out var evt))
                yield return evt;
    }

    private async Task DrainIntoChannelAsync(
        ExecuteMission msg, PlatformKeyContext principal, string runId,
        ChannelWriter<MissionRunEvent> writer, CancellationToken ct)
    {
        try
        {
            var response = await RunCoreAsync(
                msg, principal,
                onProgress: p => writer.TryWrite(new MissionRunEvent
                {
                    Type = "progress", RunId = runId, Progress = MapProgress(p),
                }),
                runId, ct);

            writer.TryWrite(new MissionRunEvent
            {
                Type = response.ResponseStatus.ErrorCode is { Length: > 0 } ? "error" : "result",
                RunId = runId,
                Result = response,
                ResponseStatus = response.ResponseStatus,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ExecuteMission stream failed for mission '{Mission}'", msg.Mission);
            writer.TryWrite(new MissionRunEvent
            {
                Type = "error", RunId = runId,
                ResponseStatus = ResponseStatus.Fail(ErrorCode.RunFailed, "The mission run failed."),
            });
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task<ExecuteMissionResponse> RunCoreAsync(
        ExecuteMission msg, PlatformKeyContext principal, Action<RunProgress>? onProgress,
        string runId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(msg.Mission))
            return Fail(runId, ErrorCode.InvalidInput, "Mission is required.");
        if (string.IsNullOrWhiteSpace(msg.Input))
            return Fail(runId, ErrorCode.InvalidInput, "Input is required.");

        var handle = MissionHandle.Parse(msg.Mission);
        var entry = await catalog.ResolveAsync(handle, msg.MissionVersion, ct);
        if (entry is null)
            return Fail(runId, ErrorCode.MissionNotFound, $"Mission '{msg.Mission}' was not found.");

        if (!await billing.HasCreditAsync(principal.MemberId, ct))
            return Fail(runId, ErrorCode.InsufficientCredit, "Insufficient credit — top up to keep running missions.");

        // M9: the server owns missionRef + policy, never the client. Built-in catalog entries always
        // run trusted (same precedent RoomAgentInvoker sets); a locked-down policy for custom/user
        // missions is Phase 39.5 scope, not 5a's.
        var request = new RunRequest(entry.MissionRef, msg.Input, Vars: null, RunPolicy.Trusted);

        var runner = httpClientFactory.CreateClient("runner");
        RunResponse result;
        try
        {
            result = await RunOnRunnerAsync(runner, request, onProgress, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Run failed for mission '{MissionRef}'", entry.MissionRef);
            return Fail(runId, ErrorCode.RunFailed, "The mission run failed.");
        }

        // M7: idempotent against msg.ClientToken — a retry of the same call returns the prior debit.
        var cost = await billing.SettleRunAsync(
            principal.MemberId, entry.MissionRef, result.Usage, ct, msg.ClientToken);
        var balance = await billing.GetBalanceMicroUsdAsync(principal.MemberId, ct);

        var response = new ExecuteMissionResponse
        {
            RunId = runId,
            Mission = entry.Handle,
            MissionVersion = entry.Version,
            Answer = result.AgentText,
            Verified = result.Verified,
            // Known gap (see phase-42.6 spoke): the runner contract carries no structured citations
            // yet — Sources stays empty until that lands. Additive (M4), so it can land post-demo.
            Sources = [],
            Trace = result.Trace.Select(t => new MissionTraceStep
            {
                Expert = t.ExpertName,
                Status = t.Status,
                Text = t.Text,
                Reason = t.Reason,
                Attempt = t.Attempt,
            }).ToList(),
            Usage = new MissionUsage
            {
                InputTokens = result.Usage.InputTokens,
                OutputTokens = result.Usage.OutputTokens,
                ComputeSeconds = result.Usage.ComputeSeconds,
                Model = result.Usage.Model,
                CostMicroUsd = cost,
            },
            BalanceMicroUsd = balance,
            ResponseStatus = ResponseStatus.Ok(),
        };

        await runStore.SaveAsync(runId, response, ct);
        return response;
    }

    private static ExecuteMissionResponse Fail(string runId, string errorCode, string message) => new()
    {
        RunId = runId,
        ResponseStatus = ResponseStatus.Fail(errorCode, message),
    };

    private static string NewRunId() => Guid.NewGuid().ToString("N");

    private static MissionProgress MapProgress(RunProgress p) => new()
    {
        ExpertName = p.ExpertName,
        Kind = p.Kind,
        Detail = p.Detail,
        ResultCount = p.ResultCount,
    };

    /// <summary>Consume the runner's NDJSON <c>/run/stream</c> — same client-side pattern as
    /// ForgeUI's <c>MissionRunnerClient.RunStreamAsync</c> (a different project; not shared, since
    /// ForgeAPI must not depend on ForgeUI).</summary>
    private static async Task<RunResponse> RunOnRunnerAsync(
        HttpClient runner, RunRequest request, Action<RunProgress>? onProgress, CancellationToken ct)
    {
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, "/run/stream")
        {
            Content = JsonContent.Create(request, RunContractsContext.Default.RunRequest),
        };

        using var response = await runner.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        RunResponse? result = null;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0) continue;
            var evt = JsonSerializer.Deserialize(line, RunContractsContext.Default.RunStreamEvent);
            switch (evt?.Type)
            {
                case "progress" when evt.Progress is not null:
                    onProgress?.Invoke(evt.Progress);
                    break;
                case "result":
                    result = evt.Result;
                    break;
                case "error":
                    throw new InvalidOperationException(evt.Error ?? "The runner reported an error.");
                case "heartbeat":
                    break; // keep-alive only — nothing to relay to a buffered caller
            }
        }

        return result ?? throw new InvalidOperationException("Runner stream ended without a result.");
    }
}
