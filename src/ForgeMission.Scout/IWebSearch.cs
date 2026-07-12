namespace Scout;

/// <summary>
/// The single swap point for live-internet retrieval. Callers depend only on this interface and the
/// provider-neutral <see cref="WebSearchRequest"/> / <see cref="WebSearchResult"/> types — never on a
/// backend's SDK or wire types. Adding a backend (OpenAI, Tavily, Exa, Grok x_search) is one new
/// implementing class, zero caller changes. See docs/phases/phase-41-live-retrieval-scout.md.
/// </summary>
public interface IWebSearch
{
    Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct = default);
}

/// <summary>What to search for, provider-neutrally. POC exposes a query + optional domain scoping.</summary>
public sealed record WebSearchRequest(
    string Query,
    IReadOnlyList<string>? AllowedDomains = null);

/// <summary>
/// A source-attributed retrieval result that spans both backend families:
/// answer-engines (Grok/OpenAI — synthesize by design, populate <see cref="Answer"/>) and raw search
/// APIs (Tavily/Exa — leave <see cref="Answer"/> null, fill <see cref="Sources"/> with real hits).
/// <para><b>Contract:</b> <see cref="Sources"/> is always the anchor; <see cref="Answer"/> is optional.
/// Downstream MCL reasoning should key off <see cref="Sources"/> so swapping in a raw backend never
/// surprises a chain that assumed a synthesized answer.</para>
/// </summary>
public sealed record WebSearchResult(
    string Provider,                              // "grok" — which backend produced this result
    string? Answer,                              // answer-engine synthesis; null for raw search APIs
    IReadOnlyList<SourceRef> Sources);

/// <summary>
/// One attributed source. <see cref="Provider"/> travels with every source (source attribution over
/// source-selection); <see cref="ImpartialityRating"/> is a null stub for a future hosted per-domain
/// rating (Phase 41.6) — we manage provenance, we do not chase "the best source".
/// </summary>
public sealed record SourceRef(
    string Url,
    string? Title,
    string Provider,
    double? ImpartialityRating = null);

/// <summary>Thrown when a backend call fails (non-2xx or unparseable), carrying enough to diagnose.</summary>
public sealed class WebSearchException(string message) : Exception(message);
