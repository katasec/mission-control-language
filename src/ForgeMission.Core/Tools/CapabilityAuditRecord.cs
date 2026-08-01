namespace ForgeMission.Core.Tools;

public sealed record CapabilityAuditRecord(
    string CapabilityName,
    string RequestSummary,
    AuthorizationOutcome Outcome,
    DateTimeOffset Timestamp,
    bool? UserConfirmed,
    bool ProviderInvoked,
    bool Succeeded);
