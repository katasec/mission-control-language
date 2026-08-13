using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Messaging;

/// <summary>
/// The only mission-command sender in the system. A plain Host-internal service — every call here
/// is a normal C# method call from <c>ConversationGrain</c>, never a grain-interface call, so no
/// Orleans serialization concern applies to <see cref="ConversationCommand"/> crossing this seam.
/// </summary>
public interface IConversationCommandDispatcher
{
    /// <summary>Sends <paramref name="command"/> with <c>MessageId = CommandId:N</c> and
    /// <c>SessionId = ConversationId:N</c> — broker-level duplicate detection makes a genuine
    /// resend of the same command a safe no-op independent of the grain's own outbox state.</summary>
    Task SendAsync(ConversationAddress address, ConversationCommand command, CancellationToken ct);
}
