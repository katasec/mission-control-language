using ForgeMission.Core.Runtime;

namespace ForgeMission.ConversationWorker.Janus;

/// <summary>
/// Runs one turn of the checked-in, read-only zero-tool <c>MissionControl</c> mission — the
/// project-refinement conversation behind a Project's Mission Control (43.20 task 2).
///
/// Deliberately much smaller than <see cref="JanusMissionExecutor"/>, and deliberately NOT routed
/// through <see cref="JanusPipelineProgressMapper"/>: this is a single-expert mission whose whole
/// result is its returned text, so its caller publishes exactly one fact per outcome rather than
/// mapping a participant timeline. Nothing here can start a run, request a tool, or select Janus.
/// </summary>
public static class MissionControlMissionExecutor
{
    public const string MissionName = "MissionControl";

    /// <summary>Runs the refinement turn and returns its result. Tools are withheld at BOTH
    /// levels: the packaged expert declares no <c>role: agent</c>, and <see cref="PipelineRunOptions.Tools"/>
    /// is left null here so no tool declaration ever reaches the provider. The trace hook exists
    /// only to fail loudly if a tool request somehow appears, rather than silently mapping one.</summary>
    public static Task<MissionResult> RunTurnAsync(
        WorkerMissionContext mission, string projectGoal, string task, CancellationToken ct)
    {
        var runner = new PipelineRunner(mission.Runners, mission.Execution);

        Task OnTrace(PipelineTraceEvent traceEvent, CancellationToken innerCt)
        {
            if (traceEvent is PipelineToolRequested)
                throw new InvalidOperationException(
                    "The MissionControl mission requested a tool. It is a zero-tool refinement mission and " +
                    "its packaged expert declares no agent role — this indicates a corrupted mission asset.");

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
