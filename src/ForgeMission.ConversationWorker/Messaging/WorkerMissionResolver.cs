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

}

/// <summary>
/// The named mission resolver that replaced <c>MissionCommandProcessor</c>'s hard-coded Janus
/// dispatch (43.20 task 2). Deliberately a switch over packaged missions rather than a registry or
/// plugin lookup, because a Worker executes only what is baked into its image — and the closed
/// catalog is what makes "no UI, Client Runtime, or Host component chooses an expert or provider"
/// true at the last possible layer rather than only at the first.
///
/// The catalog is exactly <c>Janus</c> and <c>Naive</c>. Unknown historical command bodies
/// remain parseable but resolve to <see cref="WorkerMissionKind.Unsupported"/> and follow the
/// existing terminal failure/dead-letter path; they never execute as Naive.
/// </summary>
public sealed class WorkerMissionResolver(WorkerMissionContext janus, WorkerMissionContext naive)
{
    public const string JanusRef = "Janus";
    public const string NaiveRef = "Naive";

    public WorkerMissionContext Janus { get; } = janus;

    /// <summary>The one zero-tool single-expert mission asset.</summary>
    public WorkerMissionContext Naive { get; } = naive;

    public WorkerMissionKind Resolve(string? missionRef) => missionRef switch
    {
        JanusRef => WorkerMissionKind.Janus,
        NaiveRef => WorkerMissionKind.Naive,
        _ => WorkerMissionKind.Unsupported,
    };
}
