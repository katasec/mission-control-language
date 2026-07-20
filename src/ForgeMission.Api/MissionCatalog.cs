using ForgeMission.Runner.Contracts;

namespace ForgeMission.Api;

/// <summary>
/// Docker-style handle parsing — one code path for implicit and explicit publisher (42.6 task 5a,
/// "Mission resolution" in the phase-42.6 spoke). Pure, dependency-free — unit-testable with zero
/// mocks/I-O.
/// </summary>
public readonly record struct MissionHandle(string? Publisher, string Name)
{
    /// <summary>"websearch" -&gt; (null, "websearch") ; "forge/websearch" -&gt; ("forge", "websearch").
    /// Always lowercased — handles are case-insensitive.</summary>
    public static MissionHandle Parse(string raw)
    {
        var s = raw.Trim().ToLowerInvariant();
        var slash = s.IndexOf('/');
        return slash < 0 ? new MissionHandle(null, s) : new MissionHandle(s[..slash], s[(slash + 1)..]);
    }
}

/// <summary>
/// Resolves a <see cref="MissionHandle"/> to the catalog artifact the runner loads. Repository-
/// pattern seam (in-memory today, DB-swappable later) — same shape as <c>ILedgerStore</c>/
/// <c>IPlatformKeyStore</c>.
/// </summary>
public interface IMissionCatalog
{
    /// <summary>NOT "katasec" — that's the OCI registry (ghcr.io/katasec), a distribution detail.
    /// "forge" is the brand/publisher identity, kept deliberately separate.</summary>
    const string DefaultPublisher = "forge";

    Task<CatalogEntry?> ResolveAsync(MissionHandle handle, string? version, CancellationToken ct);
    Task<IReadOnlyList<CatalogEntry>> SearchAsync(string? query, string? publisher, CancellationToken ct);
}

public sealed record CatalogEntry(
    string Handle,
    string Description,
    string Publisher,
    string Version,
    bool Verified,
    string MissionRef,
    MissionArtifactCapabilities? ArtifactCapabilities);

/// <summary>
/// Today's only <see cref="IMissionCatalog"/> implementation: a hardcoded entry list filtered
/// against the runner's live <c>GET /missions</c> at construction — the same precedent
/// <see cref="!:AgentRegistry"/> (src/ForgeUI/Services/AgentRegistry.cs) already sets, so a built-in
/// whose backing mission isn't currently loadable (e.g. a missing provider key) simply doesn't
/// resolve rather than 500ing.
///
/// <para>Resolution is <b>one path</b>, not an implicit/explicit fork: an absent handle publisher
/// defaults to <see cref="IMissionCatalog.DefaultPublisher"/>, then a single lookup keyed on
/// (publisher, name) runs — so <c>"websearch"</c> and <c>"forge/websearch"</c> resolve identically
/// (the same guarantee <c>docker pull nginx</c> == <c>docker pull library/nginx</c> gives). An
/// unrecognized publisher fails closed (no entry), it never falls through to a name-only match.</para>
///
/// <para>Multi-publisher support (real third-party publishers) is a two-way door from here:
/// <see cref="MissionHandle"/>/<see cref="IMissionCatalog"/> never change, only this class's
/// hardcoded list gets replaced by a real registry.</para>
/// </summary>
public sealed class StaticMissionCatalog : IMissionCatalog
{
    private readonly List<CatalogEntry> _entries = [];

    /// <param name="availableMissions">Missions the runner can execute (from GET /missions).
    /// An entry whose ref is absent is skipped, so its handle simply won't resolve.</param>
    public StaticMissionCatalog(IReadOnlyList<MissionInfo> availableMissions)
    {
        Register(availableMissions, new CatalogEntry(
            Handle: "websearch",
            Description: "Grounded, source-cited answers via live web search — classifies, searches when current data is needed.",
            Publisher: IMissionCatalog.DefaultPublisher,
            Version: "0.1.0",
            Verified: true,
            MissionRef: "WebSearch",
            ArtifactCapabilities: null));

        Register(availableMissions, new CatalogEntry(
            Handle: "ocr",
            Description: "Deterministic OCR artifact demo — accepts an uploaded image/PDF and returns text or PDF output.",
            Publisher: IMissionCatalog.DefaultPublisher,
            Version: "0.1.0",
            Verified: true,
            MissionRef: "Ocr",
            ArtifactCapabilities: null));

        Register(availableMissions, new CatalogEntry(
            Handle: "summarize",
            Description: "OCR + verified LLM synthesis — accepts an uploaded image/PDF and returns a grounded summary.",
            Publisher: IMissionCatalog.DefaultPublisher,
            Version: "0.1.0",
            Verified: true,
            MissionRef: "Summarize",
            ArtifactCapabilities: null));
    }

    private void Register(IReadOnlyList<MissionInfo> availableMissions, CatalogEntry entry)
    {
        var mission = availableMissions.FirstOrDefault(m =>
            string.Equals(m.MissionRef, entry.MissionRef, StringComparison.Ordinal));
        if (mission is null) return;

        _entries.Add(entry with { ArtifactCapabilities = mission.ArtifactCapabilities });
    }

    public Task<CatalogEntry?> ResolveAsync(MissionHandle handle, string? version, CancellationToken ct)
    {
        var publisher = handle.Publisher ?? IMissionCatalog.DefaultPublisher;
        var match = _entries.FirstOrDefault(e =>
            string.Equals(e.Publisher, publisher, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Handle, handle.Name, StringComparison.OrdinalIgnoreCase));
        // Single-version static catalog for now — `version` isn't used to select among alternatives
        // yet (there's only ever one). A future multi-version catalog checks it here.
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<CatalogEntry>> SearchAsync(string? query, string? publisher, CancellationToken ct)
    {
        IEnumerable<CatalogEntry> results = _entries;
        if (publisher is { Length: > 0 })
            results = results.Where(e => string.Equals(e.Publisher, publisher, StringComparison.OrdinalIgnoreCase));
        if (query is { Length: > 0 })
            results = results.Where(e =>
                e.Handle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<CatalogEntry>>(results.ToList());
    }
}
