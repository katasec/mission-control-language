using Katasec.OciClient;
using ForgeMission.Core.Resolution;

static class OciExpertPuller
{
    // Parses "ghcr.io/katasec/forge-k8s-architect@0.1.0" → (registry, name, tag)
    public static (string Registry, string Name, string Tag) ParseRef(string ociRef)
    {
        var firstSlash = ociRef.IndexOf('/');
        if (firstSlash < 0)
            throw new ArgumentException($"Invalid OCI reference '{ociRef}': expected registry/name@tag");

        var registry = ociRef[..firstSlash];
        var rest     = ociRef[(firstSlash + 1)..];

        var atIdx = rest.LastIndexOf('@');
        if (atIdx < 0)
            throw new ArgumentException($"Invalid OCI reference '{ociRef}': expected name@tag");

        return (registry, rest[..atIdx], rest[(atIdx + 1)..]);
    }

    // Pull expert into ~/.forge cache. Returns (absolutePath, status) where
    // status is "cached", "pulled", or throws on failure.
    public static async Task<(string Path, string Status)> PullAsync(
        string ociRef,
        bool   refresh,
        CancellationToken ct = default)
    {
        var (registry, name, tag) = ParseRef(ociRef);
        var cachePath = ForgeCache.ExpertMdPath(registry, name, tag);

        if (!refresh && File.Exists(cachePath))
            return (cachePath, "cached");

        var token = CredentialStore.GetToken(registry);

        using var client = new OciClient(credential: token);
        var content = await client.PullExpertAsync(registry, name, tag);

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await File.WriteAllTextAsync(cachePath, content, ct);

        return (cachePath, "pulled");
    }

    // Returns a ~/... path for storing in mcl.lock so it survives machine moves.
    public static string ToLockPath(string absolutePath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (absolutePath.StartsWith(home, StringComparison.Ordinal))
            return "~/" + absolutePath[(home.Length + 1)..].Replace(Path.DirectorySeparatorChar, '/');
        return absolutePath;
    }
}
