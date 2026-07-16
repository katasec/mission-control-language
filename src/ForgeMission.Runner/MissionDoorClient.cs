using ForgeMission.Cli;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Runtime;
using Microsoft.Extensions.AI;

namespace ForgeMission.Runner;

/// <summary>
/// The /v1 wire doors' entry into the mission registry (Phase 42.4 task 2). One instance per
/// door; each call resolves the wire's <c>model</c> field to a mission (<c>"@grok"</c> /
/// <c>"grok"</c> → label <c>Grok</c>, case-insensitive), then runs it on a FRESH provider
/// client + <see cref="MissionChatClient"/> — per-request isolation, mirroring
/// <see cref="MissionRunHandler"/>. A single-mission registry answers regardless of model
/// (wire clients like the claude CLI send provider model ids, not mission labels — the
/// <c>forge claude --container</c> case). Metering/billing wrap at the hosting layer (42.6),
/// not here; key→mission routing replaces model routing there too.
/// </summary>
internal sealed class MissionDoorClient(RunnerRegistry registry, bool fullConversation) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        => BuildMissionClient(options).GetResponseAsync(messages, options, ct);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        => BuildMissionClient(options).GetStreamingResponseAsync(messages, options, ct);

    public void Dispose() { }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    // ------------------------------------------------------------------

    private IChatClient BuildMissionClient(ChatOptions? options)
    {
        var mission = ResolveMission(options?.ModelId);

        // Instrumented provider client per request, as in MissionRunHandler (gen_ai.* spans,
        // sensitive data off). No UsageTrackingChatClient: the /v1 doors are unmetered until
        // 42.6 wraps them at the hosting layer.
        IExpertRunner runner = mission.Profile is { } profile
            ? new DirectExpertRunner(ProviderClientBuilder.BuildChatClient(profile)
                .AsBuilder()
                .UseOpenTelemetry(sourceName: RunnerTelemetry.SourceName)
                .Build())
            : new ExecExpertRunner();

        return new MissionChatClient(
            mission.Ast, mission.Experts, runner, fullConversation,
            webSearch: ProviderClientBuilder.BuildWebSearch());
    }

    private RunnerMission ResolveMission(string? modelId)
    {
        var handle = (modelId ?? string.Empty).TrimStart('@');
        var match  = registry.All.FirstOrDefault(m =>
            string.Equals(m.Label, handle, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match;

        if (registry.All.Count == 1)
            return registry.All.First();

        throw new InvalidOperationException(
            $"Unknown mission '{modelId}'. Loaded: {string.Join(", ", registry.All.Select(m => m.Label))}.");
    }
}
