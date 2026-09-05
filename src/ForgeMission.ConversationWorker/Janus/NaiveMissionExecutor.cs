using ForgeMission.Core.Runtime;

namespace ForgeMission.ConversationWorker.Janus;

/// <summary>
/// Runs the checked-in, read-only zero-tool <c>Naive</c> mission (43.21 task 1) — one expert, one
/// direct answer.
///
/// Deliberately much smaller than <see cref="JanusMissionExecutor"/>, and deliberately NOT routed
/// through <see cref="JanusPipelineProgressMapper"/>: this is a single-expert mission whose whole
/// result is its returned text, so its caller publishes one message fact rather than mapping a
/// participant timeline. Nothing here can request a tool or select Janus.
/// </summary>
public static class NaiveMissionExecutor
{
    public const string MissionName = "Naive";

    /// <summary>Runs the mission and returns its result. Tools are withheld at BOTH levels: the
    /// packaged expert declares no <c>role: agent</c>, and <see cref="PipelineRunOptions.Tools"/>
    /// is left null here so no tool declaration ever reaches the provider. The trace hook exists
    /// only to fail loudly if a tool request somehow appears, rather than silently mapping one —
    /// that guard is what makes "Naive rejects tool requests" structural rather than a promise.</summary>
    public static Task<MissionResult> RunTurnAsync(
        WorkerMissionContext mission, string projectGoal, string task, CancellationToken ct)
    {
        var runner = new PipelineRunner(mission.Runners, mission.Execution);

        Task OnTrace(PipelineTraceEvent traceEvent, CancellationToken innerCt)
        {
            if (traceEvent is PipelineToolRequested)
                throw new InvalidOperationException(
                    "The Naive mission requested a tool. It is a zero-tool single-expert mission and its " +
                    "packaged expert declares no agent role — this indicates a corrupted mission asset.");

            return Task.CompletedTask;
        }

        var options = new PipelineRunOptions(
            MissionName: MissionName,
            Vars: new Dictionary<string, string> { ["projectGoal"] = projectGoal, ["task"] = task },
            MissionPath: [MissionName],
            OnTrace: OnTrace);

        return runner.RunAsync(mission.Ast, mission.Experts, options, ct);
    }
}
