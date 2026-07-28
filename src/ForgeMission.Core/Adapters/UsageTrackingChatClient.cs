using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Adapters;

/// <summary>
/// Thread-safe token counter for a single mission run (Phase 39.1). One accumulator is created per
/// run request in the runner, so counts never bleed across the warm runner's concurrent runs.
/// </summary>
public sealed class UsageAccumulator
{
    private long _input;
    private long _cachedInput;
    private long _output;
    private long _reasoningOutput;

    private static readonly AsyncLocal<UsageAccumulator?> CurrentStepSlot = new();

    public void Add(UsageDetails? usage)
    {
        if (usage is null) return;
        if (usage.InputTokenCount  is { } i) System.Threading.Interlocked.Add(ref _input,  i);
        if (usage.CachedInputTokenCount is { } c) System.Threading.Interlocked.Add(ref _cachedInput, c);
        if (usage.OutputTokenCount is { } o) System.Threading.Interlocked.Add(ref _output, o);
        if (usage.ReasoningTokenCount is { } r) System.Threading.Interlocked.Add(ref _reasoningOutput, r);
    }

    public long InputTokens  => System.Threading.Interlocked.Read(ref _input);
    public long CachedInputTokens => System.Threading.Interlocked.Read(ref _cachedInput);
    public long OutputTokens => System.Threading.Interlocked.Read(ref _output);
    public long ReasoningOutputTokens => System.Threading.Interlocked.Read(ref _reasoningOutput);
    public long TotalTokens => InputTokens + OutputTokens;

    public static UsageAccumulator? CurrentStep => CurrentStepSlot.Value;

    public static IDisposable BeginStep(UsageAccumulator accumulator)
    {
        var prior = CurrentStepSlot.Value;
        CurrentStepSlot.Value = accumulator;
        return new UsageScope(prior);
    }

    private sealed class UsageScope(UsageAccumulator? prior) : IDisposable
    {
        public void Dispose() => CurrentStepSlot.Value = prior;
    }
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
        AddUsage(response.Usage);
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
                    AddUsage(usageContent.Details);
            yield return update;
        }
    }

    private void AddUsage(UsageDetails? usage)
    {
        accumulator.Add(usage);
        UsageAccumulator.CurrentStep?.Add(usage);
    }
}
