using System.Collections.Concurrent;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Core.Tools;

namespace ForgeMission.ClientRuntime.TransportHost;

internal sealed class PendingConfirmationHandler(string sessionId, ClientRuntimeEventHub events) : ICapabilityConfirmationHandler
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pending = [];

    public async Task<bool> ConfirmAsync(CapabilityConfirmationRequest request, CancellationToken ct)
    {
        var confirmationId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(confirmationId, completion))
            throw new InvalidOperationException("Unable to register capability confirmation.");

        events.Publish(new ClientRuntimeEvent(
            ClientRuntimeEventKind.ConfirmationRequest,
            sessionId,
            ConfirmationId: confirmationId,
            CapabilityName: request.CapabilityName,
            RequestSummary: request.RequestSummary));

        using var registration = ct.Register(() => completion.TrySetCanceled(ct));
        try
        {
            return await completion.Task;
        }
        finally
        {
            _pending.TryRemove(confirmationId, out _);
        }
    }

    public bool Resolve(string confirmationId, bool approved) =>
        _pending.TryGetValue(confirmationId, out var completion) && completion.TrySetResult(approved);
}
