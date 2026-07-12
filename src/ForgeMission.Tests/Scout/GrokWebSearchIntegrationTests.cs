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
}
