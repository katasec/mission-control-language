namespace ForgeMission.Core.Tools;

// The sole path from Mission Runtime tool requests to capability providers.
public interface ICapabilityDispatcher
{
    Task<ToolExecutionResult> DispatchAsync(
        string capabilityName,
        object request,
        CancellationToken ct);
}
