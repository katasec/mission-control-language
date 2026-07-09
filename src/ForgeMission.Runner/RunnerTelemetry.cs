using System.Diagnostics;

namespace ForgeMission.Runner;

/// <summary>
/// The runner's tracing surface. One <see cref="ActivitySource"/> carries both the mission-level
/// span we emit per <c>/run</c> and the <c>gen_ai.*</c> spans that <c>UseOpenTelemetry</c> emits from
/// the wrapped <see cref="Microsoft.Extensions.AI.IChatClient"/> (we pass this same name as its
/// <c>sourceName</c>). Attributes are deliberately non-sensitive: mission ref, provider name, and
/// model — never the API key. Combined with the HTTP client instrumentation's <c>server.address</c>,
/// a trace shows exactly which endpoint + model each <c>@</c>-agent hit, which is the ground truth
/// that a "wrong provider" bug (e.g. @openai answering as xAI) would otherwise hide.
/// </summary>
internal static class RunnerTelemetry
{
    public const string SourceName = "ForgeMission.Runner";

    public static readonly ActivitySource Source = new(SourceName);
}
