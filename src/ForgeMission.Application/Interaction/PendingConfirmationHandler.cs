using System.Collections.Concurrent;
using ForgeMission.Application.Transport;
using ForgeMission.Core.Tools;

namespace ForgeMission.Application;

internal sealed class PendingConfirmationHandler(string sessionId, Action<ApplicationEvent> publish) : ICapabilityConfirmationHandler
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pending = [];

    internal bool HasPending => !_pending.IsEmpty;

    public async Task<bool> ConfirmAsync(CapabilityConfirmationRequest request, CancellationToken ct)
    {
        var confirmationId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(confirmationId, completion))
            throw new InvalidOperationException("Unable to register capability confirmation.");

        publish(new ApplicationEvent(
            ApplicationEventKind.ConfirmationRequest,
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
