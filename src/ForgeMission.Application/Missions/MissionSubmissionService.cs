using System.Net;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using HostStartProjectMissionRunRequest = ForgeMission.Conversations.Contracts.StartProjectMissionRunRequest;

namespace ForgeMission.Application;

/// <summary>
/// Coordinates one immutable Project Mission submission. The ProjectService owns file transactions;
/// ConversationHostClient owns HTTP. This class deliberately releases the manifest lease before
/// every Host call, and has no capability, provider, or presentation dependency.
/// </summary>
internal sealed class MissionSubmissionService(
    ProjectService projects,
    IHttpClientFactory clients,
    ApplicationSessionService? sessions = null,
    MissionHandsConversationService? missionHands = null) : IMissionSubmissionService
{
    public Task<ProjectSubmissionResponse> StartAsync(
        ForgeMission.Application.Transport.StartProjectMissionRunRequest request,
        CancellationToken ct) => StartAsync(RequiredSession(request.SessionId), request, ct);

    public Task<ProjectSubmissionResponse> RetryAsync(
        RetryProjectMissionSubmissionRequest request,
        CancellationToken ct) => RetryAsync(RequiredSession(request.SessionId), request, ct);

    public async Task<ProjectSubmissionResponse> StartAsync(
        ApplicationSession session, ForgeMission.Application.Transport.StartProjectMissionRunRequest request, CancellationToken ct)
    {
        ProjectRecord before;
        try { before = projects.ReadForHome(session.ProjectHome); }
        catch (ProjectOperationException exception) { return new ProjectSubmissionResponse(null, ToError(exception)); }
        var busy = await ActiveRunErrorAsync(before.Manifest, ct);
        if (busy is not null)
            return new ProjectSubmissionResponse(null, busy);

        // A generic launch is only eligible after the caller explicitly acknowledges the
        // exact profile already approved in the manifest. The caller still cannot provide or
        // alter a package/profile: DispatchAsync re-resolves that immutable launch below.
        var genericLaunches = before.Manifest.ApprovedMissionLaunches?.Where(candidate => candidate.Package is not null).ToArray() ?? [];
        if (genericLaunches.Length > 1)
            return new ProjectSubmissionResponse(null, Error(ProjectOperationErrorCode.MissionRunConflict,
                "Exactly one approved generic mission launch must be active before starting."));
        if (genericLaunches.Length == 1 && !request.ProfileAccepted)
            return new ProjectSubmissionResponse(null, Error(ProjectOperationErrorCode.MissionRunConflict,
                "The exact approved mission capability profile must be acknowledged before starting."));

        ProjectRecord prepared;
        try
        {
            prepared = await projects.PrepareSubmissionAsync(
                session.ProjectHome, request.CommandId, request.PreviousCommandId, request.Input, ct);
        }
        catch (ProjectOperationException exception)
        {
            return new ProjectSubmissionResponse(null, ToError(exception));
        }

        return await DispatchAsync(session, prepared, retry: false, ct);
    }

    public async Task<ProjectSubmissionResponse> RetryAsync(
        ApplicationSession session, RetryProjectMissionSubmissionRequest request, CancellationToken ct)
    {
        ProjectRecord current;
        try { current = projects.ReadForHome(session.ProjectHome); }
        catch (ProjectOperationException exception) { return new ProjectSubmissionResponse(null, ToError(exception)); }
        if (current.Manifest.Submission is not { } submission || submission.CommandId != request.CommandId)
            return new ProjectSubmissionResponse(null, Error(ProjectOperationErrorCode.SubmissionChanged,
                "The Project submission changed. Refresh before retrying."));

        if (submission.Phase != ProjectSubmissionPhase.Prepared)
            return new ProjectSubmissionResponse(ToView(submission), null);

        return await DispatchAsync(session, current, retry: true, ct);
    }

    private ApplicationSession RequiredSession(string sessionId)
    {
        if (sessions is null || !sessions.TryGet(sessionId, out var session) || session is null)
            throw new KeyNotFoundException();
        return session;
    }

    private async Task<ProjectSubmissionResponse> DispatchAsync(
        ApplicationSession session, ProjectRecord prepared, bool retry, CancellationToken ct)
    {
        var home = session.ProjectHome;
        var submission = prepared.Manifest.Submission!;
        var host = new ConversationHostClient(clients.CreateClient("conversation-host"));
        var genericAttachmentCreated = false;
        try
        {
            var containerId = await EnsureContainerAsync(home, prepared.Manifest, host, ct);
            if (retry)
            {
                var receipt = await FindReceiptAsync(host, containerId, submission.CommandId, ct);
                if (receipt is not null)
                    return await CommitReceiptAsync(home, submission, receipt, ct);
            }

            // The caller never supplies a package/profile. Application re-resolves the approved
            // local launch from the manifest and can therefore send only that immutable value.
            var launches = prepared.Manifest.ApprovedMissionLaunches?.Where(candidate => candidate.Package is not null).ToArray() ?? [];
            if (launches.Length > 1)
                return new ProjectSubmissionResponse(ToView(submission), Error(ProjectOperationErrorCode.MissionRunConflict,
                    "Exactly one approved generic mission launch must be active before starting."));
            var launch = launches.SingleOrDefault();
            var durableLaunch = launch is null ? null : new DurableMissionLaunch(launch.MissionVersionId,
                launch.VersionNumber, launch.DefinitionHash, launch.Definition, ToDurableProfile(launch.CapabilityProfile), launch.Package);
            if (launch is not null)
            {
                if (missionHands is null)
                    return new ProjectSubmissionResponse(ToView(submission), Error(ProjectOperationErrorCode.MissionRunConflict,
                        "Generic mission hands are not composed in this Application."));
                var attachmentError = await missionHands.AttachApprovedForRunAsync(session, containerId, launch, ct);
                if (attachmentError is not null)
                    return new ProjectSubmissionResponse(ToView(submission), Error(ProjectOperationErrorCode.MissionRunConflict, attachmentError));
                genericAttachmentCreated = true;
            }
            var accepted = await host.StartProjectMissionRunAsync(new HostStartProjectMissionRunRequest(
                containerId, submission.CommandId, durableLaunch is null ? submission.Mission : "Durable", submission.Input, durableLaunch), ct);
            if (accepted.ContainerId != containerId || accepted.RunId == Guid.Empty || accepted.AcceptedSequence <= 0)
            {
                await RevokeProvisionalGenericHandsAsync(session, genericAttachmentCreated);
                return Uncertain(submission);
            }

            return await CommitReceiptAsync(home, submission, new ProjectCommandReceipt(
                accepted.ContainerId, accepted.RunId, submission.Mission, submission.Input, submission.ProjectGoal,
                accepted.AcceptedSequence, accepted.Status), ct);
        }
        catch (ConversationHostProjectException exception) when (IsDefinitive(exception.StatusCode))
        {
            await RevokeProvisionalGenericHandsAsync(session, genericAttachmentCreated);
            var rejection = new ProjectSubmissionRejection(exception.Error.Code, SafeHostMessage(exception.Error.Code));
            try
            {
                var rejected = await projects.RecordSubmissionRejectedAsync(home, submission.CommandId, rejection, ct);
                return new ProjectSubmissionResponse(ToView(rejected.Manifest.Submission!), null);
            }
            catch (ProjectOperationException failure)
            {
                return new ProjectSubmissionResponse(ToView(submission), ToError(failure));
            }
        }
        catch (ProjectOperationException exception)
        {
            await RevokeProvisionalGenericHandsAsync(session, genericAttachmentCreated);
            return new ProjectSubmissionResponse(ToView(submission), ToError(exception));
        }
        catch (Exception exception) when (IsUncertain(exception))
        {
            await RevokeProvisionalGenericHandsAsync(session, genericAttachmentCreated);
            return Uncertain(submission);
        }
    }

    private static async Task RevokeProvisionalGenericHandsAsync(ApplicationSession session, bool created)
    {
        if (!created) return;
        try { await session.MissionHands.RevokeAsync(CancellationToken.None); }
        // Host uncertainty must never leave a locally authoritative Bob live. A later explicit
        // retry creates a fresh attachment; cleanup failure cannot rewrite the original result.
        catch { }
    }

    private async Task<ProjectSubmissionResponse> CommitReceiptAsync(
        string home, ProjectSubmission expected, ProjectCommandReceipt receipt, CancellationToken ct)
    {
        if (receipt.ContainerId == Guid.Empty || receipt.RunId == Guid.Empty || receipt.AcceptedSequence <= 0 ||
            !string.Equals(receipt.Mission, expected.Mission, StringComparison.Ordinal) ||
            !string.Equals(receipt.Input, expected.Input, StringComparison.Ordinal) ||
            !string.Equals(receipt.ProjectGoal, expected.ProjectGoal, StringComparison.Ordinal))
            return new ProjectSubmissionResponse(ToView(expected), Error(ProjectOperationErrorCode.MissionRunConflict,
                "The Project Mission receipt did not match the prepared command."));

        try
        {
            var written = await projects.RecordSubmissionAcceptedAsync(home, expected.CommandId,
                new ProjectSubmissionAcceptance(receipt.ContainerId, receipt.RunId, receipt.AcceptedSequence, receipt.Status), ct);
            return new ProjectSubmissionResponse(ToView(written.Manifest.Submission!), null);
        }
        catch (ProjectOperationException exception)
        {
            return new ProjectSubmissionResponse(ToView(expected), ToError(exception));
        }
    }

    private async Task<Guid> EnsureContainerAsync(
        string home, ProjectManifest manifest, ConversationHostClient host, CancellationToken ct)
    {
        var create = await host.CreateProjectMissionContainerAsync(new CreateProjectMissionContainerRequest(
            manifest.ProjectId, ConversationDeterministicIds.ProjectMissionContainerCreate(manifest.ProjectId), manifest.Goal), ct);
        if (create.ContainerId == Guid.Empty)
            throw new ConversationHostProtocolException("ConversationHost returned an invalid Project Mission container.");

        var stored = await projects.SetProjectMissionContainerIdAsync(home, create.ContainerId, ct);
        var snapshot = (await host.ReadConversationAsync(stored.Manifest.ProjectMissionContainerId!.Value, ct)).Snapshot;
        if (snapshot.Purpose != ConversationPurpose.ProjectMission || snapshot.ProjectId != stored.Manifest.ProjectId)
            throw new ProjectOperationException(ProjectOperationErrorCode.MissionRunConflict,
                "The Project Mission container belongs to another Project.");
        return snapshot.ConversationId;
    }

    private async Task<ProjectOperationError?> ActiveRunErrorAsync(ProjectManifest manifest, CancellationToken ct)
    {
        if (manifest.ProjectMissionContainerId is not { } containerId)
            return null;

        try
        {
            var host = new ConversationHostClient(clients.CreateClient("conversation-host"));
            var snapshot = (await host.ReadConversationAsync(containerId, ct)).Snapshot;
            if (snapshot.Purpose != ConversationPurpose.ProjectMission || snapshot.ProjectId != manifest.ProjectId)
                return Error(ProjectOperationErrorCode.MissionRunConflict, "The Project Mission container belongs to another Project.");
            return snapshot.ActiveRunId is null ? null : Error(ProjectOperationErrorCode.RunAlreadyActive,
                "This Project already has an active Mission run.");
        }
        catch (Exception exception) when (IsUncertain(exception))
        {
            return Error(ProjectOperationErrorCode.HistoryUnavailable,
                "Forge could not verify whether this Project has an active run.");
        }
    }

    private static async Task<ProjectCommandReceipt?> FindReceiptAsync(
        ConversationHostClient host, Guid containerId, Guid commandId, CancellationToken ct)
    {
        try { return await host.ReadProjectCommandAsync(containerId, commandId, ct); }
        catch (ConversationHostProjectException exception) when (exception.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    private static ProjectSubmissionResponse Uncertain(ProjectSubmission submission) =>
        new(ToView(submission), Error(ProjectOperationErrorCode.SubmissionUncertain,
            "Forge could not confirm the Project Mission result. Retry the same command."));

    private static MissionHandsProfile ToDurableProfile(MissionCapabilityProfile profile) => profile switch
    {
        MissionCapabilityProfile.NoHands => MissionHandsProfile.NoHands,
        MissionCapabilityProfile.ProjectWorkspace => MissionHandsProfile.ProjectWorkspace,
        MissionCapabilityProfile.ProjectWorkspaceAndTerminal => MissionHandsProfile.ProjectWorkspaceAndTerminal,
        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    };

    internal static ProjectSubmissionView ToView(ProjectSubmission submission) => submission.Phase switch
    {
        ProjectSubmissionPhase.Prepared => new(submission.CommandId, submission.Mission, submission.Input,
            ProjectSubmissionState.Prepared, null, null, null),
        ProjectSubmissionPhase.Accepted when submission.Acceptance is { } acceptance => new(
            submission.CommandId, submission.Mission, submission.Input, ProjectSubmissionState.Accepted,
            acceptance.RunId, acceptance.AcceptedSequence, null),
        ProjectSubmissionPhase.Rejected when submission.Rejection is { } rejection => new(
            submission.CommandId, submission.Mission, submission.Input, ProjectSubmissionState.Rejected,
            null, null, Error(RejectionCode(rejection.Code), rejection.Message)),
        _ => throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
            "The Project Mission submission record is incomplete."),
    };

    internal static ProjectOperationError ToError(ProjectOperationException exception) => Error(exception.Code, exception.Message);
    internal static ProjectOperationError Error(ProjectOperationErrorCode code, string message) => new(code, message);
    private static ProjectOperationErrorCode RejectionCode(string code) => code switch
    {
        "invalidRequest" => ProjectOperationErrorCode.InvalidMissionInput,
        "unknownMission" => ProjectOperationErrorCode.UnknownMission,
        "runAlreadyActive" => ProjectOperationErrorCode.RunAlreadyActive,
        "notFound" => ProjectOperationErrorCode.MissionRunNotFound,
        _ => ProjectOperationErrorCode.MissionRunConflict,
    };
    private static bool IsDefinitive(HttpStatusCode? status) => status is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict;
    private static bool IsUncertain(Exception exception) => exception is HttpRequestException or ConversationHostProtocolException or TaskCanceledException;
    private static string SafeHostMessage(string code) => code switch
    {
        "invalidRequest" => "The Project Mission instruction was rejected.",
        "unknownMission" => "The Project Mission is not available.",
        "runAlreadyActive" => "This Project already has an active Mission run.",
        "notFound" => "The Project Mission resource could not be found.",
        _ => "The Project Mission command conflicts with durable state.",
    };
}
