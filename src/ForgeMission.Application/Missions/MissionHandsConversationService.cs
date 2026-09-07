using ForgeMission.Application.Transport;
using ForgeMission.ClientRuntime;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Tools;

namespace ForgeMission.Application;

/// <summary>
/// Application's narrow grant boundary for generic mission hands. It re-resolves a locally
/// approved launch, requires an acknowledgement, creates a fresh Bob root, then registers only
/// its ephemeral correlation with Host. Neither Host nor the transport sees the Project root.
/// </summary>
internal sealed class MissionHandsConversationService(
    ProjectService projects,
    ApplicationSessionService sessions,
    IHttpClientFactory clients,
    CapabilityAuthorizationPolicy policy,
    CancellationToken applicationStopping) : IMissionHandsConversationService
{
    /// <summary>Application's generic-start path creates the one live Bob attachment before it
    /// releases the immutable package to Host. This internal operation has no transport request,
    /// so a caller cannot select its package, profile, or capability set.</summary>
    internal async Task<string?> AttachApprovedForRunAsync(ApplicationSession session, Guid conversationId,
        MissionVersionLaunch launch, CancellationToken ct)
    {
        var attachmentId = Guid.NewGuid();
        ClientExecutionSession execution;
        try
        {
            execution = ClientExecutionSession.CreateForMission(session.ProjectHome, ToExecutionProfile(launch.CapabilityProfile),
                policy, session.Confirmation, applicationStopping);
        }
        catch (PlatformNotSupportedException exception)
        {
            return exception.Message;
        }

        var durableLaunch = new DurableMissionLaunch(launch.MissionVersionId, launch.VersionNumber, launch.DefinitionHash,
            launch.Definition, ToDurableProfile(launch.CapabilityProfile), launch.Package);
        var host = new ConversationHostClient(clients.CreateClient("conversation-host"));
        try
        {
            await session.MissionHands.RevokeAsync(ct);
            var attached = await host.AttachMissionHandsAsync(new AttachMissionHandsRequest(conversationId, attachmentId,
                Guid.ParseExact(session.Id, "N"), durableLaunch), ct);
            if (attached.Status is not (MissionHandsStatus.Attached or MissionHandsStatus.AwaitingHands or MissionHandsStatus.InFlight))
            {
                await execution.DisposeAsync();
                return attached.Reason ?? "Host rejected the generic mission hands attachment.";
            }
            await session.MissionHands.ReplaceAsync(conversationId, attachmentId, Guid.ParseExact(session.Id, "N"),
                launch.CapabilityProfile.ToString(), execution, host, ct);
            return null;
        }
        catch
        {
            await execution.DisposeAsync();
            throw;
        }
    }

    public async Task<AcknowledgeMissionHandsResponse> AcknowledgeAsync(AcknowledgeMissionHandsRequest request, CancellationToken ct)
    {
        if (!request.ProfileAccepted)
            return new AcknowledgeMissionHandsResponse(null, null, null, "The exact approved profile must be acknowledged.");
        if (!sessions.TryGet(request.SessionId, out var session) || session is null)
            return new AcknowledgeMissionHandsResponse(null, null, null, "The application session is stale or foreign.");

        var launch = projects.ResolveApprovedLaunch(session, request.MissionVersionId, request.VersionNumber, request.DefinitionHash);
        if (launch is null)
            return new AcknowledgeMissionHandsResponse(null, null, null, "The approved mission launch is missing or no longer matches.");

        var attachmentId = Guid.NewGuid();
        ClientExecutionSession execution;
        try
        {
            execution = ClientExecutionSession.CreateForMission(
                session.ProjectHome, ToExecutionProfile(launch.CapabilityProfile), policy,
                session.Confirmation, applicationStopping);
        }
        catch (PlatformNotSupportedException exception)
        {
            return new AcknowledgeMissionHandsResponse(null, launch.CapabilityProfile.ToString(), null, exception.Message);
        }

        var durableLaunch = new DurableMissionLaunch(
            launch.MissionVersionId, launch.VersionNumber, launch.DefinitionHash, launch.Definition,
            ToDurableProfile(launch.CapabilityProfile), launch.Package);
        var host = new ConversationHostClient(clients.CreateClient("conversation-host"));
        try
        {
            // Drain/revoke any old Bob first. Host does not hand a claimed request to the new
            // attachment until this old attachment's durable detach is acknowledged.
            await session.MissionHands.RevokeAsync(ct);
            var attached = await host.AttachMissionHandsAsync(new AttachMissionHandsRequest(
                request.ConversationId, attachmentId, Guid.ParseExact(session.Id, "N"), durableLaunch), ct);
            if (attached.Status is not (MissionHandsStatus.Attached or MissionHandsStatus.AwaitingHands or MissionHandsStatus.InFlight))
            {
                await execution.DisposeAsync();
                return new AcknowledgeMissionHandsResponse(null, null, null, attached.Reason ?? "Host rejected the hands attachment.");
            }

            await session.MissionHands.ReplaceAsync(request.ConversationId, attachmentId, Guid.ParseExact(session.Id, "N"), launch.CapabilityProfile.ToString(), execution, host, ct);
            return new AcknowledgeMissionHandsResponse(attachmentId, launch.CapabilityProfile.ToString(), execution.AvailableCapabilities, null);
        }
        catch
        {
            await execution.DisposeAsync();
            throw;
        }
    }

    public async Task<DetachMissionHandsResponse> DetachAsync(ForgeMission.Application.Transport.DetachMissionHandsRequest request, CancellationToken ct)
    {
        if (!sessions.TryGet(request.SessionId, out var session) || session is null)
            return new DetachMissionHandsResponse(false, "The application session is stale or foreign.");
        return await session.MissionHands.DetachAsync(request.ConversationId, request.AttachmentId, ct)
            ? new DetachMissionHandsResponse(true, null)
            : new DetachMissionHandsResponse(false, "The hands attachment is stale or foreign.");
    }

    public async Task<GetMissionHandsStatusResponse> GetStatusAsync(GetMissionHandsStatusRequest request, CancellationToken ct)
    {
        if (!sessions.TryGet(request.SessionId, out var session) || session is null)
            return new GetMissionHandsStatusResponse(null, null, null, null, "The application session is stale or foreign.");

        var state = await session.MissionHands.GetStateAsync(request.ConversationId, request.AttachmentId, ct);
        if (state.AttachmentId is null)
            return new GetMissionHandsStatusResponse(null, null, null, null, "The hands attachment is stale or foreign.");
        var host = new ConversationHostClient(clients.CreateClient("conversation-host"));
        var work = await host.GetMissionHandsWorkAsync(new GetMissionHandsWorkRequest(
            request.ConversationId, request.AttachmentId, state.ApplicationSessionId), ct);
        return new GetMissionHandsStatusResponse(state.AttachmentId, state.Profile, state.AvailableCapabilities, work.Status, work.Reason);
    }

    public async Task<ExecuteMissionHandsResponse> ExecuteAsync(ExecuteMissionHandsRequest request, CancellationToken ct)
    {
        if (!sessions.TryGet(request.SessionId, out var session) || session is null)
            return new ExecuteMissionHandsResponse(null, null, null, null, "The application session is stale or foreign.");
        var state = await session.MissionHands.GetStateAsync(request.ConversationId, request.AttachmentId, ct);
        if (state.AttachmentId is null)
            return new ExecuteMissionHandsResponse(null, null, null, null, "The hands attachment is stale or foreign.");
        var host = new ConversationHostClient(clients.CreateClient("conversation-host"));
        var work = await host.ClaimMissionHandsWorkAsync(new ClaimMissionHandsWorkRequest(
            request.ConversationId, request.AttachmentId, state.ApplicationSessionId), ct);
        if (work.Status != MissionHandsStatus.InFlight || work.Request is null || work.Attachment?.AttachmentId != request.AttachmentId)
            return new ExecuteMissionHandsResponse(null, null, work.Reason, null,
                "There is no current Host-originated mission hands request for this attachment.");

        var capability = ToCapabilityRequest(work.Request);
        if (capability is null)
            return new ExecuteMissionHandsResponse(MissionToolOutcome.DeniedOutOfProfile, null,
                "The mission request is not a supported bounded capability operation.", null, null);
        if (policy.RuleFor(capability.Value.Name).Outcome == AuthorizationOutcome.RequiresUserConfirmation)
        {
            var awaiting = await host.BeginMissionHandsConfirmationAsync(new BeginMissionHandsConfirmationRequest(
                request.ConversationId, request.AttachmentId, work.Request.ToolRequestId), ct);
            if (awaiting.Status != MissionHandsStatus.AwaitingToolConfirmation)
                return new ExecuteMissionHandsResponse(null, null, awaiting.Reason, null, "Host rejected mission hands confirmation.");
        }

        var execution = await session.MissionHands.ExecuteAsync(work.Request.ConversationId, request.AttachmentId,
            capability.Value.Name, capability.Value.Request, ct);
        if (!execution.Attached)
            return new ExecuteMissionHandsResponse(null, null, null, null, "The hands attachment is stale or foreign.");

        var outcome = OutcomeFor(execution.Result!, capability.Value.Name, execution.AvailableCapabilities);
        var accepted = await host.SubmitMissionHandsResultAsync(new SubmitMissionToolResultRequest(
            work.Request.ConversationId, request.AttachmentId, work.Request.TurnAttemptId,
            ConversationDeterministicIds.ClientToolResult(work.Request.ToolRequestId), work.Request.ToolRequestId,
            outcome, outcome == MissionToolOutcome.Succeeded ? execution.Result!.Content : null,
            outcome == MissionToolOutcome.Succeeded ? null : execution.Result!.Content), ct);
        return new ExecuteMissionHandsResponse(outcome, execution.Result!.Content,
            accepted.Reason, accepted.AcceptedSequence, null);
    }

    public async Task<CancelMissionHandsResponse> CancelAsync(CancelMissionHandsRequest request, CancellationToken ct)
    {
        if (!sessions.TryGet(request.SessionId, out var session) || session is null)
            return new CancelMissionHandsResponse(false, null, "The application session is stale or foreign.");
        var state = await session.MissionHands.GetStateAsync(request.ConversationId, request.AttachmentId, ct);
        if (state.AttachmentId is null)
            return new CancelMissionHandsResponse(false, null, "The hands attachment is stale or foreign.");
        var host = new ConversationHostClient(clients.CreateClient("conversation-host"));
        var work = await host.GetMissionHandsWorkAsync(new GetMissionHandsWorkRequest(request.ConversationId, request.AttachmentId, state.ApplicationSessionId), ct);
        if (work.Request is null)
            return new CancelMissionHandsResponse(false, null, "There is no current Host-originated mission hands request to cancel.");
        var cancelled = await host.CancelMissionHandsAttemptAsync(new CancelMissionHandsAttemptRequest(
            request.ConversationId, request.AttachmentId, work.Request.TurnAttemptId, work.Request.ToolRequestId, request.Reason), ct);
        return cancelled.Status == MissionHandsStatus.Cancelled
            ? await CancelLocalAsync(session, request, cancelled.AcceptedSequence, ct)
            : new CancelMissionHandsResponse(false, null, cancelled.Reason);
    }

    public async Task<RecoverMissionHandsResponse> RecoverAsync(RecoverMissionHandsRequest request, CancellationToken ct)
    {
        if (!sessions.TryGet(request.SessionId, out var session) || session is null)
            return new RecoverMissionHandsResponse(null, null, "The application session is stale or foreign.");
        var state = await session.MissionHands.GetStateAsync(request.ConversationId, request.AttachmentId, ct);
        if (state.AttachmentId is null)
            return new RecoverMissionHandsResponse(null, null, "The hands attachment is stale or foreign.");

        var host = new ConversationHostClient(clients.CreateClient("conversation-host"));
        var recovered = await host.RecoverMissionHandsInFlightAsync(new RecoverMissionHandsInFlightRequest(
            request.ConversationId, request.AttachmentId, state.ApplicationSessionId), ct);
        return recovered.Status == MissionHandsStatus.Interrupted
            ? new RecoverMissionHandsResponse(recovered.Status, recovered.AcceptedSequence, null)
            : new RecoverMissionHandsResponse(recovered.Status, recovered.AcceptedSequence, recovered.Reason ??
                "The Host rejected mission hands crash recovery.");
    }

    private static async Task<CancelMissionHandsResponse> CancelLocalAsync(ApplicationSession session,
        CancelMissionHandsRequest request, long? sequence, CancellationToken ct)
    {
        await session.MissionHands.CancelAsync(request.ConversationId, request.AttachmentId, ct);
        return new CancelMissionHandsResponse(true, sequence, null);
    }

    internal static (string Name, ICapabilityRequest Request)? ToCapabilityRequest(MissionToolRequest request)
    {
        var args = request.Arguments;
        return request.ToolName switch
        {
            "Read" when StringValue(args, "file_path") is { } path => ("file", new ReadFileCapabilityRequest(path, IntValue(args, "offset") ?? 0, IntValue(args, "limit"))),
            "Write" when StringValue(args, "file_path") is { } path && StringValue(args, "content") is { } content =>
                ("file", new WriteFileCapabilityRequest(path, content)),
            "Edit" when StringValue(args, "file_path") is { } path && StringValue(args, "old_string") is { } oldString && StringValue(args, "new_string") is { } newString =>
                ("file", new EditFileCapabilityRequest(path, oldString, newString, BoolValue(args, "replace_all"))),
            "Bash" when StringValue(args, "command") is { } command => ("terminal", new ExecuteTerminalCapabilityRequest(command)),
            _ => null,
        };
    }

    private static string? StringValue(System.Text.Json.JsonElement value, string name) =>
        value.ValueKind == System.Text.Json.JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == System.Text.Json.JsonValueKind.String
            ? property.GetString() : null;
    private static int? IntValue(System.Text.Json.JsonElement value, string name) =>
        value.ValueKind == System.Text.Json.JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.TryGetInt32(out var integer)
            ? integer : null;
    private static bool BoolValue(System.Text.Json.JsonElement value, string name) =>
        value.ValueKind == System.Text.Json.JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False
            && property.GetBoolean();

    private static MissionToolOutcome OutcomeFor(ToolExecutionResult result, string capability, IReadOnlyList<string> available) =>
        !result.IsError ? MissionToolOutcome.Succeeded :
        !available.Contains(capability, StringComparer.Ordinal) ? MissionToolOutcome.DeniedOutOfProfile :
        result.Content.Contains("denied by policy", StringComparison.OrdinalIgnoreCase) ? MissionToolOutcome.DeniedByPolicy :
        result.Content.Contains("denied by the user", StringComparison.OrdinalIgnoreCase) ? MissionToolOutcome.DeniedByOperator :
        result.Content.Contains("cancel", StringComparison.OrdinalIgnoreCase) ? MissionToolOutcome.Cancelled : MissionToolOutcome.Failed;

    private static MissionExecutionProfile ToExecutionProfile(MissionCapabilityProfile profile) => profile switch
    {
        MissionCapabilityProfile.NoHands => MissionExecutionProfile.NoHands,
        MissionCapabilityProfile.ProjectWorkspace => MissionExecutionProfile.ProjectWorkspace,
        MissionCapabilityProfile.ProjectWorkspaceAndTerminal => MissionExecutionProfile.ProjectWorkspaceAndTerminal,
        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    };

    private static MissionHandsProfile ToDurableProfile(MissionCapabilityProfile profile) => profile switch
    {
        MissionCapabilityProfile.NoHands => MissionHandsProfile.NoHands,
        MissionCapabilityProfile.ProjectWorkspace => MissionHandsProfile.ProjectWorkspace,
        MissionCapabilityProfile.ProjectWorkspaceAndTerminal => MissionHandsProfile.ProjectWorkspaceAndTerminal,
        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    };
}
