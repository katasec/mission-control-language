namespace ForgeMission.Core.Tools;

// Administrator-controlled is a policy source, not an additional runtime outcome. An
// administrator-supplied rule still resolves to one of these three decisions.
public enum AuthorizationOutcome
{
    AutoApproved,
    AutoDenied,
    RequiresUserConfirmation,
}
