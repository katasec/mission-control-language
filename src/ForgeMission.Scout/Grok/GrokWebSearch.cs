using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Scout.Grok;

/// <summary>
/// Grok-backed <see cref="IWebSearch"/> via the xAI Responses API's server-side <c>web_search</c> tool.
/// Grok is an <b>answer engine</b>: it runs a server-side search+reason loop and returns a synthesized
/// answer plus URL citations (no raw page content). So this populates <see cref="WebSearchResult.Answer"/>
/// and fills <see cref="WebSearchResult.Sources"/> from the citation URLs. The "raw hits → MCL synthesizes"
/// path arrives with a raw-search backend (Tavily/Exa, Phase 41.3).
/// <para>Direct <see cref="HttpClient"/> + STJ source-gen — the OpenAI SDK cannot emit a
/// <c>{"type":"web_search"}</c> server-side tool, and this keeps the path AOT-clean.</para>
/// </summary>
public sealed class GrokWebSearch(HttpClient http, string apiKey, string model = "grok-4.5") : IWebSearch
{
    private const string Endpoint = "https://api.x.ai/v1/responses";
    private const string ProviderName = "grok";

    // Buffered path (no narration) — used by the CLI and as the fallback for backends/callers that
    // don't want progress. One request, one response object, mapped.
    public async Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct = default)
    {
        using var httpResp = await SendAsync(request, stream: false, ct).ConfigureAwait(false);

        var parsed = await httpResp.Content
            .ReadFromJsonAsync(GrokJsonContext.Default.GrokResponse, ct)
            .ConfigureAwait(false)
            ?? throw new WebSearchException("xAI Responses API returned an empty/unparseable body.");

        return Map(parsed);
    }

    // Streaming path (Phase 41.7 Task 2) — narrate the server-side search loop. Grok emits one
    // web_search_call item per sub-search; its completed `action` (on response.output_item.done) is
    // reported as a WebSearchProgress. The terminal response.completed carries the full final response,
    // so the answer + citations reuse the buffered Map(). One HTTP call — the search never re-runs.
    public async Task<WebSearchResult> SearchAsync(
        WebSearchRequest request, IProgress<WebSearchProgress>? progress, CancellationToken ct = default)
    {
        if (progress is null)
            return await SearchAsync(request, ct).ConfigureAwait(false);

        using var httpResp = await SendAsync(request, stream: true, ct).ConfigureAwait(false);
        await using var body = await httpResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(body);

        WebSearchResult? result = null;
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;   // ignore `event:` + blanks
            var json = line[5..].Trim();
            if (json.Length == 0 || json == "[DONE]") continue;

            var evt = JsonSerializer.Deserialize(json, GrokJsonContext.Default.GrokStreamEvent);
            switch (evt?.Type)
            {
                case "response.output_item.done" when evt.Item?.Type == "web_search_call":
                    if (ToProgress(evt.Item.Action) is { } p) progress.Report(p);
                    break;
                case "response.completed" when evt.Response is not null:
                    result = Map(evt.Response);
                    break;
            }
        }

        return result ?? throw new WebSearchException("xAI stream ended without a completed response.");
    }

    // Build + send the Responses request. `stream` toggles SSE; ResponseHeadersRead keeps the streamed
    // body a live stream (bytes flow as Grok searches). Throws WebSearchException on a non-2xx.
    private async Task<HttpResponseMessage> SendAsync(WebSearchRequest request, bool stream, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            throw new WebSearchException("Query must be non-empty.");

        var tool = new GrokTool
        {
            Type = "web_search",
            Filters = request.AllowedDomains is { Count: > 0 } domains
                ? new GrokToolFilters { AllowedDomains = domains }
                : null,
        };
        var payload = new GrokRequest
        {
            Model = model,
            Input = [new GrokInputMessage { Role = "user", Content = request.Query }],
            Tools = [tool],
            Stream = stream ? true : null,
        };

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload, GrokJsonContext.Default.GrokRequest),
        };
        httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var completion = stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
        var httpResp = await http.SendAsync(httpReq, completion, ct).ConfigureAwait(false);
        if (httpResp.IsSuccessStatusCode) return httpResp;

        var errBody = await httpResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var snippet = errBody.Length > 500 ? errBody[..500] : errBody;
        var status  = (int)httpResp.StatusCode;
        httpResp.Dispose();
        throw new WebSearchException($"xAI Responses API returned {status}: {snippet}");
    }

    // Map one completed web_search_call action → a neutral progress event. Non-search items (reasoning,
    // the final message) and unknown action types map to null and are skipped.
    private static WebSearchProgress? ToProgress(GrokSearchAction? action) => action?.Type switch
    {
        "search"    => new WebSearchProgress("searching_web", action.Query, action.Sources?.Count),
        "open_page" => new WebSearchProgress("reading", HostOf(action.Url)),
        _           => null,
    };

    private static string? HostOf(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;

    // Pull the synthesized answer + citation URLs off the single "message" item in output[].
    private static WebSearchResult Map(GrokResponse resp)
    {
        var answer = new StringBuilder();
        var sources = new List<SourceRef>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in resp.Output ?? [])
        {
            if (item.Type != "message" || item.Content is null) continue;

            foreach (var content in item.Content)
            {
                if (content.Type != "output_text") continue;

                if (!string.IsNullOrEmpty(content.Text))
                    answer.Append(content.Text);

                foreach (var ann in content.Annotations ?? [])
                {
                    // Grok's annotation "title" is a numeric citation label ("1"), not a page title —
                    // leave SourceRef.Title null rather than mislabel. Provider tag travels with each.
                    if (ann.Type == "url_citation" && !string.IsNullOrEmpty(ann.Url) && seenUrls.Add(ann.Url))
                        sources.Add(new SourceRef(ann.Url, Title: null, ProviderName));
                }
            }
        }

        return new WebSearchResult(
            Provider: ProviderName,
            Answer: answer.Length > 0 ? answer.ToString() : null,
            Sources: sources);
    }
}
