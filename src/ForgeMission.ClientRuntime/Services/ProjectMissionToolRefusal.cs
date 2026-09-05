using System.Net;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Services;

/// <summary>
/// The only Project Mission tool path. It never receives a workspace, capability registry, or
/// dispatcher, so a hostile ToolRequested event can only produce the stable refusal result.
/// </summary>
internal sealed class ProjectMissionToolRefusal(ConversationHostClient host)
{
    public async Task ApplyAsync(ConversationEvent evt, CancellationToken ct)
    {
        if (evt.Kind != ConversationEventKind.ToolRequested || evt.ToolRequest is not { } request)
            return;

        try
        {
            await host.SubmitToolResultAsync(new SubmitToolResultRequest(
                evt.ConversationId,
                ConversationDeterministicIds.ClientToolResult(request.RequestId),
                request.RequestId,
                "Project Mission runs cannot execute local tools.",
                IsError: true), ct);
        }
        catch (ConversationHostProjectException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            // A retry may race the Host's idempotent result write. If the run no longer expects
            // this tool request, durable state proves the refusal was resolved; otherwise throw
            // so ConversationTailReader keeps its cursor behind this event and retries.
            var snapshot = (await host.ReadConversationAsync(evt.ConversationId, ct)).Snapshot;
            if (snapshot.ExpectedToolRequestId == request.RequestId)
                throw;
        }
    }
}
