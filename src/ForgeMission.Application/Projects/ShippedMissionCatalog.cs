using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Application;

/// <summary>
/// The shipped Janus mission, as Project-owned constants (Phase 48). It is release-reviewed code
/// comparable to a shipped model choice, not an operator-authored candidate: there is no download,
/// registry, provider selection, or dynamic lookup here, and no evaluation result is invented for
/// it. The release label is display provenance only — admission, pinning, package validation and
/// durable correlation all continue to use the version's GUID, numeric version, hash and immutable
/// package.
/// </summary>
internal static class ShippedMissionCatalog
{
    internal const string MissionName = "Janus";

    /// <summary>The user-facing release label. The immutable numeric version stays 1.</summary>
    internal const string ReleaseLabel = "1.4";

    /// <summary>The goal the managed chat Project is created with. A Project cannot exist without
    /// one, and this flow never asks a person for it.</summary>
    internal const string ProjectGoal = "Chat with Forge's shipped missions.";

    internal const MissionHandsProfile Profile = MissionHandsProfile.ProjectWorkspace;

    /// <summary>The small propose-then-approve mission the UI names. Exactly one declaration and
    /// exactly two experts, which is the durable package's own ceiling.</summary>
    internal const string Definition = "mission Janus(task) = {\n    Proposer\n    -> Approver\n}\n";

    /// <summary>The shipped pair. Kinds and input/output names are limited to what a durable
    /// package accepts, and the chain is what the definition above composes.</summary>
    internal static readonly (string Name, string Markdown)[] Experts =
    [
        ("Proposer", "---\nname: Proposer\nkind: llm\ninput: task\noutput: proposal\n---\nPropose a concise plan for the task below. State assumptions explicitly.\n\n{{task}}\n"),
        ("Approver", "---\nname: Approver\nkind: llm\ninput: proposal\noutput: answer\n---\nCheck the proposal below against the task it answers. Approve it, or decline and name the specific conflict.\n\n{{proposal}}\n"),
    ];
}
