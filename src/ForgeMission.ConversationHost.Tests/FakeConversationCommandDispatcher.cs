using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Messaging;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>
/// In-memory <see cref="IConversationCommandDispatcher"/> fake — the small application-level seam
/// the Task 5 spoke calls for in place of a real Azure Service Bus emulator. Records every sent
/// command; when <see cref="FailNextSend"/> is set, the next <see cref="SendAsync"/> throws once
/// instead of recording, so a test can prove the outbox retries the identical command ID after a
/// failed send rather than resending under a new one.
/// </summary>
public sealed class FakeConversationCommandDispatcher : IConversationCommandDispatcher
{
    private readonly List<(ConversationAddress Address, ConversationCommand Command)> _sent = [];
    private readonly object _gate = new();

    public bool FailNextSend { get; set; }

    public IReadOnlyList<(ConversationAddress Address, ConversationCommand Command)> Sent
    {
        get { lock (_gate) return [.. _sent]; }
    }

    public Task SendAsync(ConversationAddress address, ConversationCommand command, CancellationToken ct)
    {
        lock (_gate)
        {
            if (FailNextSend)
            {
                FailNextSend = false;
                throw new InvalidOperationException("Simulated Service Bus send failure.");
            }

            _sent.Add((address, command));
        }

        return Task.CompletedTask;
    }
}
