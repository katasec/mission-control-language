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

    /// <summary>
    /// Resolves one OCI expert to its immutable identity and materializes it in the cache
    /// (43.20 task 3).
    ///
    /// Resolution always goes to the registry, even when the materialization already exists: a tag
    /// cannot honestly become an immutable <c>oci://…@sha256:…</c> source without its current
    /// manifest, and the manifest digest is the whole point of the record. That is deliberate, not
    /// a missed cache hit — the status word says <c>resolved</c> when the digest-derived file
    /// already holds identical content and <c>pulled</c> otherwise, and neither claims an offline
    /// success.
    /// </summary>
    /// <param name="refresh">Rewrites the cache file even when it already holds identical content
    /// — the escape hatch for a corrupted materialization. It does not change what is resolved,
    /// because resolution already goes to the registry every time.</param>
    public static async Task<PulledExpertResult> PullAsync(
        string ociRef,
        bool   refresh,
        CancellationToken ct = default)
    {
        var (registry, name, tag) = ParseRef(ociRef);
        var token = CredentialStore.GetToken(registry);

        using var client = new OciClient(credential: token);
        var pulled = await client.PullExpertWithDigestAsync(registry, name, tag, ct);

        // The source is built and validated BEFORE anything is written, so a registry answer that
        // cannot become a portable source fails here rather than after touching the cache.
        var source    = ExpertSource.ForOci(registry, name, pulled.ManifestDigest);
        var cachePath = ForgeCache.ExpertMdPath(ExpertSource.Parse(source, name));

        var status = await MaterializeAsync(cachePath, pulled.Content, refresh, ct);
        return new PulledExpertResult(source, LockFileIO.ComputeContentDigest(cachePath), status);
    }

    // Writing identical bytes over identical bytes is not worth a file mutation, and skipping it
    // keeps the cache file's timestamp meaningful. The content digest is computed from the file on
    // disk either way, so the recorded digest always describes what a later run will actually read.
    private static async Task<string> MaterializeAsync(
        string cachePath, string content, bool refresh, CancellationToken ct)
    {
        if (!refresh && File.Exists(cachePath) && await File.ReadAllTextAsync(cachePath, ct) == content)
            return "resolved";

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await File.WriteAllTextAsync(cachePath, content, ct);
        return "pulled";
    }
}

/// <summary>What <c>forge init</c> records for one OCI expert: the immutable source URI, the
/// content digest of the resolved <c>expert.md</c>, and how it was obtained. No materialization
/// path is returned — the lock file must not contain one.</summary>
record PulledExpertResult(string Source, string ContentDigest, string Status);
