using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Adapters;

/// <summary>
/// Thread-safe token counter for a single mission run (Phase 39.1). One accumulator is created per
/// run request in the runner, so counts never bleed across the warm runner's concurrent runs.
/// </summary>
public sealed class UsageAccumulator
{
    private long _input;
    private long _output;

    public void Add(UsageDetails? usage)
    {
        if (usage is null) return;
        if (usage.InputTokenCount  is { } i) System.Threading.Interlocked.Add(ref _input,  i);
        if (usage.OutputTokenCount is { } o) System.Threading.Interlocked.Add(ref _output, o);
    }

    public long InputTokens  => System.Threading.Interlocked.Read(ref _input);
    public long OutputTokens => System.Threading.Interlocked.Read(ref _output);
}

/// <summary>
/// Decorates an <see cref="IChatClient"/> to accumulate token usage from every response into a
/// per-run <see cref="UsageAccumulator"/>. The runner wraps its provider client with this so each
/// run emits <c>tokens</c> (half of the cost-meter; compute-seconds is the wall-clock half). The
/// orchestrator prices and debits these signals in 39.2 — the runner only measures.
/// </summary>
public sealed class UsageTrackingChatClient(IChatClient inner, UsageAccumulator accumulator)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        accumulator.Add(response.Usage);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            // Streaming usage arrives as a trailing UsageContent on the final update(s).
            foreach (var content in update.Contents)
                if (content is UsageContent usageContent)
                    accumulator.Add(usageContent.Details);
            yield return update;
        }
    }
}
