using System.Diagnostics;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using ForgeMission.Runner.Contracts;

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

        // Fresh usage-tracked runner per request → isolated token counts under concurrency.
        var accumulator = new UsageAccumulator();
        IExpertRunner runner = mission.Profile is { } profile
            ? new DirectExpertRunner(new UsageTrackingChatClient(ProviderClientBuilder.BuildChatClient(profile), accumulator))
            : new ExecExpertRunner();

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
            });

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

        var usage = new RunUsage(
            InputTokens:    accumulator.InputTokens,
            OutputTokens:   accumulator.OutputTokens,
            ComputeSeconds: stopwatch.Elapsed.TotalSeconds);

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
