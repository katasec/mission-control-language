using ForgeMission.ConversationWorker.Janus;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>Which packaged mission a command's <c>MissionRef</c> names.</summary>
public enum WorkerMissionKind
{
    /// <summary>A MissionRef this Worker does not execute. It is reported as a failed run rather
    /// than guessed at.</summary>
    Unsupported,

    /// <summary>The Janus mission: negotiation, approval, and a tool-executing Implementer.</summary>
    Janus,

    /// <summary>The one-expert zero-tool Naive mission (43.21 task 1). It performs direct
    /// single-expert reasoning; it never requests a tool or selects another mission.</summary>
    Naive,

    /// <summary>The legacy Project-control turn (43.20 task 2). Same asset and same executor as
    /// <see cref="Naive"/>, but a control turn produces no run: it publishes one message fact with
    /// a null run id. Retained only until 43.21 task 3 deletes the route.</summary>
    ProjectControl,
}

/// <summary>
/// The named mission resolver that replaced <c>MissionCommandProcessor</c>'s hard-coded Janus
/// dispatch (43.20 task 2). Deliberately a switch over packaged missions rather than a registry or
/// plugin lookup, because a Worker executes only what is baked into its image — and the closed
/// catalog is what makes "no UI, Client Runtime, or Host component chooses an expert or provider"
/// true at the last possible layer rather than only at the first.
///
/// The catalog is exactly <c>Janus</c> and <c>Naive</c> (43.21 task 1). The legacy
/// <c>MissionControl</c> key resolves to the SAME loaded Naive asset rather than a second
/// near-identical checked-in copy; it maps to its own kind only because a control turn's fact
/// shape differs from a run's, not because it is a different mission.
/// </summary>
public sealed class WorkerMissionResolver(WorkerMissionContext janus, WorkerMissionContext naive)
{
    public const string JanusRef = "Janus";
    public const string NaiveRef = "Naive";

    /// <summary>The legacy Project-control mission ref. Removed by 43.21 task 3.</summary>
    public const string LegacyProjectControlRef = "MissionControl";

    public WorkerMissionContext Janus { get; } = janus;

    /// <summary>The one zero-tool single-expert asset, serving both <see cref="WorkerMissionKind.Naive"/>
    /// and the legacy control turn.</summary>
    public WorkerMissionContext Naive { get; } = naive;

    public WorkerMissionKind Resolve(string? missionRef) => missionRef switch
    {
        JanusRef => WorkerMissionKind.Janus,
        NaiveRef => WorkerMissionKind.Naive,
        LegacyProjectControlRef => WorkerMissionKind.ProjectControl,
        _ => WorkerMissionKind.Unsupported,
    };
}
