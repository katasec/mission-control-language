using System.Runtime.CompilerServices;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;
using ForgeMission.Parser;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Runtime;

// End-to-end smoke test for AgenticSession (Phase 43.1 task 4): no external `claude` CLI, no
// `forge serve`, no mocked tool execution — the model is scripted (as every other test in this
// codebase scripts the LLM), but Read/Edit/Bash run for real against a real temp-directory
// workspace. Pass criterion is planted, tool-derived content flowing back into the final answer
// (no-false-green discipline, same as 42.3) — never a bare status field.
public sealed class AgenticSessionTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("forge-agentic-").FullName;
    private readonly CapabilityRegistry _capabilities;
    private readonly ICapabilityDispatcher _dispatcher;

    public AgenticSessionTests()
    {
        var workspace = new LocalDiskWorkspace(_workspace);
        _capabilities = new CapabilityRegistry(
        [
            new WorkspaceFileProvider(workspace),
            new WorkspaceTerminalProvider(workspace),
        ]);
        _dispatcher = DefaultDispatcher(_capabilities);
    }

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    private static readonly Program Ast = MclParser.Parse("""
        mission Task(goal) = {
            Respond
        }
        output(Task)
        """);

    private static Dictionary<string, ExpertDefinition> Experts() => new(StringComparer.Ordinal)
    {
        ["Respond"] = new("Respond", "any", "text", "You respond.", Role: "agent"),
    };

    // ------------------------------------------------------------------
    // Single tool round-trip: agent must Read a planted file to answer.
    // ------------------------------------------------------------------
    [Fact]
    public async Task SingleToolCall_ReadsPlantedFile_AnswersWithItsContent()
    {
        var notesPath = Path.Combine(_workspace, "notes.txt");
        await File.WriteAllTextAsync(notesPath, "the secret word is PLATYPUS-42");

        var client  = new ScriptedAgentClient(notesPath);
        var runner  = new PipelineRunner(new DirectExpertRunner(client));
        var session = new AgenticSession(Ast, Experts(), runner, _capabilities, _dispatcher);

        var result = await session.RunAsync(new PipelineRunOptions(
            "Task", new Dictionary<string, string> { ["goal"] = "read notes.txt and tell me the secret word" }));

        Assert.Equal(MissionStatus.Pass, result.Status);
        Assert.Null(result.ToolCalls);
        Assert.Contains("PLATYPUS-42", result.Text);
        Assert.Equal(2, client.CallCount); // 1 turn asking for the tool + 1 turn answering
    }

    // ------------------------------------------------------------------
    // Multi-tool round-trip: Edit a planted file, then Bash-cat it back — proves the loop
    // chains more than one continuation and the growing Conversation carries prior results.
    // ------------------------------------------------------------------
    [Fact]
    public async Task MultiToolCall_EditThenBash_ChainsAcrossTwoContinuations()
    {
        var notesPath = Path.Combine(_workspace, "notes.txt");
        await File.WriteAllTextAsync(notesPath, "status: before-XYZ");
        var catCommand = OperatingSystem.IsWindows() ? "type notes.txt" : "cat notes.txt";

        var client  = new MultiToolScriptedClient(notesPath, catCommand);
        var runner  = new PipelineRunner(new DirectExpertRunner(client));
        var session = new AgenticSession(Ast, Experts(), runner, _capabilities, _dispatcher);

        var result = await session.RunAsync(new PipelineRunOptions(
            "Task", new Dictionary<string, string> { ["goal"] = "update the status then read it back" }));

        Assert.Equal(MissionStatus.Pass, result.Status);
        Assert.Null(result.ToolCalls);
        Assert.Contains("after-PLATYPUS", result.Text);
        Assert.Equal(3, client.CallCount); // Edit request + Bash request + final answer

        // The Edit really landed on disk — not just echoed back by the scripted model.
        Assert.Equal("status: after-PLATYPUS", await File.ReadAllTextAsync(notesPath));
    }

    // ------------------------------------------------------------------
    // A capability policy denial must not execute the tool, and the model must see the denial
    // (not a crash, not a silent skip).
    // ------------------------------------------------------------------
    [Fact]
    public async Task PolicyDenies_ToolNeverExecutes_ModelSeesDenial()
    {
        var notesPath = Path.Combine(_workspace, "notes.txt");
        await File.WriteAllTextAsync(notesPath, "should not be read");

        var client  = new ScriptedAgentClient(notesPath);
        var runner  = new PipelineRunner(new DirectExpertRunner(client));
        var dispatcher = new CapabilityDispatcher(_capabilities,
            new FixedOutcomeAuthorizer(AuthorizationOutcome.AutoDenied), new InMemoryCapabilityAuditLog());
        var session = new AgenticSession(Ast, Experts(), runner, _capabilities, dispatcher);

        var result = await session.RunAsync(new PipelineRunOptions(
            "Task", new Dictionary<string, string> { ["goal"] = "read notes.txt" }));

        Assert.Equal(MissionStatus.Pass, result.Status);
        Assert.Contains("denied", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("should not be read", result.Text);
    }

    private static ICapabilityDispatcher DefaultDispatcher(CapabilityRegistry capabilities) =>
        new CapabilityDispatcher(capabilities, new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default),
            new InMemoryCapabilityAuditLog());

    private sealed class FixedOutcomeAuthorizer(AuthorizationOutcome outcome) : ICapabilityAuthorizer
    {
        public Task<AuthorizationOutcome> AuthorizeAsync(string capabilityName, object request, CancellationToken ct) =>
            Task.FromResult(outcome);
    }

    // ------------------------------------------------------------------
    // Fakes — the model is scripted (every test in this codebase scripts the LLM); tool
    // execution below runs through the REAL AgenticSession -> ToolExecutorRegistry -> filesystem.
    // ------------------------------------------------------------------

    private sealed class ScriptedAgentClient(string plantedPath) : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            CallCount++;
            var toolResults = messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().ToList();

            if (toolResults.Count == 0)
            {
                var call = new FunctionCallContent("call_read_1", "Read",
                    new Dictionary<string, object?> { ["file_path"] = plantedPath });
                return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, [call])]));
            }

            var toolText = toolResults[0].Result?.ToString() ?? string.Empty;
            return Task.FromResult(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, $"The file said: {toolText}")]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
            => throw new NotSupportedException("AgenticSession does not stream.");

        public void Dispose() { }
        public object? GetService(Type serviceType, object? key = null) => null;
    }

    private sealed class MultiToolScriptedClient(string filePath, string catCommand) : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            CallCount++;
            var toolResults = messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().ToList();

            ChatMessage reply = toolResults.Count switch
            {
                0 => new(ChatRole.Assistant, [new FunctionCallContent("call_edit_1", "Edit",
                        new Dictionary<string, object?>
                        {
                            ["file_path"]  = filePath,
                            ["old_string"] = "before-XYZ",
                            ["new_string"] = "after-PLATYPUS",
                        })]),
                1 => new(ChatRole.Assistant, [new FunctionCallContent("call_bash_1", "Bash",
                        new Dictionary<string, object?> { ["command"] = catCommand })]),
                _ => new(ChatRole.Assistant, $"Done — file now reads: {toolResults[^1].Result}"),
            };

            return Task.FromResult(new ChatResponse([reply]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
            => throw new NotSupportedException("AgenticSession does not stream.");

        public void Dispose() { }
        public object? GetService(Type serviceType, object? key = null) => null;
    }
}
