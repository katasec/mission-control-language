namespace ForgeMission.Core.Tools;

// Presentation implements this contract in a later phase. The dispatcher only needs a yes/no
// answer for one already-policy-evaluated request; it never grants implicit session-wide consent.
public interface ICapabilityConfirmationHandler
{
    Task<bool> ConfirmAsync(CapabilityConfirmationRequest request, CancellationToken ct);
}

public sealed record CapabilityConfirmationRequest(string CapabilityName, string RequestSummary);
