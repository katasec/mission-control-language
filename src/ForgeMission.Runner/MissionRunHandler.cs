using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using ForgeMission.Runner.Contracts;
using Microsoft.Extensions.AI;

namespace ForgeMission.Runner;

/// <summary>
/// Executes a mission run for one <see cref="RunRequest"/> and shapes the result into a
/// <see cref="RunResponse"/>. This is the extracted core of ForgeUI's old <c>MissionService</c>:
/// same display-selection and trust logic, now running container-side and emitting cost signals.
/// Stateless — one instance is fine for the whole (concurrent) process; per-run state lives in
/// locals and a per-run <see cref="UsageAccumulator"/>.
/// </summary>
internal sealed class MissionRunHandler(RunnerRegistry registry, ILogger<MissionRunHandler> logger)
{
    /// <summary>Emit a keep-alive if no step-progress event has flowed for this long, so the long
    /// kind:search step (~40s of server-side silence) can't be reaped by an idle timeout (41.7).</summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <summary>Buffered run (Phase 39.1) — the non-interactive contract behind <c>POST /run</c>.
    /// Runs to completion, then returns result + trace + cost. Unchanged for the CLI.</summary>
    public Task<RunResponse?> RunAsync(RunRequest request, CancellationToken ct)
        => ExecuteAsync(request, onProgress: null, ct);

    /// <summary>
    /// Streaming run (Phase 41.7) — the interactive contract behind <c>POST /run/stream</c>. Yields
    /// <c>progress</c> events as steps begin and <c>heartbeat</c> events across quiet gaps, then a
    /// terminal <c>result</c> (or <c>error</c>). The run executes on a background task writing into a
    /// channel; this reader forwards events and injects a heartbeat whenever the channel goes quiet.
    /// </summary>
    public async IAsyncEnumerable<RunStreamEvent> RunStreamAsync(
        RunRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<RunStreamEvent>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        _ = Task.Run(() => DrainRunIntoChannel(request, channel.Writer, ct), ct);

        var reader = channel.Reader;
        while (true)
        {
            var ready  = reader.WaitToReadAsync(ct).AsTask();
            var winner = await Task.WhenAny(ready, Task.Delay(HeartbeatInterval, ct)).ConfigureAwait(false);
            if (winner != ready)
            {
                yield return new RunStreamEvent("heartbeat");   // quiet gap → keep the connection warm
                continue;
            }
            if (!await ready.ConfigureAwait(false))
                break;                                          // channel completed and drained
            while (reader.TryRead(out var evt))
                yield return evt;
        }
    }

    // Run the mission, mapping each step-start to a progress event and the outcome to a terminal event.
    // Any failure becomes a single `error` event so the reader always terminates cleanly.
    private async Task DrainRunIntoChannel(
        RunRequest request, ChannelWriter<RunStreamEvent> writer, CancellationToken ct)
    {
        try
        {
            var response = await ExecuteAsync(
                request,
                onProgress: p => writer.TryWrite(new RunStreamEvent("progress", Progress: p)),
                ct);

            writer.TryWrite(response is null
                ? new RunStreamEvent("error", Error: $"Unknown mission '{request.MissionRef}'.")
                : new RunStreamEvent("result", Result: response));
        }
        catch (Exception ex)
        {
            writer.TryWrite(new RunStreamEvent("error", Error: ex.Message));
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task<RunResponse?> ExecuteAsync(
        RunRequest request, Action<RunProgress>? onProgress, CancellationToken ct)
    {
        if (!registry.TryGet(request.MissionRef, out var mission))
        {
            logger.LogWarning("Unknown mission ref '{MissionRef}'", request.MissionRef);
            return null; // → 404 at the endpoint
        }

        // Per-run policy hook (39.1). All built-ins run under the trusted policy; enforcement of the
        // restricted policy (deny exec/http, restricted egress) lands in 39.5 with custom missions.
        RunPolicyGate.EnsureAllowed(mission, request.Policy);

        // Mission-level span: non-sensitive attributes (ref, provider, model) so a trace ties the
        // gen_ai.* + outbound-HTTP child spans to which @-agent ran. No API key is ever tagged.
        using var runSpan = RunnerTelemetry.Source.StartActivity("mission.run");
        runSpan?.SetTag("forge.mission.ref", request.MissionRef);
        runSpan?.SetTag("forge.provider", mission.Profile?.Provider);
        runSpan?.SetTag("gen_ai.request.model", mission.Profile?.Model);

        // Fresh usage-tracked runner per request → isolated token counts under concurrency.
        var accumulator = new UsageAccumulator();
        IExpertRunner runner;
        if (mission.Profile is { } profile)
        {
            // Instrument the provider client for gen_ai.* spans (model + token usage). Sensitive data
            // stays OFF, so prompts/answers/keys never enter a span. OTel sits closest to the provider
            // (inside UsageTrackingChatClient) so it observes the real outbound call.
            var instrumented = ProviderClientBuilder.BuildChatClient(profile)
                .AsBuilder()
                .UseOpenTelemetry(sourceName: RunnerTelemetry.SourceName)
                .Build();
            runner = new DirectExpertRunner(new UsageTrackingChatClient(instrumented, accumulator));
        }
        else
        {
            runner = new ExecExpertRunner();
        }

        var trace   = new List<RunTraceStep>();
        var attempt = 1;

        var decl      = mission.Ast.Declarations.OfType<MissionDeclaration>().First();
        var paramName = decl.Params.FirstOrDefault() ?? "goal";
        var vars      = new Dictionary<string, string>(StringComparer.Ordinal) { [paramName] = request.Goal };
        if (request.Vars is not null)
            foreach (var (k, v) in request.Vars)
                vars[k] = v;

        var options = new PipelineRunOptions(
            decl.Name,
            vars,
            OnStepComplete: (expertName, envelope) =>
            {
                if (envelope.Status == "fail") attempt++;
                trace.Add(new RunTraceStep(expertName, envelope.Status, envelope.Text, envelope.Reason, attempt));
            },
            // Step-start → transient progress (41.7). No-op for the buffered path (onProgress null).
            OnStepStart: (expertName, kind) => onProgress?.Invoke(new RunProgress(expertName, kind)));

        // kind:search backend (Phase 41.2) — implicitly Grok, built from the runner's XAI_API_KEY operator
        // env var (null if unset ⇒ missions without kind:search are unaffected). Same seam as the CLI.
        var stopwatch = Stopwatch.StartNew();
        var result    = await new PipelineRunner(runner, webSearch: ProviderClientBuilder.BuildWebSearch())
            .RunAsync(mission.Ast, mission.Experts, options, ct);
        stopwatch.Stop();

        var verified = result.Status == MissionStatus.Pass;

        string agentText;
        if (verified)
        {
            agentText = trace.LastOrDefault(e => e.ExpertName == "Answerer")?.Text ?? result.Text;
        }
        else
        {
            var lastFailReason = trace.LastOrDefault(e => e.Status == "fail")?.Reason;
            agentText = string.IsNullOrWhiteSpace(lastFailReason)
                ? "Could not verify this answer after multiple attempts."
                : $"Could not verify: {lastFailReason}";
        }

        var usage = new RunUsage(
            InputTokens:    accumulator.InputTokens,
            OutputTokens:   accumulator.OutputTokens,
            ComputeSeconds: stopwatch.Elapsed.TotalSeconds,
            Model:          mission.Profile?.Model);

        logger.LogInformation(
            "Ran '{MissionRef}' [{Policy}] — verified={Verified} steps={Steps} in {Tokens}+{Out} tok / {Secs:F2}s",
            request.MissionRef, request.Policy, verified, trace.Count,
            usage.InputTokens, usage.OutputTokens, usage.ComputeSeconds);

        return new RunResponse(
            AgentText:  agentText,
            Verified:   verified,
            StepCount:  trace.Count,
            RetryCount: result.Attempts - 1,
            Trace:      trace,
            Usage:      usage);
    }
}

/// <summary>
/// The per-run trust boundary (decision 10). 39.1 only carries the attribute; the restricted policy
/// enforcement (custom missions limited to <c>llm</c>/<c>rule</c> kinds, no <c>exec</c>/<c>http</c>)
/// is implemented in 39.5. This gate is the single seam that will grow that logic.
/// </summary>
internal static class RunPolicyGate
{
    public static void EnsureAllowed(RunnerMission mission, string policy)
    {
        _ = mission;
        // 39.1: built-ins run trusted; anything unrecognised is treated as trusted too (no custom
        // missions exist yet). 39.5 tightens this to reject exec/http kinds under Restricted.
        if (policy is not (RunPolicy.Trusted or RunPolicy.Restricted))
            throw new ArgumentException($"Unknown run policy '{policy}'.", nameof(policy));
    }
}
