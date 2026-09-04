using System.Net;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Services;

// Owns one Project's Mission Control conversation on the Client Runtime side (43.20 task 2):
// resolving the Project from the session's own root, creating the durable conversation only when
// the manifest holds no ID yet, writing that ID back after Host acceptance, and following the
// stream through the shared ConversationTailReader.
//
// It has no capability registry, no dispatcher, and no tool executor — not by rule but by
// construction: it passes no per-event hook to the tail reader, so no code path exists here that
// could reach local authority. Its counterpart ConversationRuntimeSession keeps the Janus tool
// hand-off unchanged.
internal sealed class ProjectControlRuntimeSession(
    string sessionId,
    string projectHome,
    ProjectStore projects,
    ConversationHostClient hostClient,
    Action<ClientRuntimeEvent> publish,
    CancellationToken applicationStopping) : IAsyncDisposable
{
    private readonly ConversationTailReader _tail = new(sessionId, hostClient, publish, applicationStopping);

    private Guid? _conversationId;

    /// <summary>Opens the Project's Mission Control conversation and starts its replay/tail.
    /// A stored ID takes the replay path with NO create and NO submit; a null ID takes the
    /// idempotent create path and persists the returned ID. Idempotent within a session: a second
    /// call returns the already-opened conversation without touching the Host.</summary>
    public async Task<Guid> OpenAsync(CancellationToken ct)
    {
        if (_conversationId is { } alreadyOpen)
            return alreadyOpen;

        // The Runtime reads the manifest itself — a surface never supplies a Project path or a
        // conversation ID.
        var project = projects.Open(projectHome).Project
            ?? throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                $"{projectHome} holds no Forge Project manifest.");

        var conversationId = project.Manifest.MissionControlConversationId
            ?? await CreateAndRecordAsync(project, ct);

        _conversationId = conversationId;
        _tail.Start(conversationId);
        return conversationId;
    }

    /// <summary>Submits one refinement turn against the opened control conversation. It carries
    /// only the command ID and the person's text: the Project goal reaches the mission from the
    /// Conversation checkpoint, and no capability, path, tool, or run value exists to send.</summary>
    public async Task<SubmitProjectControlMessageResponse> SubmitAsync(Guid commandId, string text, CancellationToken ct)
    {
        var conversationId = _conversationId ?? throw new MissionControlNotOpenedException();

        return await hostClient.SubmitProjectControlMessageAsync(
            new SubmitProjectControlMessageRequest(conversationId, commandId, text), ct);
    }

    // The create command ID is derived from the stable manifest project ID, so this whole method is
    // safe to repeat: a retry after Host acceptance but before the manifest write re-derives the
    // same command ID, reaches the same deterministic conversation, and gets its original
    // acceptance back rather than creating a second control conversation.
    private async Task<Guid> CreateAndRecordAsync(ProjectRecord project, CancellationToken ct)
    {
        var response = await hostClient.CreateProjectControlAsync(
            new CreateProjectControlConversationRequest(
                project.Manifest.ProjectId,
                ConversationDeterministicIds.ProjectControlCreate(project.Manifest.ProjectId),
                project.Manifest.Goal),
            ct);

        // Only after durable acceptance. A failed write leaves the conversation valid and reports
        // ManifestWriteFailed — never a new conversation, never a successful local write.
        projects.SetMissionControlConversationId(project.Home, response.ConversationId);
        return response.ConversationId;
    }

    /// <summary>Maps an expected Conversation-service rejection to this Project's typed error
    /// vocabulary. An unexpected status is left to fail the transport normally rather than being
    /// laundered into a domain code.</summary>
    public static ProjectOperationErrorCode? ToErrorCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => ProjectOperationErrorCode.MissionControlInvalid,
        HttpStatusCode.NotFound => ProjectOperationErrorCode.MissionControlNotFound,
        HttpStatusCode.Conflict => ProjectOperationErrorCode.MissionControlConflict,
        _ => null,
    };

    public ValueTask DisposeAsync() => _tail.DisposeAsync();
}

/// <summary>A turn submitted before Mission Control was opened for this session. A dedicated type
/// rather than a bare <see cref="InvalidOperationException"/>: the endpoint maps THIS to a typed
/// outcome, and mapping the general exception would also launder unrelated faults — a missing
/// HttpClient base address, for one — into a domain error a surface would render as normal.
/// Raised from both places that can observe it: the session slot (no session yet) and the session
/// itself (constructed, but its open never completed).</summary>
internal sealed class MissionControlNotOpenedException()
    : Exception("Mission Control has not been opened for this session.");
