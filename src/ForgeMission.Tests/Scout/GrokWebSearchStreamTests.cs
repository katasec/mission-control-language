using System.Net;
using Scout;
using Scout.Grok;

namespace ForgeMission.Tests.Scout;

/// <summary>
/// Phase 41.7 Task 2: <see cref="GrokWebSearch"/>'s streaming path parses xAI's SSE Responses stream and
/// maps each <c>web_search_call</c> sub-search to a provider-neutral <see cref="WebSearchProgress"/>, then
/// builds the grounded result from the terminal <c>response.completed</c>. The fixture below is real wire
/// shape captured live from api.x.ai (2026-07-12), trimmed for size — so this proves the parser against
/// ground truth, network-free, in CI.
/// </summary>
public class GrokWebSearchStreamTests
{
    // Real SSE frames: a `search` sub-search (query + 3 sources), an `open_page` sub-search (a URL being
    // read), then the terminal completed response carrying the message (answer + one citation).
    private const string Sse =
        """
        event: response.output_item.done
        data: {"type":"response.output_item.done","item":{"type":"web_search_call","status":"completed","action":{"type":"search","query":"next FIFA World Cup game schedule","sources":[{"type":"url","url":"https://www.fifa.com/en/x"},{"type":"url","url":"https://www.espn.com/soccer/schedule"},{"type":"url","url":"https://kansascityfwc26.com/matches/"}]}}}

        event: response.output_item.done
        data: {"type":"response.output_item.done","item":{"type":"web_search_call","status":"completed","action":{"type":"open_page","url":"https://www.fifa.com/en/tournaments/worldcup/schedule"}}}

        event: response.output_text.delta
        data: {"type":"response.output_text.delta","delta":"ignored"}

        event: response.completed
        data: {"type":"response.completed","response":{"output":[{"type":"reasoning"},{"type":"message","content":[{"type":"output_text","text":"The next game is France vs. Spain on July 14.","annotations":[{"type":"url_citation","url":"https://www.fifa.com/en/x"}]}]}]}}

        """;

    [Fact]
    public async Task StreamingSearch_ReportsSubSearchProgress_ThenBuildsGroundedResult()
    {
        var http = new HttpClient(new SseHandler(Sse));
        var search = new GrokWebSearch(http, apiKey: "test-key");

        var events = new List<WebSearchProgress>();
        var progress = new CollectingProgress(events);

        var result = await search.SearchAsync(new WebSearchRequest("when is the next world cup game"), progress);

        // Two sub-searches narrated, in order, mapped to neutral shapes.
        Assert.Equal(2, events.Count);
        Assert.Equal("searching_web", events[0].Kind);
        Assert.Equal("next FIFA World Cup game schedule", events[0].Detail);
        Assert.Equal(3, events[0].ResultCount);                          // #sources on the completed action
        Assert.Equal("reading", events[1].Kind);
        Assert.Equal("www.fifa.com", events[1].Detail);                  // open_page url reduced to host

        // Terminal response.completed → grounded result (answer + citation), same Map() as the buffered path.
        Assert.Equal("grok", result.Provider);
        Assert.Contains("France vs. Spain", result.Answer);
        Assert.Equal("https://www.fifa.com/en/x", Assert.Single(result.Sources).Url);
    }

    [Fact]
    public async Task NullProgress_TakesBufferedPath_NoStreaming()
    {
        // A non-SSE (plain JSON) body — the buffered SearchAsync path. Passing null progress must not
        // attempt to stream-parse; it should read the whole response object and Map() it.
        const string buffered =
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"42","annotations":[]}]}]}""";
        var http = new HttpClient(new JsonHandler(buffered));
        var search = new GrokWebSearch(http, apiKey: "test-key");

        var result = await search.SearchAsync(new WebSearchRequest("q"), progress: null);

        Assert.Equal("42", result.Answer);
    }

    private sealed class CollectingProgress(List<WebSearchProgress> sink) : IProgress<WebSearchProgress>
    {
        public void Report(WebSearchProgress value) => sink.Add(value);
    }

    // Returns the SSE body as a text/event-stream response — exercises the line-by-line stream read.
    private sealed class SseHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "text/event-stream"),
            });
    }

    private sealed class JsonHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
