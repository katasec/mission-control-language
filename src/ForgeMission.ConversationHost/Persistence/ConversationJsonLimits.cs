namespace ForgeMission.ConversationHost.Persistence;

/// <summary>
/// Fixed payload bounds shared by the event store's own integrity backstop and the grain's
/// public-facing client validation, so both sides check the SAME numbers rather than two
/// independently maintained magic constants.
/// </summary>
internal static class ConversationJsonLimits
{
    /// <summary>Conservative vs. the Azure Table entity's 1 MiB row max — used both by
    /// <see cref="AzureTableConversationEventStore"/>'s own <c>AppendAsync</c> integrity guard and
    /// by <c>ConversationGrain</c>'s public-facing tool-result validation.</summary>
    internal const int MaxInlineEventJsonBytes = 48 * 1024;

    /// <summary>Leaves margin below the Azure Table-backed Orleans grain-state provider's 64 KiB
    /// cell limit. <c>ConversationCheckpoint</c> retains at most one full start-command JSON copy
    /// at a time, so this bound is enforced for both first and follow-up starts before any
    /// checkpoint write.</summary>
    internal const int MaxStartCommandJsonBytes = 32 * 1024;
}
