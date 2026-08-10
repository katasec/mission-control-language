using Scout;
using Scout.Grok;

// Live-retrieval wiring stays in CLI; chat-provider construction lives in ForgeMission.ChatClients.
public static class ProviderClientBuilder
{
    // Live-retrieval backend for kind:search experts (Phase 41). Implicitly Grok for the POC.
    // Returns null when no xAI key is present — missions without kind:search are unaffected; a
    // kind:search step then fails with a clear "IWebSearch not configured" error.
    private static readonly HttpClient SearchHttpClient = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static IWebSearch? BuildWebSearch()
    {
        var key = Environment.GetEnvironmentVariable("XAI_API_KEY")
                  ?? Environment.GetEnvironmentVariable("GROK_API_KEY");
        return string.IsNullOrWhiteSpace(key) ? null : new GrokWebSearch(SearchHttpClient, key);
    }

}
