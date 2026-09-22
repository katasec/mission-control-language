using ForgeMission.Core.Retrieval;
using Scout.Grok;

namespace ForgeMission.Runner;

// Runner owns its provider-side search composition. The static client preserves the existing
// three-minute timeout and deliberately has no lifetime tied to a single mission request.
internal static class RunnerWebSearch
{
    private static readonly HttpClient SearchHttpClient = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static IWebSearch? Build()
    {
        return Build(
            Environment.GetEnvironmentVariable("XAI_API_KEY"),
            Environment.GetEnvironmentVariable("GROK_API_KEY"));
    }

    internal static IWebSearch? Build(string? xaiKey, string? grokKey)
    {
        return Build(ResolveKey(xaiKey, grokKey));
    }

    internal static string? ResolveKey(string? xaiKey, string? grokKey) => xaiKey ?? grokKey;

    private static IWebSearch? Build(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : new GrokWebSearch(SearchHttpClient, key);
}
