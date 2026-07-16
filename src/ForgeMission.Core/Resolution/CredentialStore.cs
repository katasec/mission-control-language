using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeMission.Core.Resolution;

public class ForgeCredentials
{
    public Dictionary<string, RegistryCredential> Credentials { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Platform sign-in (42.5): the forge platform key + who/where it came from.</summary>
    public PlatformCredential? Platform { get; set; }
}

public class RegistryCredential
{
    public string Token { get; set; } = "";
}

public class PlatformCredential
{
    /// <summary>The platform key (fg_live_…) — the bearer token for hosted forge endpoints.</summary>
    public string Key { get; set; } = "";

    /// <summary>Display label of the signed-in user (email), informational only.</summary>
    public string User { get; set; } = "";

    /// <summary>Base URL of the platform that issued the key (e.g. https://forge.katasec.com).</summary>
    public string Endpoint { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ForgeCredentials))]
[JsonSerializable(typeof(RegistryCredential))]
[JsonSerializable(typeof(PlatformCredential))]
internal partial class CredentialsJsonContext : JsonSerializerContext { }

public static class CredentialStore
{
    private static string CredentialsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".forge", "credentials.json");

    public static string? GetToken(string registry)
    {
        // Env var takes precedence for simple CI setups
        var envToken = Environment.GetEnvironmentVariable("FORGE_REGISTRY_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken)) return envToken;

        if (!File.Exists(CredentialsPath)) return null;

        try
        {
            var json = File.ReadAllText(CredentialsPath);
            var creds = JsonSerializer.Deserialize(json, CredentialsJsonContext.Default.ForgeCredentials);
            if (creds?.Credentials.TryGetValue(registry, out var cred) == true)
                return string.IsNullOrWhiteSpace(cred.Token) ? null : cred.Token;
        }
        catch { /* corrupt credentials file — treat as missing */ }

        return null;
    }

    public static void SaveToken(string registry, string token) =>
        Mutate(creds => creds.Credentials[registry] = new RegistryCredential { Token = token });

    // --- Platform credential (42.5) --------------------------------------------------------

    public static PlatformCredential? GetPlatform()
    {
        if (!File.Exists(CredentialsPath)) return null;

        try
        {
            var json = File.ReadAllText(CredentialsPath);
            var creds = JsonSerializer.Deserialize(json, CredentialsJsonContext.Default.ForgeCredentials);
            return string.IsNullOrWhiteSpace(creds?.Platform?.Key) ? null : creds!.Platform;
        }
        catch { return null; /* corrupt credentials file — treat as missing */ }
    }

    public static void SavePlatform(PlatformCredential platform) =>
        Mutate(creds => creds.Platform = platform);

    public static void ClearPlatform() =>
        Mutate(creds => creds.Platform = null);

    // --- Shared read-modify-write -----------------------------------------------------------

    private static void Mutate(Action<ForgeCredentials> change)
    {
        var dir = Path.GetDirectoryName(CredentialsPath)!;
        Directory.CreateDirectory(dir);

        ForgeCredentials existing;
        try
        {
            existing = File.Exists(CredentialsPath)
                ? JsonSerializer.Deserialize(File.ReadAllText(CredentialsPath), CredentialsJsonContext.Default.ForgeCredentials) ?? new()
                : new();
        }
        catch { existing = new(); }

        change(existing);
        File.WriteAllText(CredentialsPath,
            JsonSerializer.Serialize(existing, CredentialsJsonContext.Default.ForgeCredentials));
    }
}
