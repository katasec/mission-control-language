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
    IArtifactStore artifacts,
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
        if (string.IsNullOrWhiteSpace(msg.Input) && msg.InputArtifactIds is not { Count: > 0 })
            return Fail(runId, ErrorCode.InvalidInput, "Input or InputArtifactIds is required.");

        var handle = MissionHandle.Parse(msg.Mission);
        var entry = await catalog.ResolveAsync(handle, msg.MissionVersion, ct);
        if (entry is null)
            return Fail(runId, ErrorCode.MissionNotFound, $"Mission '{msg.Mission}' was not found.");

        if (!await billing.HasCreditAsync(principal.MemberId, ct))
            return Fail(runId, ErrorCode.InsufficientCredit, "Insufficient credit — top up to keep running missions.");

        var runner = httpClientFactory.CreateClient("runner");
        IReadOnlyList<RunArtifact>? inputArtifacts;
        try
        {
            inputArtifacts = await CopyInputsToRunnerAsync(
                msg.InputArtifactIds,
                principal,
                entry,
                runner,
                ct);
        }
        catch (ArtifactException ex)
        {
            return Fail(runId, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Input artifact staging failed for mission '{Mission}'", msg.Mission);
            return Fail(runId, ErrorCode.RunFailed, "The mission run failed.");
        }

        // M9: the server owns missionRef + policy, never the client. Built-in catalog entries always
        // run trusted (same precedent RoomAgentInvoker sets); a locked-down policy for custom/user
        // missions is Phase 39.5 scope, not 5a's.
        var request = new RunRequest(
            entry.MissionRef,
            msg.Input,
            Vars: msg.Inputs,
            RunPolicy.Trusted,
            InputArtifacts: inputArtifacts);

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

        List<MissionArtifact> outputArtifacts;
        try
        {
            outputArtifacts = await CopyOutputsFromRunnerAsync(
                result.OutputArtifacts,
                principal,
                runner,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Output artifact copy failed for mission '{MissionRef}'", entry.MissionRef);
            return Fail(
                runId,
                ErrorCode.RunFailed,
                "The mission run failed.",
                MapUsage(result.Usage, cost),
                balance);
        }

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
            Artifacts = outputArtifacts,
            Trace = result.Trace.Select(t => new MissionTraceStep
            {
                Expert = t.ExpertName,
                Status = t.Status,
                Text = t.Text,
                Reason = t.Reason,
                Attempt = t.Attempt,
            }).ToList(),
            Usage = MapUsage(result.Usage, cost),
            BalanceMicroUsd = balance,
            ResponseStatus = ResponseStatus.Ok(),
        };

        await runStore.SaveAsync(runId, response, ct);
        return response;
    }

    private static ExecuteMissionResponse Fail(
        string runId,
        string errorCode,
        string message,
        MissionUsage? usage = null,
        long balanceMicroUsd = 0) => new()
    {
        RunId = runId,
        Usage = usage ?? new MissionUsage(),
        BalanceMicroUsd = balanceMicroUsd,
        ResponseStatus = ResponseStatus.Fail(errorCode, message),
    };

    private static MissionUsage MapUsage(RunUsage usage, long costMicroUsd) => new()
    {
        InputTokens = usage.InputTokens,
        OutputTokens = usage.OutputTokens,
        ComputeSeconds = usage.ComputeSeconds,
        Model = usage.Model,
        CostMicroUsd = costMicroUsd,
    };

    private static string NewRunId() => Guid.NewGuid().ToString("N");

    private static MissionProgress MapProgress(RunProgress p) => new()
    {
        ExpertName = p.ExpertName,
        Kind = p.Kind,
        Detail = p.Detail,
        ResultCount = p.ResultCount,
    };

    private async Task<IReadOnlyList<RunArtifact>?> CopyInputsToRunnerAsync(
        List<string>? artifactIds,
        PlatformKeyContext principal,
        CatalogEntry entry,
        HttpClient runner,
        CancellationToken ct)
    {
        if (artifactIds is not { Count: > 0 }) return null;

        var inputs = new List<ArtifactRead>();
        try
        {
            foreach (var artifactId in artifactIds)
            {
                var input = await artifacts.OpenAsync(artifactId, principal, ct)
                    ?? throw new ArtifactException(ErrorCode.ArtifactNotFound, $"Artifact '{artifactId}' was not found.");
                inputs.Add(input);
            }

            ValidateInputArtifactCapabilities(inputs.Select(i => i.Artifact), entry.ArtifactCapabilities);

            var copied = new List<RunArtifact>();
            foreach (var input in inputs)
                copied.Add(await UploadToRunnerAsync(runner, input.Artifact, input.Content, ct));

            return copied;
        }
        finally
        {
            foreach (var input in inputs)
                await input.DisposeAsync();
        }
    }

    private static void ValidateInputArtifactCapabilities(
        IEnumerable<MissionArtifact> inputArtifacts,
        MissionArtifactCapabilities? capabilities)
    {
        if (capabilities?.Inputs is not { Count: > 0 } inputs) return;

        foreach (var artifact in inputArtifacts)
        {
            var match = inputs.FirstOrDefault(i => IsAllowed(artifact, i));
            if (match is not null) continue;

            var allowedTypes = string.Join(", ", inputs.SelectMany(i => i.ContentTypes).Distinct(StringComparer.OrdinalIgnoreCase));
            var maxSizeMb = inputs.Max(i => i.MaxSizeMb);
            throw new ArtifactException(
                ErrorCode.InvalidInput,
                $"Artifact '{artifact.Name}' is not allowed for this mission. " +
                $"Allowed content types: {allowedTypes}; max size: {maxSizeMb} MB.");
        }
    }

    private static bool IsAllowed(MissionArtifact artifact, MissionArtifactInputCapability input)
    {
        var contentTypeOk = input.ContentTypes.Any(t =>
            string.Equals(t, artifact.ContentType, StringComparison.OrdinalIgnoreCase));
        var maxBytes = input.MaxSizeMb * 1024L * 1024L;
        return contentTypeOk && artifact.Size <= maxBytes;
    }

    private static async Task<RunArtifact> UploadToRunnerAsync(
        HttpClient runner,
        MissionArtifact artifact,
        Stream content,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/artifacts/upload")
        {
            Content = new StreamContent(content),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(artifact.ContentType);
        request.Content.Headers.ContentLength = artifact.Size;
        request.Headers.Add("X-Forge-Artifact-Name", artifact.Name);
        request.Headers.Add("X-Forge-Artifact-Sha256", artifact.Sha256);
        request.Headers.Add("X-Forge-Artifact-Role", artifact.Role);

        using var response = await runner.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var uploaded = await response.Content.ReadFromJsonAsync(
            RunContractsContext.Default.RunArtifact,
            ct);
        return uploaded ?? throw new InvalidOperationException("Runner artifact upload returned no metadata.");
    }

    private async Task<List<MissionArtifact>> CopyOutputsFromRunnerAsync(
        IReadOnlyList<RunArtifact>? outputArtifacts,
        PlatformKeyContext principal,
        HttpClient runner,
        CancellationToken ct)
    {
        var copied = new List<MissionArtifact>();
        if (outputArtifacts is not { Count: > 0 }) return copied;

        foreach (var output in outputArtifacts)
        {
            using var response = await runner.GetAsync($"/artifacts/{output.Id}", HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var saved = await artifacts.SaveAsync(
                new ArtifactWriteRequest(
                    output.Name,
                    output.ContentType,
                    output.Sha256,
                    ArtifactRole.Output,
                    output.Size),
                stream,
                principal,
                ct);
            copied.Add(saved.Artifact);
            await DeleteRunnerArtifactAsync(runner, output.Id, ct);
        }

        return copied;
    }

    private async Task DeleteRunnerArtifactAsync(HttpClient runner, string artifactId, CancellationToken ct)
    {
        try
        {
            using var response = await runner.DeleteAsync($"/artifacts/{artifactId}", ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning(
                    "Runner artifact cleanup returned {StatusCode} for artifact '{ArtifactId}'",
                    response.StatusCode,
                    artifactId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Runner artifact cleanup failed for artifact '{ArtifactId}'", artifactId);
        }
    }

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

file sealed class ArtifactException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
