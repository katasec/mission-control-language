using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.AI;
using ForgeMission.Core.Experts;
using ForgeMission.Parser;
using ForgeMission.Core.Runtime;
using MclProgram = ForgeMission.Parser.Program;

namespace ForgeMission.Core.Adapters;

// Wraps PipelineRunner as an IChatClient so the wire servers have no knowledge of MCL.
// Two modes (Phase 42.1):
//   fullConversation: false — legacy OpenAI-wire behaviour: the last user message maps to the
//                             mission's first parameter; history is dropped.
//   fullConversation: true  — Anthropic-wire behaviour: the whole conversation is seeded into the
//                             context bag as structured objects (conversation/system), and the goal
//                             is the LAST TEXT BLOCK of the last user message (clients like the
//                             claude CLI prepend scaffolding blocks that must not masquerade as
//                             the goal — decided 2026-07-16 from live wire capture).
public sealed class MissionChatClient(
    MclProgram ast,
    Dictionary<string, ExpertDefinition> experts,
    IExpertRunner runner,
    bool fullConversation = false) : IChatClient
{
    public ChatClientMetadata Metadata => new("forge-mission", null, null);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        var runOptions = BuildOptions(messages, stepWriter: null, contentWriter: null);
        var result     = await new PipelineRunner(runner).RunAsync(ast, experts, runOptions, ct);

        if (result.Status == MissionStatus.Fail)
            throw new InvalidOperationException($"Mission failed: {result.FailReason}");

        return new ChatResponse([new ChatMessage(ChatRole.Assistant, result.Text)]);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        var runOptions = BuildOptions(messages, stepWriter: null, contentWriter: new ChannelTextWriter(channel.Writer));

        // Run the full pipeline in a background task; chunks flow through the channel
        var pipelineTask = Task.Run(async () =>
        {
            try
            {
                var result = await new PipelineRunner(runner).RunAsync(ast, experts, runOptions, ct);
                if (result.Status == MissionStatus.Fail)
                    channel.Writer.TryComplete(
                        new InvalidOperationException($"Mission failed: {result.FailReason}"));
                else
                    channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }, ct);

        await foreach (var chunk in channel.Reader.ReadAllAsync(ct))
            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);

        await pipelineTask;
    }

    public void Dispose() { }

    public object? GetService(Type serviceType, object? key = null) => null;

    // -------------------------------------------------------------------------

    private PipelineRunOptions BuildOptions(IEnumerable<ChatMessage> messages, TextWriter? stepWriter, TextWriter? contentWriter)
    {
        var mission   = ast.Declarations.OfType<MissionDeclaration>().First();
        var paramName = mission.Params.FirstOrDefault() ?? "goal";
        var goal      = fullConversation ? ExtractGoal(messages) : LastUserMessage(messages);
        var vars      = new Dictionary<string, string>(StringComparer.Ordinal) { [paramName] = goal };
        var objects   = fullConversation ? ConversationObjects(messages) : null;
        return new PipelineRunOptions(mission.Name, vars, stepWriter, contentWriter, ContextObjects: objects);
    }

    // The goal is the LAST TEXT BLOCK of the last user message, never the concatenation:
    // the claude CLI sends scaffolding blocks (system reminders, memory) before the real prompt,
    // and concatenating them poisons classify/search/guard experts downstream. The scaffolding
    // still travels in context["conversation"]. Observed regularity — re-verify on CLI bumps
    // against the checked-in wire fixture.
    public static string ExtractGoal(IEnumerable<ChatMessage> messages)
        => messages.LastOrDefault(m => m.Role == ChatRole.User)?
               .Contents.OfType<TextContent>().LastOrDefault()?.Text ?? string.Empty;

    private static Dictionary<string, object> ConversationObjects(IEnumerable<ChatMessage> messages)
    {
        var all = messages.ToList();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["conversation"] = new Conversation(all),
            ["system"]       = string.Join("\n\n", all.Where(m => m.Role == ChatRole.System).Select(m => m.Text)),
        };
    }

    private static string LastUserMessage(IEnumerable<ChatMessage> messages)
        => messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;

    // TextWriter that forwards WriteAsync calls into an unbounded channel.
    // Write(char) uses TryWrite (always succeeds on unbounded channels).
    private sealed class ChannelTextWriter(ChannelWriter<string> writer) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => writer.TryWrite(value.ToString());

        public override Task WriteAsync(string? value)
        {
            if (string.IsNullOrEmpty(value)) return Task.CompletedTask;
            return writer.WriteAsync(value).AsTask();
        }

        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken ct = default)
            => WriteAsync(buffer.ToString());
    }
}
