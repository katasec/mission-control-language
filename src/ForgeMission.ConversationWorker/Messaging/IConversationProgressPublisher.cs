using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>The only conversation-progress sender in the system. A plain Worker-internal service —
/// <paramref name="tenantId"/> is always the value preserved from the mission-command message that
/// triggered this fact, never re-derived.</summary>
public interface IConversationProgressPublisher
{
    Task PublishAsync(ConversationProgress progress, string tenantId, CancellationToken ct);
}
