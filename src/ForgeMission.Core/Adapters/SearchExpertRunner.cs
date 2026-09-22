using System.Runtime.CompilerServices;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using Scout;

namespace ForgeMission.Core.Adapters;

/// <summary>
/// The <c>kind: search</c> expert — a native primitive that performs live-internet retrieval via Scout's
/// <see cref="IWebSearch"/>. Reads the query from context (<c>query</c>, set by a <c>(query: …)</c> step
/// binding, falling back to <c>output</c>), calls the configured backend, and publishes the results as
/// named context keys the way <c>json_extract</c> does — <c>search_results</c> (the retrieved text) and
/// <c>search_sources</c> (newline-joined source URLs) — so a downstream <c>llm</c> expert can template
/// <c>{{search_results}}</c> / <c>{{search_sources}}</c>.
/// <para>Depends only on the <see cref="IWebSearch"/> abstraction — the backend (Grok today; Tavily/Exa/
/// OpenAI later) is chosen where the <see cref="PipelineRunner"/> is built, keeping this AOT-clean and
/// provider-agnostic.</para>
/// </summary>
public sealed class SearchExpertRunner(IWebSearch search, Action<WebSearchProgress>? onProgress = null) : IExpertRunner
{
    // Report sub-search steps synchronously and in order (not Progress<T>, which posts asynchronously
    // and can reorder). Null when no consumer is listening ⇒ the backend takes its buffered path.
    private readonly IProgress<WebSearchProgress>? _progress =
        onProgress is null ? null : new SyncProgress(onProgress);

    public async Task<StepEnvelope> RunAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        CancellationToken ct = default)
    {
        var query = Resolve(context, "query") ?? Resolve(context, "output");
        if (string.IsNullOrWhiteSpace(query))
            throw new InvalidOperationException(
                $"kind: search ({expert.Name}) has no query — set a 'query' binding or upstream 'output'.");

        var result = await search.SearchAsync(new WebSearchRequest(query), _progress, ct);

        // Publish retrieval into context (the json_extract pattern) for downstream {{search_results}} / {{search_sources}}.
        context["search_results"] = result.Answer ?? string.Empty;
        context["search_sources"] = result.Sources.Count > 0
            ? string.Join('\n', result.Sources.Select(s => s.Url))
            : string.Empty;

        return new StepEnvelope(result.Answer ?? string.Empty);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var envelope = await RunAsync(expert, context, ct);
        yield return envelope.Text;
    }

    private static string? Resolve(Dictionary<string, object> context, string key) =>
        context.TryGetValue(key, out var v) ? v?.ToString() : null;

    // Synchronous IProgress — invokes the callback on the reporting thread so sub-search events reach
    // the room in the order Grok emits them.
    private sealed class SyncProgress(Action<WebSearchProgress> report) : IProgress<WebSearchProgress>
    {
        public void Report(WebSearchProgress value) => report(value);
    }
}
