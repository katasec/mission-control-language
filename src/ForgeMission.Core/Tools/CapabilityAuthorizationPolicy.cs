namespace ForgeMission.Core.Tools;

public enum CapabilityPolicySource
{
    Local,
    Administrator,
}

public sealed record CapabilityAuthorizationRule(
    AuthorizationOutcome Outcome,
    CapabilityPolicySource Source = CapabilityPolicySource.Local);

// Local v1 policy configuration. Distribution of administrator-owned rules is deliberately
// outside this layer; Source merely preserves where a resolved rule came from.
public sealed class CapabilityAuthorizationPolicy
{
    private readonly IReadOnlyDictionary<string, CapabilityAuthorizationRule> _rules;

    public CapabilityAuthorizationPolicy(
        IEnumerable<KeyValuePair<string, CapabilityAuthorizationRule>> rules,
        CapabilityAuthorizationRule? defaultRule = null)
    {
        _rules = rules.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        DefaultRule = defaultRule ?? new CapabilityAuthorizationRule(AuthorizationOutcome.AutoDenied);
    }

    public IReadOnlyDictionary<string, CapabilityAuthorizationRule> Rules => _rules;

    public CapabilityAuthorizationRule DefaultRule { get; }

    public CapabilityAuthorizationRule RuleFor(string capabilityName) =>
        _rules.GetValueOrDefault(capabilityName, DefaultRule);

    public static CapabilityAuthorizationPolicy Default { get; } = new(
    [
        new KeyValuePair<string, CapabilityAuthorizationRule>(
            "file", new CapabilityAuthorizationRule(AuthorizationOutcome.AutoApproved)),
        new KeyValuePair<string, CapabilityAuthorizationRule>(
            "terminal", new CapabilityAuthorizationRule(AuthorizationOutcome.AutoApproved)),
    ]);
}

public sealed class PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy policy) : ICapabilityAuthorizer
{
    public Task<AuthorizationOutcome> AuthorizeAsync(
        string capabilityName,
        object request,
        CancellationToken ct) =>
        Task.FromResult(policy.RuleFor(capabilityName).Outcome);
}
