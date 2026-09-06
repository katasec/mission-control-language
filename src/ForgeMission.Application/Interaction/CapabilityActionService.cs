using ForgeMission.Application.Transport;
using ForgeMission.Core.Tools;

namespace ForgeMission.Application;

/// <summary>Maps the shared transport request to Bob's existing typed capability boundary.
/// Session lookup never approves an operation; Bob applies policy and confirmation.</summary>
internal sealed class CapabilityActionService(
    ApplicationSessionService sessions,
    Action<ApplicationEvent> publish) : ICapabilityActionService
{
    public async Task<CapabilityDispatchResponse> DispatchAsync(
        CapabilityDispatchRequest request,
        CancellationToken ct)
    {
        if (!sessions.TryGet(request.SessionId, out var session) || session is null)
            throw new KeyNotFoundException();

        var capability = ToCapabilityRequest(request.Request)
            ?? throw new ArgumentException("Unsupported capability request.");
        var target = request.Request.FilePath ?? request.Request.Command;
        PublishStatus(request.SessionId, request.Request, "running", target);
        var result = await session.Execution.DispatchAsync(request.Request.CapabilityName, capability, ct);
        PublishStatus(request.SessionId, request.Request, result.IsError ? "error" : "done", target);
        return new CapabilityDispatchResponse(result.Content, result.IsError);
    }

    private void PublishStatus(string sessionId, CapabilityRequestData request, string status, string? target) =>
        publish(new ApplicationEvent(
            ApplicationEventKind.ToolCallStatus,
            sessionId,
            ToolName: request.Operation.ToString(),
            ToolStatus: status,
            ToolTarget: target));

    private static ICapabilityRequest? ToCapabilityRequest(CapabilityRequestData request) =>
        request.Operation switch
        {
            CapabilityOperation.ReadFile when request.FilePath is not null =>
                new ReadFileCapabilityRequest(request.FilePath, request.Offset, request.Limit),
            CapabilityOperation.EditFile when request.FilePath is not null &&
                                                   request.OldString is not null &&
                                                   request.NewString is not null =>
                new EditFileCapabilityRequest(
                    request.FilePath,
                    request.OldString,
                    request.NewString,
                    request.ReplaceAll),
            CapabilityOperation.WriteFile when request.FilePath is not null && request.Content is not null =>
                new WriteFileCapabilityRequest(request.FilePath, request.Content),
            CapabilityOperation.ExecuteTerminal when request.Command is not null =>
                new ExecuteTerminalCapabilityRequest(request.Command),
            _ => null,
        };
}
