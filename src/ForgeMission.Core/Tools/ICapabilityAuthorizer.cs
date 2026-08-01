namespace ForgeMission.Core.Tools;

public interface ICapabilityAuthorizer
{
    Task<AuthorizationOutcome> AuthorizeAsync(
        string capabilityName,
        object request,
        CancellationToken ct);
}
