using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Runtime;

// Shared dialogue history for loop(N) missions whose participants are plain LLM experts.
// Unlike Conversation, roles are assigned when a particular participant reads the transcript.
public sealed class SpeakerTranscript
{
    private readonly List<(string Speaker, string Text)> _turns = [];

    public IReadOnlyList<(string Speaker, string Text)> Turns => _turns;

    public void Add(string speaker, string text) => _turns.Add((speaker, text));

    public IReadOnlyList<ChatMessage> AsMessages(string self)
        => _turns.Select(turn => new ChatMessage(
            turn.Speaker == self ? ChatRole.Assistant : ChatRole.User,
            turn.Text)).ToList();
}
