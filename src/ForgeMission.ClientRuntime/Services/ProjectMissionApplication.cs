using System.Net;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.ClientRuntime.TransportHost;
using ForgeMission.Conversations.Contracts;
using HostStartProjectMissionRunRequest = ForgeMission.Conversations.Contracts.StartProjectMissionRunRequest;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>
/// Coordinates one immutable Project Mission submission. The ProjectStore owns file transactions;
/// ConversationHostClient owns HTTP. This class deliberately releases the manifest lease before
/// every Host call, and has no capability, provider, or presentation dependency.
/// </summary>
internal sealed class ProjectMissionApplication(ProjectStore projects, IHttpClientFactory clients)
{
    public async Task<ProjectSubmissionResponse> StartAsync(
        ClientRuntimeSession session, ForgeMission.ClientRuntime.Transport.StartProjectMissionRunRequest request, CancellationToken ct)
    {
        ProjectRecord before;
        try { before = projects.ReadForHome(session.Workspace.Root!); }
        catch (ProjectOperationException exception) { return new ProjectSubmissionResponse(null, ToError(exception)); }
        var busy = await ActiveRunErrorAsync(before.Manifest, ct);
        if (busy is not null)
            return new ProjectSubmissionResponse(null, busy);

        ProjectRecord prepared;
        try
        {
            prepared = await projects.PrepareSubmissionAsync(
                session.Workspace.Root!, request.CommandId, request.PreviousCommandId, request.Input, ct);
        }
        catch (ProjectOperationException exception)
        {
            return new ProjectSubmissionResponse(null, ToError(exception));
        }

        return await DispatchAsync(session.Workspace.Root!, prepared, retry: false, ct);
    }

    public async Task<ProjectSubmissionResponse> RetryAsync(
        ClientRuntimeSession session, RetryProjectMissionSubmissionRequest request, CancellationToken ct)
    {
        ProjectRecord current;
        try { current = projects.ReadForHome(session.Workspace.Root!); }
        catch (ProjectOperationException exception) { return new ProjectSubmissionResponse(null, ToError(exception)); }
        if (current.Manifest.Submission is not { } submission || submission.CommandId != request.CommandId)
            return new ProjectSubmissionResponse(null, Error(ProjectOperationErrorCode.SubmissionChanged,
                "The Project submission changed. Refresh before retrying."));

        if (submission.Phase != ProjectSubmissionPhase.Prepared)
            return new ProjectSubmissionResponse(ToView(submission), null);

        return await DispatchAsync(session.Workspace.Root!, current, retry: true, ct);
    }

    private async Task<ProjectSubmissionResponse> DispatchAsync(
        string home, ProjectRecord prepared, bool retry, CancellationToken ct)
    {
        var submission = prepared.Manifest.Submission!;
        var host = new ConversationHostClient(clients.CreateClient("conversation-host"));
        try
        {
            var containerId = await EnsureContainerAsync(home, prepared.Manifest, host, ct);
            if (retry)
            {
                var receipt = await FindReceiptAsync(host, containerId, submission.CommandId, ct);
                if (receipt is not null)
                    return await CommitReceiptAsync(home, submission, receipt, ct);
            }

            var accepted = await host.StartProjectMissionRunAsync(new HostStartProjectMissionRunRequest(
                containerId, submission.CommandId, submission.Mission, submission.Input), ct);
            if (accepted.ContainerId != containerId || accepted.RunId == Guid.Empty || accepted.AcceptedSequence <= 0)
                return Uncertain(submission);

            return await CommitReceiptAsync(home, submission, new ProjectCommandReceipt(
                accepted.ContainerId, accepted.RunId, submission.Mission, submission.Input, submission.ProjectGoal,
                accepted.AcceptedSequence, accepted.Status), ct);
        }
        catch (ConversationHostProjectException exception) when (IsDefinitive(exception.StatusCode))
        {
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
            return new ProjectSubmissionResponse(ToView(submission), ToError(exception));
        }
        catch (Exception exception) when (IsUncertain(exception))
        {
            return Uncertain(submission);
        }
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
