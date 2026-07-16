using System.Collections.Concurrent;
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
    bool fullConversation = false,
    IEnrichmentCache? enrichmentCache = null) : IChatClient
{
    private readonly IEnrichmentCache _enrichmentCache = enrichmentCache ?? new InMemoryEnrichmentCache();

    // duplicate_continuation evidence hook (42.3 task 7): counts full-conversation hashes (F)
    // seen twice within the window. Replay is deliberately NOT built in v1 — this counter is
    // the data that decides whether it ever is. Non-zero ⇒ build the §4 replay design.
    public static long DuplicateContinuations => _duplicateContinuations;
    private static long _duplicateContinuations;
    private static readonly ConcurrentDictionary<string, DateTimeOffset> _recentFullHashes = new();
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(5);

    public ChatClientMetadata Metadata => new("forge-mission", null, null);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        var runOptions = await BuildOptionsAsync(messages, stepWriter: null, contentWriter: null, options, ct);
        var result     = await new PipelineRunner(runner).RunAsync(ast, experts, runOptions, ct);

        if (result.Status == MissionStatus.Fail)
            throw new InvalidOperationException($"Mission failed: {result.FailReason}");

        return new ChatResponse([BuildReply(result)]);
    }

    // A tool-calling result carries the FunctionCallContent parts back to the wire verbatim;
    // a plain result stays a text message.
    private static ChatMessage BuildReply(MissionResult result)
    {
        if (result.ToolCalls is not { Count: > 0 } calls)
            return new ChatMessage(ChatRole.Assistant, result.Text);

        var contents = new List<AIContent>();
        if (!string.IsNullOrEmpty(result.Text)) contents.Add(new TextContent(result.Text));
        contents.AddRange(calls);
        return new ChatMessage(ChatRole.Assistant, contents);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        var runOptions = await BuildOptionsAsync(messages, stepWriter: null, contentWriter: new ChannelTextWriter(channel.Writer), null, ct);

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

    private async Task<PipelineRunOptions> BuildOptionsAsync(
        IEnumerable<ChatMessage> messages, TextWriter? stepWriter, TextWriter? contentWriter,
        ChatOptions? chatOptions, CancellationToken ct)
    {
        var mission   = ast.Declarations.OfType<MissionDeclaration>().First();
        var paramName = mission.Params.FirstOrDefault() ?? "goal";
        var goal      = fullConversation ? ExtractGoal(messages) : LastUserMessage(messages);
        var vars      = new Dictionary<string, string>(StringComparer.Ordinal) { [paramName] = goal };
        var objects   = fullConversation ? ConversationObjects(messages) : null;
        var tools     = fullConversation ? chatOptions?.Tools : null; // agent expert only — see PipelineRunner

        if (!fullConversation)
            return new PipelineRunOptions(mission.Name, vars, stepWriter, contentWriter);

        // Three-segment gate (42.3): a tool continuation resumes the agent with the pre-agent
        // output restored from the enrichment cache; a cache MISS re-runs the pre-agent segment
        // (never answer ungrounded). Fresh user turns store the snapshot for their continuations.
        var all          = messages.ToList();
        var prefixHash   = ConversationHash.Prefix(all);
        var startAtAgent = false;
        Action<IReadOnlyDictionary<string, string>>? onPreAgentComplete = null;

        CountDuplicateContinuations(all);

        if (IsToolContinuation(all) && await _enrichmentCache.GetAsync(prefixHash, ct) is { } cached)
        {
            startAtAgent = true;
            foreach (var (key, value) in cached) vars[key] = value;
            vars[paramName] = cached.GetValueOrDefault(paramName, goal); // the ORIGINAL turn's goal
        }
        else
        {
            onPreAgentComplete = snapshot =>
                _ = _enrichmentCache.SetAsync(prefixHash, snapshot, CancellationToken.None);
        }

        return new PipelineRunOptions(mission.Name, vars, stepWriter, contentWriter,
            ContextObjects: objects, Tools: tools,
            StartAtAgent: startAtAgent, OnPreAgentComplete: onPreAgentComplete);
    }

    // A tool continuation hands back a tool's output — the last message carries tool_result parts.
    private static bool IsToolContinuation(IReadOnlyList<ChatMessage> messages)
        => messages.LastOrDefault()?.Contents.OfType<FunctionResultContent>().Any() == true;

    // Task 7: observe-only. If the identical full conversation (F) arrives twice inside the
    // window, count it and log — no replay, no reject (decided 2026-07-16, §4).
    private static void CountDuplicateContinuations(IReadOnlyList<ChatMessage> messages)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, at) in _recentFullHashes)
            if (now - at >= DuplicateWindow)
                _recentFullHashes.TryRemove(key, out _);

        var fullHash = ConversationHash.Full(messages);
        if (!_recentFullHashes.TryAdd(fullHash, now))
        {
            Interlocked.Increment(ref _duplicateContinuations);
            Console.Error.WriteLine($"duplicate_continuation observed (total {DuplicateContinuations})");
        }
    }

    // The goal is the LAST TEXT BLOCK of the last REAL user turn, never the concatenation:
    // the claude CLI sends scaffolding blocks (system reminders, memory) before the real prompt,
    // and concatenating them poisons classify/search/guard experts downstream. The scaffolding
    // still travels in context["conversation"]. Tool-result hand-backs also arrive with role
    // "user" — they carry no user intent and are skipped. Observed regularity — re-verify on
    // CLI bumps against the checked-in wire fixture.
    public static string ExtractGoal(IEnumerable<ChatMessage> messages)
        => messages.LastOrDefault(m => m.Role == ChatRole.User
                   && m.Contents.OfType<TextContent>().Any()
                   && !m.Contents.OfType<FunctionResultContent>().Any())?
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
