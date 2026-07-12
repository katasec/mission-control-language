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

    /// <summary>
    /// Optional streaming variant (Phase 41.7): narrate a backend's server-side search loop as
    /// provider-neutral <see cref="WebSearchProgress"/> events while it runs. Answer-engines (Grok,
    /// OpenAI) map their native <c>web_search_call</c> stream here; raw search APIs (Tavily/Exa) have
    /// no loop to narrate and inherit the default — <b>no events</b>. Callers still fetch the grounded
    /// result from <see cref="SearchAsync"/>; this channel is progress only.
    /// </summary>
    IAsyncEnumerable<WebSearchProgress> SearchStreamAsync(WebSearchRequest request, CancellationToken ct = default)
        => NoProgress();

    /// <summary>Default: a backend that can't narrate emits nothing (the interface still satisfies).</summary>
    private static async IAsyncEnumerable<WebSearchProgress> NoProgress()
    {
        await Task.CompletedTask;
        yield break;
    }
}

/// <summary>
/// A provider-neutral progress event from a backend's search loop — the streaming parallel of
/// <see cref="WebSearchResult"/>: a shape every backend <i>fills</i>, never a backend's wire type.
/// Grok/OpenAI map their SSE <c>web_search_call</c> actions onto this; the transport + UI consume it
/// unchanged whatever the backend. <see cref="Kind"/> is the coarse phase (a small closed vocabulary);
/// <see cref="Detail"/> and <see cref="ResultCount"/> are best-effort colour when the backend reports it.
/// </summary>
public sealed record WebSearchProgress(
    string  Kind,          // "searching_web" | "searching_x" | "reading" | "results"
    string? Detail = null, // the query being searched, or a URL being read
    int?    ResultCount = null);

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
