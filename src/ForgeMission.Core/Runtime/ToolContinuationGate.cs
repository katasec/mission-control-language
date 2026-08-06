using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Runtime;

/// <summary>
/// Applies the stateless tool-continuation gate shared by mission wire adapters. A continuation
/// restores the cached pre-agent context for its user-turn prefix; a miss re-runs enrichment.
/// </summary>
public static class ToolContinuationGate
{
    public static async Task<ToolContinuationState> ApplyAsync(
        IReadOnlyList<ChatMessage> messages,
        IDictionary<string, string> vars,
        string paramName,
        string goal,
        IEnrichmentCache enrichmentCache,
        CancellationToken ct = default)
    {
        var prefixHash = ConversationHash.Prefix(messages);

        if (IsToolContinuation(messages)
            && await enrichmentCache.GetAsync(prefixHash, ct) is { } cached)
        {
            foreach (var (key, value) in cached)
                vars[key] = value;
            vars[paramName] = cached.GetValueOrDefault(paramName, goal);
            return new ToolContinuationState(StartAtAgent: true, OnPreAgentComplete: null);
        }

        return new ToolContinuationState(
            StartAtAgent: false,
            OnPreAgentComplete: snapshot =>
                _ = enrichmentCache.SetAsync(prefixHash, snapshot, CancellationToken.None));
    }

    // Tool-result hand-backs arrive in a user-role message on the stateless wire, so the content
    // type — not the message role — identifies a continuation.
    public static bool IsToolContinuation(IReadOnlyList<ChatMessage> messages)
        => messages.LastOrDefault()?.Contents.OfType<FunctionResultContent>().Any() == true;
}

public sealed record ToolContinuationState(
    bool StartAtAgent,
    Action<IReadOnlyDictionary<string, string>>? OnPreAgentComplete);
