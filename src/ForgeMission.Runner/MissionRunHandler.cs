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
internal sealed class MissionRunHandler(
    RunnerRegistry registry,
    IRunnerArtifactStore artifacts,
    ILogger<MissionRunHandler> logger)
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

        using var workspace = await RunWorkspace.CreateAsync(request.InputArtifacts, artifacts, ct);

        // Mission-level span: non-sensitive attributes (ref, provider, model) so a trace ties the
        // gen_ai.* + outbound-HTTP child spans to which @-agent ran. No API key is ever tagged.
        using var runSpan = RunnerTelemetry.Source.StartActivity("mission.run");
        runSpan?.SetTag("forge.mission.ref", request.MissionRef);
        runSpan?.SetTag("forge.provider", mission.Profile?.Provider);
        runSpan?.SetTag("gen_ai.request.model", mission.Profile?.Model);

        var accumulator = new UsageAccumulator();
        var runner = BuildRunner(mission, accumulator);

        var trace   = new List<RunTraceStep>();
        var attempt = 1;

        var decl = mission.Ast.Declarations.OfType<MissionDeclaration>().First();
        var vars = BuildVars(request, decl, workspace);

        var options = new PipelineRunOptions(
            decl.Name,
            vars,
            OnStepComplete: (expertName, envelope) =>
            {
                if (envelope.Status == "fail") attempt++;
                trace.Add(new RunTraceStep(expertName, envelope.Status, envelope.Text, envelope.Reason, attempt));
            },
            // Step-start → transient progress (41.7). No-op for the buffered path (onProgress null).
            OnStepStart: (expertName, kind) => onProgress?.Invoke(new RunProgress(expertName, kind)),
            // Sub-search narration from the kind:search step (41.7 Task 2) — Grok's per-query loop.
            OnSearchProgress: sp => onProgress?.Invoke(
                new RunProgress("WebSearch", sp.Kind, sp.Detail, sp.ResultCount)));

        // kind:search backend (Phase 41.2) — implicitly Grok, built from the runner's XAI_API_KEY operator
        // env var (null if unset ⇒ missions without kind:search are unaffected). Same seam as the CLI.
        var stopwatch = Stopwatch.StartNew();
        var result    = await new PipelineRunner(runner, webSearch: ProviderClientBuilder.BuildWebSearch())
            .RunAsync(mission.Ast, mission.Experts, options, ct);
        stopwatch.Stop();

        var verified  = result.Status == MissionStatus.Pass;
        var agentText = BuildAgentText(verified, trace, result);

        var usage = new RunUsage(
            InputTokens:    accumulator.InputTokens,
            OutputTokens:   accumulator.OutputTokens,
            ComputeSeconds: stopwatch.Elapsed.TotalSeconds,
            Model:          mission.Profile?.Model);

        var outputs = await workspace.CollectOutputsAsync(artifacts, ct);

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
            Usage:      usage,
            OutputArtifacts: outputs);
    }

    private static IExpertRunner BuildRunner(RunnerMission mission, UsageAccumulator accumulator)
    {
        if (mission.Profile is not { } profile)
            return new ExecExpertRunner();

        // Fresh usage-tracked runner per request → isolated token counts under concurrency.
        var instrumented = ProviderClientBuilder.BuildChatClient(profile)
            .AsBuilder()
            .UseOpenTelemetry(sourceName: RunnerTelemetry.SourceName)
            .Build();
        return new DirectExpertRunner(new UsageTrackingChatClient(instrumented, accumulator));
    }

    private static Dictionary<string, string> BuildVars(
        RunRequest request,
        MissionDeclaration decl,
        RunWorkspace workspace)
    {
        var paramName = decl.Params.FirstOrDefault() ?? "goal";
        var vars = new Dictionary<string, string>(StringComparer.Ordinal) { [paramName] = request.Goal };
        if (request.Vars is not null)
            foreach (var (k, v) in request.Vars)
                vars[k] = v;
        foreach (var (k, v) in workspace.ContextVars)
            vars[k] = v;
        if (vars.TryGetValue("mode", out var mode))
            vars["FORGE_MODE"] = mode;
        return vars;
    }

    private static string BuildAgentText(bool verified, List<RunTraceStep> trace, MissionResult result)
    {
        if (verified)
            return trace.LastOrDefault(e => e.ExpertName == "Answerer")?.Text ?? result.Text;

        var lastFailReason = trace.LastOrDefault(e => e.Status == "fail")?.Reason;
        if (string.IsNullOrWhiteSpace(lastFailReason))
        {
            return "Could not verify this answer after multiple attempts.";
        }

        return $"Could not verify: {lastFailReason}";
    }
}

internal sealed class RunWorkspace : IDisposable
{
    private RunWorkspace(string root, Dictionary<string, string> contextVars)
    {
        Root = root;
        ContextVars = contextVars;
    }

    public string Root { get; }
    public Dictionary<string, string> ContextVars { get; }

    public static async Task<RunWorkspace> CreateAsync(
        IReadOnlyList<RunArtifact>? inputArtifacts,
        IRunnerArtifactStore artifacts,
        CancellationToken ct)
    {
        var root = Path.Combine(Path.GetTempPath(), "forge-run", Guid.NewGuid().ToString("N"));
        var inputDir = Path.Combine(root, "inputs");
        var outputDir = Path.Combine(root, "outputs");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        var vars = BaseVars(root, inputDir, outputDir);
        if (inputArtifacts is { Count: > 0 })
            await StageInputsAsync(inputArtifacts, artifacts, inputDir, vars, ct);

        return new RunWorkspace(root, vars);
    }

    public async Task<IReadOnlyList<RunArtifact>> CollectOutputsAsync(
        IRunnerArtifactStore artifacts,
        CancellationToken ct)
    {
        var outputDir = ContextVars["output_dir"];
        var outputs = new List<RunArtifact>();
        foreach (var file in Directory.EnumerateFiles(outputDir, "*", SearchOption.TopDirectoryOnly))
        {
            await using var stream = File.OpenRead(file);
            outputs.Add(await artifacts.SaveAsync(
                new RunArtifactWriteRequest(
                    Name: Path.GetFileName(file),
                    ContentType: ContentTypeFor(file),
                    Sha256: "",
                    Role: "output",
                    DeclaredSize: stream.Length),
                stream,
                ct));
        }
        return outputs;
    }

    public void Dispose()
    {
        try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
        catch { /* best-effort scratch cleanup */ }
    }

    private static Dictionary<string, string> BaseVars(string root, string inputDir, string outputDir) => new(StringComparer.Ordinal)
    {
        ["work_dir"] = root,
        ["input_dir"] = inputDir,
        ["output_dir"] = outputDir,
        ["FORGE_WORK_DIR"] = root,
        ["FORGE_INPUT_DIR"] = inputDir,
        ["FORGE_OUTPUT_DIR"] = outputDir,
    };

    private static async Task StageInputsAsync(
        IReadOnlyList<RunArtifact> inputArtifacts,
        IRunnerArtifactStore artifacts,
        string inputDir,
        Dictionary<string, string> vars,
        CancellationToken ct)
    {
        for (var i = 0; i < inputArtifacts.Count; i++)
        {
            var artifact = inputArtifacts[i];
            await using var read = await artifacts.OpenAsync(artifact.Id, ct)
                ?? throw new InvalidOperationException($"Input artifact '{artifact.Id}' was not found in runner scratch.");

            var path = Path.Combine(inputDir, Path.GetFileName(artifact.Name));
            await using var file = File.Create(path);
            await read.Content.CopyToAsync(file, ct);

            vars[$"input_artifact_{i}"] = path;
            if (i == 0)
            {
                vars["source_file"] = path;
                vars["FORGE_SOURCE_FILE"] = path;
            }
        }
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".txt" => "text/plain",
        ".pdf" => "application/pdf",
        ".json" => "application/json",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };
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
