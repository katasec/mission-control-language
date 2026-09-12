using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Application;

/// <summary>
/// The one named product action set behind the live Mission Chat journey (Phase 48). It validates
/// and composes: managed-Project resolution, the shipped version's admission, one Host-owned
/// conversation, the existing fixed Client Runtime attachment, and the one history/tail owner. It
/// holds no snapshot, page, or tail logic of its own, writes no manifest, sequences no durable fact,
/// and exposes no retry, cancel, rename, search, sort, version, or profile choice.
/// </summary>
internal sealed class MissionChatService(
    ManagedChatProjectService managed,
    ProjectService projects,
    MissionVersionService versions,
    MissionConversationService conversations,
    MissionHandsConversationService hands,
    ApplicationSessionService sessions,
    IHttpClientFactory clients,
    Action<ApplicationEvent> publish,
    CancellationToken applicationStopping) : IMissionChatService
{
    /// <summary>Chat with a mission: find or provision the managed Project, open it, then create and
    /// attach one fresh chat on its shipped version. A person chooses nothing here.</summary>
    public async Task<StartMissionChatResponse> StartAsync(StartMissionChatRequest request, CancellationToken ct)
    {
        var resolution = await managed.ResolveAsync(ct);
        if (Refused(resolution) is { } refused)
            return new StartMissionChatResponse(null, null, null, null, null, null, 0, refused);

        var opened = await projects.OpenAsync(new ProjectOpenRequest(resolution.Home!), ct);
        if (opened.Session is not { } session)
            return new StartMissionChatResponse(null, null, null, null, null, null, 0,
                new MissionChatFailure(MissionChatFailureCode.ManagedProjectInvalid,
                    opened.Error?.Message ?? "The managed chat Project could not be opened."));

        var chat = await CreateChatAsync(session.SessionId, resolution.MissionId, ct);
        return new StartMissionChatResponse(session, chat.ConversationId, chat.Pin, chat.Access, chat.Rows,
            chat.Events, chat.ThroughSequence, chat.Failure);
    }

    /// <summary>New chat: one further chat in the session's own managed Project. It never re-resolves
    /// or re-provisions that Project and never selects a mission or version.</summary>
    public async Task<CreateMissionChatResponse> CreateAsync(CreateMissionChatRequest request, CancellationToken ct)
    {
        var home = RequiredHome(request.SessionId);
        var missionId = ShippedMissionId(home);
        if (missionId is not { } mission)
            return new CreateMissionChatResponse(null, null, null, null, null, 0,
                new MissionChatFailure(MissionChatFailureCode.MissionUnavailable,
                    "This Project no longer holds its shipped Janus version."));

        var chat = await CreateChatAsync(request.SessionId, mission, ct);
        return new CreateMissionChatResponse(chat.ConversationId, chat.Pin, chat.Access, chat.Rows,
            chat.Events, chat.ThroughSequence, chat.Failure);
    }

    /// <summary>Select a real chat row: confirm it belongs to this session's Project, then let the one
    /// history owner assemble it and resume its live stream. This method reads no snapshot, no page,
    /// and starts no tail itself.</summary>
    public async Task<OpenMissionChatResponse> OpenAsync(OpenMissionChatRequest request, CancellationToken ct)
    {
        var home = RequiredHome(request.SessionId);
        try
        {
            var rows = await conversations.ListChatRowsAsync(home, ct);
            if (rows.FirstOrDefault(row => row.ConversationId == request.ConversationId) is null)
                return new OpenMissionChatResponse(null, null, null, rows, null, 0,
                    new MissionChatFailure(MissionChatFailureCode.ChatNotFound,
                        "That chat is not part of this Project."));

            var pin = await ResolvePinAsync(home, request.ConversationId, ct);
            if (pin is null)
                return new OpenMissionChatResponse(null, null, null, rows, null, 0,
                    new MissionChatFailure(MissionChatFailureCode.ManagedProjectInvalid,
                        "This Project can no longer name the version that chat is pinned to."));

            var history = await OpenHistoryAsync(request.SessionId, request.ConversationId, ct);
            return new OpenMissionChatResponse(request.ConversationId, pin,
                new MissionChatAccess(MissionChatAccessState.Attached, null), rows,
                history.Events, history.ThroughSequence, null);
        }
        catch (MissionChatHistoryException exception)
        {
            return new OpenMissionChatResponse(null, null, null, null, null, 0, new MissionChatFailure(exception.Code, exception.Message));
        }
        catch (Exception exception) when (Unavailable(exception))
        {
            return new OpenMissionChatResponse(null, null, null, null, null, 0,
                new MissionChatFailure(MissionChatFailureCode.HistoryUnavailable, exception.Message));
        }
    }

    /// <summary>Send a message: one typed submit carrying only this session, this chat, one idempotent
    /// command identity, and the text. Host allocates the turn and attempt.</summary>
    public async Task<SubmitMissionChatTurnResponse> SubmitAsync(SubmitMissionChatTurnRequest request, CancellationToken ct)
    {
        RequiredHome(request.SessionId);
        if (request.ConversationId == Guid.Empty || request.CommandId == Guid.Empty || string.IsNullOrWhiteSpace(request.Text))
            return new SubmitMissionChatTurnResponse(null, null, null,
                new MissionChatFailure(MissionChatFailureCode.TurnConflict, "A chat, a command id, and a message are required."));

        try
        {
            var accepted = await conversations.SubmitAsync(request.ConversationId, request.CommandId, request.Text, ct);
            return new SubmitMissionChatTurnResponse(accepted.TurnId, accepted.AcceptedSequence, accepted.Status, null);
        }
        catch (ConversationHostProjectException exception)
        {
            return new SubmitMissionChatTurnResponse(null, null, null,
                new MissionChatFailure(MissionChatFailureCode.TurnConflict, exception.Error.Message));
        }
        catch (Exception exception) when (exception is HttpRequestException or ConversationHostProtocolException)
        {
            return new SubmitMissionChatTurnResponse(null, null, null,
                new MissionChatFailure(MissionChatFailureCode.StreamLost, exception.Message));
        }
    }

    // ── Composition ─────────────────────────────────────────────────────────────────────────────

    private sealed record CreatedChat(Guid? ConversationId, MissionChatPin? Pin, MissionChatAccess? Access,
        IReadOnlyList<MissionChatRow>? Rows, IReadOnlyList<ConversationEvent>? Events, long ThroughSequence,
        MissionChatFailure? Failure);

    /// <summary>Create one empty pinned chat, attach the version's fixed profile automatically, then
    /// open its (empty) history and live stream. A failed attachment leaves the chat real, listed, and
    /// selectable with its typed status: there is no profile fallback and no fake ready state.</summary>
    private async Task<CreatedChat> CreateChatAsync(string sessionId, Guid missionId, CancellationToken ct)
    {
        var home = RequiredHome(sessionId);
        var commandId = Guid.NewGuid();
        Guid conversationId;
        DurableMissionLaunch pinned;
        try
        {
            var created = await conversations.CreateAsync(home, missionId, commandId, ct);
            conversationId = created.ConversationId;
            pinned = created.Launch;
        }
        catch (ProjectOperationException exception)
        {
            return Failed(MissionChatFailureCode.MissionUnavailable, exception.Message);
        }
        catch (ConversationHostProjectException exception)
        {
            return Failed(MissionChatFailureCode.MissionUnavailable, exception.Error.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or ConversationHostProtocolException)
        {
            // The request left and its outcome did not come back. The command identity stays the same
            // one, so Host answers it once if it is ever asked again; nothing here creates a second chat.
            return Failed(MissionChatFailureCode.CreateUncertain,
                $"We could not confirm whether the chat was created. ({exception.Message})");
        }

        var access = await AttachAsync(sessionId, conversationId, pinned, ct);
        try
        {
            var pin = await ResolvePinAsync(home, conversationId, ct);
            var rows = await conversations.ListChatRowsAsync(home, ct);
            if (pin is null)
                return new CreatedChat(conversationId, null, access, rows, null, 0,
                    new MissionChatFailure(MissionChatFailureCode.ManagedProjectInvalid,
                        "This Project can no longer name the version that chat is pinned to."));

            var history = await OpenHistoryAsync(sessionId, conversationId, ct);
            return new CreatedChat(conversationId, pin, access, rows, history.Events, history.ThroughSequence, null);
        }
        catch (MissionChatHistoryException exception)
        {
            return new CreatedChat(conversationId, null, access, null, null, 0, new MissionChatFailure(exception.Code, exception.Message));
        }
        catch (Exception exception) when (Unavailable(exception))
        {
            return new CreatedChat(conversationId, null, access, null, null, 0,
                new MissionChatFailure(MissionChatFailureCode.HistoryUnavailable, exception.Message));
        }
    }

    /// <summary>The version's own fixed profile, attached automatically. Application re-resolves the
    /// exact approved launch locally; a caller supplies no profile and acknowledges nothing.</summary>
    private async Task<MissionChatAccess> AttachAsync(string sessionId, Guid conversationId, DurableMissionLaunch pinned, CancellationToken ct)
    {
        if (!sessions.TryGet(sessionId, out var session) || session is null)
            throw new KeyNotFoundException();

        var launch = projects.ResolveApprovedLaunch(session, pinned.MissionVersionId, pinned.VersionNumber, pinned.DefinitionHash);
        if (launch is null)
            return new MissionChatAccess(MissionChatAccessState.Unavailable,
                "The approved mission launch is missing or no longer matches.");

        try
        {
            var reason = await hands.AttachApprovedForRunAsync(session, conversationId, launch, ct);
            return reason is null
                ? new MissionChatAccess(MissionChatAccessState.Attached, null)
                : new MissionChatAccess(MissionChatAccessState.Unavailable, reason);
        }
        catch (Exception exception) when (Unavailable(exception))
        {
            return new MissionChatAccess(MissionChatAccessState.Unavailable, exception.Message);
        }
    }

    private Task<MissionChatHistory> OpenHistoryAsync(string sessionId, Guid conversationId, CancellationToken ct)
    {
        if (!sessions.TryGet(sessionId, out var session) || session is null)
            throw new KeyNotFoundException();

        return session.MissionChat.OpenAsync(
            () => new MissionChatStream(session.Id, conversations,
                new ConversationHostClient(clients.CreateClient("conversation-host")), publish, applicationStopping),
            conversationId, ct);
    }

    /// <summary>A chat's read-only provenance, joined from what Host pinned and what this Project can
    /// name. A version this Project cannot name produces no pin rather than an invented one.</summary>
    private async Task<MissionChatPin?> ResolvePinAsync(string home, Guid conversationId, CancellationToken ct)
    {
        var snapshot = await conversations.ReadSnapshotAsync(conversationId, ct);
        if (snapshot.PinnedLaunch is not { } launch)
            return null;

        var identity = await conversations.ResolveIdentityAsync(home, launch.MissionVersionId, ct);
        if (identity is null)
            return null;

        var approved = await versions.ListApprovedVersionsAsync(home, ct);
        var isApproved = approved.Any(item => item.MissionVersionId == launch.MissionVersionId);
        return new MissionChatPin(identity.MissionName, launch.VersionNumber,
            identity.ReleaseLabel ?? $"v{launch.VersionNumber}", launch.Profile, isApproved);
    }

    private Guid? ShippedMissionId(string home)
    {
        var definitions = projects.ReadForHome(home).Manifest.MissionDefinitions ?? [];
        return definitions
            .Where(item => string.Equals(item.Name, ShippedMissionCatalog.MissionName, StringComparison.OrdinalIgnoreCase))
            .Select(item => (Guid?)item.MissionId)
            .SingleOrDefault();
    }

    private static MissionChatFailure? Refused(ManagedChatProjectResolution resolution) => resolution.Outcome switch
    {
        ManagedChatProjectOutcome.Provisioned or ManagedChatProjectOutcome.Reused => null,
        ManagedChatProjectOutcome.PackageUnavailable => new MissionChatFailure(MissionChatFailureCode.MissionUnavailable,
            resolution.Reason ?? "The shipped Janus mission is not available."),
        _ => new MissionChatFailure(MissionChatFailureCode.ManagedProjectInvalid,
            resolution.Reason ?? "The managed chat Project is not usable."),
    };

    private static CreatedChat Failed(MissionChatFailureCode code, string message) =>
        new(null, null, null, null, null, 0, new MissionChatFailure(code, message));

    // A stale or foreign session is not a product outcome, so it leaves as the KeyNotFoundException
    // every other session-scoped owner throws and the Host route already answers with 404.
    private string RequiredHome(string sessionId) =>
        sessions.TryGet(sessionId, out var session) && session is not null
            ? session.ProjectHome
            : throw new KeyNotFoundException();

    private static bool Unavailable(Exception exception) =>
        exception is ConversationHostProjectException or HttpRequestException or ConversationHostProtocolException
            or ProjectOperationException;
}
