using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Runtime;
using System.Security.Cryptography;
using System.Text;

namespace ForgeMission.ConversationHost.Grains;

/// <summary>Host admission maps the wire value to Core's one package parser/validator. Worker
/// performs the same validation independently before it can construct a provider request.</summary>
internal static class DurableMissionPackageAdmission
{
    public static bool TryValidate(DurableMissionLaunch? launch, out string? reason)
    {
        reason = null;
        if (launch?.Package is not { } package || launch.MissionVersionId == Guid.Empty || launch.VersionNumber <= 0 || !Enum.IsDefined(launch.Profile))
        { reason = "A generic durable run requires an approved immutable launch package."; return false; }
        if (string.IsNullOrWhiteSpace(launch.Definition))
        { reason = "A generic durable launch definition is required."; return false; }
        var definitionHash = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(launch.Definition))).ToLowerInvariant();
        if (launch.Definition.Length > 1024 || !string.Equals(launch.DefinitionHash, definitionHash, StringComparison.OrdinalIgnoreCase))
        { reason = "A generic durable launch definition is invalid or exceeds its checkpoint budget."; return false; }
        return DurableMissionPackageValidator.TryValidate(new DurableMissionPackageInput(
            package.FormatVersion, package.PackageHash, package.MissionSource, package.RootMissionName, package.RootInputName,
            package.ResolvedExperts.Select(expert => new DurableResolvedExpertInput(expert.Name, expert.LockSource,
                expert.LockPath, expert.LockHash, expert.ExpertMarkdown)).ToArray()), out _, out reason);
    }
}
