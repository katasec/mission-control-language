using System.Diagnostics;
using System.Text.Json;
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
    /// <summary>Base dir for per-run work folders (38.9 file-in/out). Per-run subdirs keep concurrent
    /// runs isolated and the runner stateless — nothing survives a run.</summary>
    private static readonly string WorkRoot =
        Environment.GetEnvironmentVariable("WORK_ROOT") ?? Path.Combine(Path.GetTempPath(), "forge-work");

    public async Task<RunResponse?> RunAsync(RunRequest request, CancellationToken ct)
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

        // File-in staging (38.9, D5 ownership split): materialize the uploaded bytes into a fresh
        // per-run work dir and expose them to the mission as source_pdf/work_dir. Only the runner
        // knows this path — the orchestrator sent bytes + filename, never a /work path. Per-run dir
        // (not a shared /work) so concurrent runs never collide; cleaned up in the finally below.
        string? workDir = null;
        if (request.Input is { } input)
        {
            workDir = Path.Combine(WorkRoot, "run-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);
            var ext       = Path.GetExtension(input.FileName);
            var inputPath = Path.Combine(workDir, "input" + (string.IsNullOrEmpty(ext) ? ".pdf" : ext));
            await File.WriteAllBytesAsync(inputPath, Convert.FromBase64String(input.Base64), ct);
            vars["source_pdf"] = inputPath;
            vars["work_dir"]   = workDir;
        }

        var options = new PipelineRunOptions(
            decl.Name,
            vars,
            OnStepComplete: (expertName, envelope) =>
            {
                if (envelope.Status == "fail") attempt++;
                trace.Add(new RunTraceStep(expertName, envelope.Status, envelope.Text, envelope.Reason, attempt));
            });

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result    = await new PipelineRunner(runner).RunAsync(mission.Ast, mission.Experts, options, ct);
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

            // File-out collection (38.9): if the mission wrote a file into the work dir, carry it back
            // inline. Convention {work_dir}/output.pdf; display name from a step's `output_name`
            // (generic key), else the on-disk name. Only collected on a verified run — an unverified
            // transformation must not hand back a file that failed the integrity check.
            RunArtifact? output = null;
            if (verified && workDir is not null)
            {
                var producedPath = Path.Combine(workDir, "output.pdf");
                if (File.Exists(producedPath))
                {
                    var bytes = await File.ReadAllBytesAsync(producedPath, ct);
                    output = new RunArtifact(
                        FileName:    ExtractOutputName(trace) ?? "output.pdf",
                        ContentType: "application/pdf",
                        Base64:      Convert.ToBase64String(bytes));
                }
            }

            var usage = new RunUsage(
                InputTokens:    accumulator.InputTokens,
                OutputTokens:   accumulator.OutputTokens,
                ComputeSeconds: stopwatch.Elapsed.TotalSeconds,
                Model:          mission.Profile?.Model);

            logger.LogInformation(
                "Ran '{MissionRef}' [{Policy}] — verified={Verified} steps={Steps} in {Tokens}+{Out} tok / {Secs:F2}s artifact={Artifact}",
                request.MissionRef, request.Policy, verified, trace.Count,
                usage.InputTokens, usage.OutputTokens, usage.ComputeSeconds, output?.FileName ?? "none");

            return new RunResponse(
                AgentText:  agentText,
                Verified:   verified,
                StepCount:  trace.Count,
                RetryCount: result.Attempts - 1,
                Trace:      trace,
                Usage:      usage,
                Output:     output);
        }
        finally
        {
            // Best-effort cleanup — the runner keeps no per-run state on disk.
            if (workDir is not null)
                try { Directory.Delete(workDir, recursive: true); } catch { /* nothing to recover */ }
        }
    }

    /// <summary>
    /// Pull a download display name from the run trace (38.9). Generic convention: a step that
    /// produces a downloadable file emits an <c>output_name</c> string in its structured output.
    /// Scans newest→oldest and ignores steps whose text isn't a JSON object. Null → caller falls
    /// back to the on-disk name.
    /// </summary>
    private static string? ExtractOutputName(IReadOnlyList<RunTraceStep> trace)
    {
        for (var i = trace.Count - 1; i >= 0; i--)
        {
            var text = trace[i].Text;
            if (string.IsNullOrWhiteSpace(text)) continue;
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("output_name", out var n)
                    && n.ValueKind == JsonValueKind.String)
                {
                    var name = n.GetString();
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
            }
            catch (JsonException) { /* step output isn't JSON — skip */ }
        }
        return null;
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
