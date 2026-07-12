using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Parser;
using ForgeMission.Tests.Runtime;
using Scout;
using static ForgeMission.Core.Runtime.MissionStatus;

namespace ForgeMission.Tests.Scout;

/// <summary>
/// The verified-capability proof for Phase 41.2: `kind: search` exercised **through the MCL pipeline**
/// (parse → load → dispatch → when()-guards → answer), not just its IExpertRunner in isolation. A stub
/// IWebSearch keeps this network-free so it runs in CI every commit. This is what backs the guarantee that
/// `forge run` over a search-fronted mission works.
/// </summary>
public class SearchMissionPipelineTests
{
    // The pure-MCL classify → conditionally-search → answer front-end (Phase 41.2 shape).
    private const string MissionMcl =
        """
        mission SearchAgent(goal) = {
            SearchRouter
            -> ExtractRoute
            -> WebSearch(query: search_query) when(search_needed: "yes")
            -> GroundedAnswer when(search_needed: "yes")
            -> DirectAnswer when(else)
        }
        """;

    private static Dictionary<string, ExpertDefinition> Experts() => new(StringComparer.Ordinal)
    {
        ["SearchRouter"]   = new("SearchRouter",   "goal",        "route json", "", Kind: "llm"),
        ["ExtractRoute"]   = new("ExtractRoute",   "route json",  "keys",       "", Kind: "json_extract"),
        ["WebSearch"]      = new("WebSearch",      "query",       "results",    "", Kind: "search"),
        ["GroundedAnswer"] = new("GroundedAnswer", "goal+search", "answer",     "", Kind: "llm"),
        ["DirectAnswer"]   = new("DirectAnswer",   "goal",        "answer",     "", Kind: "llm"),
    };

    // Fake retrieval backend — records calls, returns deterministic "live" data. No network.
    private sealed class StubWebSearch : IWebSearch
    {
        public int Calls;
        public string? LastQuery;
        public Task<WebSearchResult> SearchAsync(WebSearchRequest request, CancellationToken ct = default)
        {
            Calls++;
            LastQuery = request.Query;
            return Task.FromResult(new WebSearchResult(
                Provider: "stub",
                Answer: $"LIVE DATA for '{request.Query}'",
                Sources: [new SourceRef("https://example.com/a", "A", "stub")]));
        }
    }

    // LLM stub: SearchRouter emits the routing JSON; GroundedAnswer echoes the injected search_results.
    private static StubExpertRunner LlmStub(string routerJson) => new((name, ctx) => name switch
    {
        "SearchRouter"   => routerJson,
        "GroundedAnswer" => $"GROUNDED[{ctx.GetValueOrDefault("search_results")}]",
        "DirectAnswer"   => "DIRECT",
        _                => $"Output from {name}",
    });

    [Fact]
    public async Task SearchNeeded_RoutesThroughKindSearch_AndGroundsTheAnswer()
    {
        var ast = MclParser.Parse(MissionMcl);
        var web = new StubWebSearch();
        var llm = LlmStub("""{"search_needed":"yes","search_query":"world cup today"}""");

        var result = await new PipelineRunner(llm, webSearch: web)
            .RunAsync(ast, Experts(), new PipelineRunOptions("SearchAgent",
                new Dictionary<string, string> { ["goal"] = "who plays in the world cup today?" }));

        Assert.Equal(Pass, result.Status);
        Assert.Equal(1, web.Calls);                                     // kind:search actually executed
        Assert.Equal("world cup today", web.LastQuery);                 // classifier's search_query flowed via (query: …)
        Assert.Equal("GROUNDED[LIVE DATA for 'world cup today']", result.Text); // retrieval reached the grounded answer
    }

    [Fact]
    public async Task SearchNotNeeded_TakesElseBranch_WithNoSearchCall()
    {
        var ast = MclParser.Parse(MissionMcl);
        var web = new StubWebSearch();
        var llm = LlmStub("""{"search_needed":"no","search_query":""}""");

        var result = await new PipelineRunner(llm, webSearch: web)
            .RunAsync(ast, Experts(), new PipelineRunOptions("SearchAgent",
                new Dictionary<string, string> { ["goal"] = "what is 2+2?" }));

        Assert.Equal(Pass, result.Status);
        Assert.Equal(0, web.Calls);                                     // no retrieval on the passthrough path
        Assert.Equal("DIRECT", result.Text);                            // when(else) answered
    }

    // Unit-level: the primitive publishes results + sources into context and returns the answer text.
    [Fact]
    public async Task SearchExpertRunner_PublishesResultsAndSourcesIntoContext()
    {
        var web = new StubWebSearch();
        var context = new Dictionary<string, object>(StringComparer.Ordinal) { ["query"] = "starlink launch" };

        var envelope = await new SearchExpertRunner(web)
            .RunAsync(new ExpertDefinition("WebSearch", "query", "results", "", Kind: "search"), context);

        Assert.Equal("LIVE DATA for 'starlink launch'", envelope.Text);
        Assert.Equal("LIVE DATA for 'starlink launch'", context["search_results"]);
        Assert.Equal("https://example.com/a", context["search_sources"]);
    }
}
