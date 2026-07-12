using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

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

    public async Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct = default)
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
        };

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload, GrokJsonContext.Default.GrokRequest),
        };
        httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var httpResp = await http.SendAsync(httpReq, ct).ConfigureAwait(false);
        if (!httpResp.IsSuccessStatusCode)
        {
            var body = await httpResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var snippet = body.Length > 500 ? body[..500] : body;
            throw new WebSearchException($"xAI Responses API returned {(int)httpResp.StatusCode}: {snippet}");
        }

        var parsed = await httpResp.Content
            .ReadFromJsonAsync(GrokJsonContext.Default.GrokResponse, ct)
            .ConfigureAwait(false)
            ?? throw new WebSearchException("xAI Responses API returned an empty/unparseable body.");

        return Map(parsed);
    }

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
