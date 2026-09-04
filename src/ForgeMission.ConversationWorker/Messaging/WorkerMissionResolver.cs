using ForgeMission.ConversationWorker.Janus;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>Which packaged mission a command's <c>MissionRef</c> names.</summary>
public enum WorkerMissionKind
{
    /// <summary>A MissionRef this Worker does not execute. It is reported as a failed run rather
    /// than guessed at.</summary>
    Unsupported,

    /// <summary>The Janus mission-run mission: negotiation, approval, and a tool-executing
    /// Implementer.</summary>
    Janus,

    /// <summary>The zero-tool Project refinement mission behind a Project's Mission Control
    /// conversation. It never requests a tool, starts a run, or selects Janus.</summary>
    MissionControl,
}

/// <summary>
/// The named mission resolver that replaced <c>MissionCommandProcessor</c>'s hard-coded Janus
/// dispatch (43.20 task 2). Two fixed keys and one unsupported case — deliberately a switch over
/// packaged missions rather than a registry or plugin lookup, because a Worker executes only what
/// is baked into its image.
/// </summary>
public sealed class WorkerMissionResolver(WorkerMissionContext janus, WorkerMissionContext missionControl)
{
    public const string JanusRef = "Janus";
    public const string MissionControlRef = "MissionControl";

    public WorkerMissionContext Janus { get; } = janus;
    public WorkerMissionContext MissionControl { get; } = missionControl;

    public WorkerMissionKind Resolve(string? missionRef) => missionRef switch
    {
        JanusRef => WorkerMissionKind.Janus,
        MissionControlRef => WorkerMissionKind.MissionControl,
        _ => WorkerMissionKind.Unsupported,
    };
}
