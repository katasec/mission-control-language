using ForgeMission.ClientRuntime.Transport;

namespace ForgeMission.ClientRuntime.Presentation;

/// <summary>
/// The mission picker's list (43.3 task 2/3) — a hardcoded proof list, not a rendering of
/// <see cref="ForgeMission.Core.Resolution.MissionDiscovery"/>'s scan. Both names are already live
/// on the hosted mission catalog today (<c>StaticMissionCatalog</c> in ForgeMission.Api), so
/// picking either one and sending a prompt is a real, deployable end-to-end attach — no new publish
/// or catalog change needed to prove the mechanism. Names only, no descriptions: that's a distinct,
/// deferred problem (see phase-43.3-mission-attach-point.md's open questions), not solved here.
/// </summary>
internal static class AttachableMissions
{
    public static readonly IReadOnlyList<AttachableMission> All =
    [
        new("ChatGPT", "vanilla"),
        new("Websearch", "websearch"),
        new("Janus", "Janus", SessionRuntimeKind.DurableConversation),
    ];

    public static AttachableMission Default => All[0];
}

/// <param name="Name">Shown in the picker.</param>
/// <param name="WireMission">Sent as the wire request's <c>Mission</c>/<c>model</c> field.</param>
/// <param name="Runtime">Mission (today's stateless per-prompt round-trip, the default) or
/// DurableConversation (the Client Runtime-owned durable Janus session, Phase 43.16 Task 7).</param>
internal sealed record AttachableMission(
    string Name, string WireMission, SessionRuntimeKind Runtime = SessionRuntimeKind.Mission);
