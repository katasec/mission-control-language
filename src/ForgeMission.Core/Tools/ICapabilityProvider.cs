namespace ForgeMission.Core.Tools;

// Minimal common shape for Client Runtime capability providers. The registry uses this stable
// name for its manifest; providers remain focused on execution, not authorization.
public interface ICapabilityProvider
{
    string CapabilityName { get; }
}
