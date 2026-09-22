using Scout;
using Scout.Grok;

namespace ForgeMission.Runner;

/// <summary>Builds the Runner-owned optional live-search backend from its deployment credentials.</summary>
internal static class RunnerWebSearch
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static IWebSearch? Build()
    {
        var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY")
                     ?? Environment.GetEnvironmentVariable("GROK_API_KEY");
        return string.IsNullOrWhiteSpace(apiKey) ? null : new GrokWebSearch(HttpClient, apiKey);
    }
}
