using Scout;
using Scout.Grok;

namespace ForgeMission.Tests.Scout;

/// <summary>
/// Integration test for the Grok-backed <see cref="IWebSearch"/> (Phase 41.1). Hits the real xAI
/// Responses API, so it is skipped automatically when XAI_API_KEY is not set.
/// Run with: XAI_API_KEY=... dotnet test --filter Category=Integration
/// </summary>
public class GrokWebSearchIntegrationTests
{
    private static readonly string? ApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task WebSearch_RealGrok_ReturnsSourceTaggedAnswer()
    {
        Skip.If(string.IsNullOrWhiteSpace(ApiKey), "XAI_API_KEY not set — skipping integration test");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        IWebSearch search = new GrokWebSearch(http, ApiKey!);

        var result = await search.SearchAsync(
            new WebSearchRequest("Summarize the latest news from Mario Nawfal's YouTube channel in 3 bullet points."));

        // Grok is an answer engine: expect a synthesized answer plus at least one attributed source.
        Assert.Equal("grok", result.Provider);
        Assert.False(string.IsNullOrWhiteSpace(result.Answer));
        Assert.NotEmpty(result.Sources);
        Assert.All(result.Sources, s =>
        {
            Assert.Equal("grok", s.Provider);           // source attribution travels with each hit
            Assert.False(string.IsNullOrWhiteSpace(s.Url));
        });
    }

    // Phase 41.7 Task 2: the streaming path against the REAL xAI SSE stream — proves sub-search progress
    // events flow (not just the trimmed fixture in GrokWebSearchStreamTests) AND the grounded result still
    // comes back from one call. A current-events query guarantees the server-side search loop runs.
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task StreamingSearch_RealGrok_NarratesSubSearches_AndReturnsResult()
    {
        Skip.If(string.IsNullOrWhiteSpace(ApiKey), "XAI_API_KEY not set — skipping integration test");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        var search = new GrokWebSearch(http, ApiKey!);

        var events = new List<WebSearchProgress>();
        var progress = new Progress<WebSearchProgress>(events.Add);

        var result = await search.SearchAsync(
            new WebSearchRequest("What is the top technology news story today?"), progress);

        // At least one real sub-search was narrated, and every event maps to a known neutral kind.
        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.Contains(e.Kind, new[] { "searching_web", "searching_x", "reading", "results" }));
        // And the grounded result still arrives from the same streaming call.
        Assert.Equal("grok", result.Provider);
        Assert.False(string.IsNullOrWhiteSpace(result.Answer));
    }
}
