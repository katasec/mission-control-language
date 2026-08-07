using System.Runtime.CompilerServices;
using System.Text.Json;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Runner.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeMission.Runner.Tests;

public sealed class MissionRunHandlerTests : IDisposable
{
    private readonly string _missionDirectory =
        Path.Combine(Path.GetTempPath(), $"forge-runner-tool-turn-{Guid.NewGuid():N}");

    [Fact]
    public async Task Tool_turn_returns_calls_then_resumes_from_cached_enrichment()
    {
        var registry = await CreateRegistryAsync();
        var cache = new InMemoryEnrichmentCache();
        var runner = new ScriptedExpertRunner();
        var handler = new MissionRunHandler(
            registry,
            new EmptyArtifactStore(),
            NullLogger<MissionRunHandler>.Instance,
            cache,
            (_, _) => runner);

        var first = await handler.RunAsync(Request(UserTurn()), CancellationToken.None);

        Assert.NotNull(first);
        Assert.Empty(first.AgentText);
        Assert.False(first.Verified);
        Assert.Equal(2, first.Trace.Count);
        var call = Assert.Single(first.ToolUse!);
        Assert.Equal("call-read-1", call.Id);
        Assert.Equal("Read", call.Name);
        Assert.Equal("notes.txt", call.Arguments.GetProperty("path").GetString());
        Assert.Equal(1, runner.EnrichCalls);
        Assert.Equal(1, runner.AgentCalls);
        Assert.Equal(0, runner.VerifyCalls);
        Assert.Equal("Read", runner.AgentToolName);

        var second = await handler.RunAsync(Request(Continuation()), CancellationToken.None);

        Assert.NotNull(second);
        Assert.Null(second.ToolUse);
        Assert.True(second.Verified);
        Assert.Equal("verified answer", second.AgentText);
        Assert.Equal(1, runner.EnrichCalls);
        Assert.Equal(2, runner.AgentCalls);
        Assert.Equal(1, runner.VerifyCalls);
        Assert.True(runner.AgentSawToolResult);
        // ChatRole.Tool, not ChatRole.User: confirmed live 2026-08-08 against a real OpenAI call
        // that embedding tool_result content in a user-role message (the wire's own bookkeeping
        // role) is Anthropic-specific, not universal — OpenAI's API rejects it outright. Each
        // provider's IChatClient adapter translates ChatRole.Tool into its own wire correctly.
        Assert.Equal(ChatRole.Tool, runner.ToolResultMessageRole);
    }

    public void Dispose()
    {
        if (Directory.Exists(_missionDirectory))
            Directory.Delete(_missionDirectory, recursive: true);
    }

    private async Task<RunnerRegistry> CreateRegistryAsync()
    {
        Directory.CreateDirectory(_missionDirectory);
        await File.WriteAllTextAsync(Path.Combine(_missionDirectory, "mission.mcl"), """
            mission Task(goal) = {
              Enrich
              -> Respond
              -> Verify
            }

            output(Task)
            """);
        await File.WriteAllTextAsync(Path.Combine(_missionDirectory, "mcl.lock"), """
            version: 1
            experts:
              Enrich:
                source: experts
                path: experts/Enrich/expert.md
              Respond:
                source: experts
                path: experts/Respond/expert.md
              Verify:
                source: experts
                path: experts/Verify/expert.md
            """);
        await WriteExpertAsync("Enrich", role: null);
        await WriteExpertAsync("Respond", role: "agent");
        await WriteExpertAsync("Verify", role: null);

        return await RunnerRegistry.LoadAsync(
            [("Task", "tool-turn test mission", Path.Combine(_missionDirectory, "mission.mcl"))]);
    }

    private Task WriteExpertAsync(string name, string? role)
    {
        var directory = Path.Combine(_missionDirectory, "experts", name);
        Directory.CreateDirectory(directory);
        return File.WriteAllTextAsync(Path.Combine(directory, "expert.md"), $$"""
            ---
            name: {{name}}
            input: text
            output: text
            {{(role is null ? "" : $"role: {role}")}}
            ---
            {{name}} prompt
            """);
    }

    private static RunRequest Request(IReadOnlyList<TurnMessage> history) => new(
        MissionLabel: "Task",
        Goal: "Read notes.txt and report the result.",
        Vars: null,
        Policy: RunPolicy.Trusted,
        History: history,
        Tools:
        [
            new MissionToolDecl
            {
                Name = "Read",
                Description = "Read a file.",
                InputSchema = ParseElement("""{"type":"object","required":["path"]}""")
            }
        ]);

    private static IReadOnlyList<TurnMessage> UserTurn() =>
    [
        new TurnMessage
        {
            Role = "user",
            Content = [new TurnContent { Type = "text", Text = "Read notes.txt and report the result." }]
        }
    ];

    private static IReadOnlyList<TurnMessage> Continuation() =>
    [
        UserTurn()[0],
        new TurnMessage
        {
            Role = "assistant",
            Content =
            [
                new TurnContent
                {
                    Type = "tool_use",
                    ToolUseId = "call-read-1",
                    ToolName = "Read",
                    ToolInput = ParseElement("""{"path":"notes.txt"}""")
                }
            ]
        },
        new TurnMessage
        {
            Role = "user",
            Content =
            [
                new TurnContent
                {
                    Type = "tool_result",
                    ToolUseId = "call-read-1",
                    ToolResult = "the magic word is PLATYPUS"
                }
            ]
        }
    ];

    private static JsonElement ParseElement(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private sealed class ScriptedExpertRunner : IExpertRunner
    {
        public int EnrichCalls { get; private set; }
        public int AgentCalls { get; private set; }
        public int VerifyCalls { get; private set; }
        public bool AgentSawToolResult { get; private set; }
        public string? AgentToolName { get; private set; }
        public ChatRole? ToolResultMessageRole { get; private set; }

        public Task<StepEnvelope> RunAsync(
            ExpertDefinition expert,
            Dictionary<string, object> context,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Respond(expert, context));
        }

        public async IAsyncEnumerable<string> StreamAsync(
            ExpertDefinition expert,
            Dictionary<string, object> context,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            yield return Respond(expert, context).Text;
            await Task.CompletedTask;
        }

        private StepEnvelope Respond(ExpertDefinition expert, Dictionary<string, object> context) => expert.Name switch
        {
            "Enrich" => Enrich(),
            "Respond" => RespondAsAgent(context),
            "Verify" => Verify(),
            _ => throw new InvalidOperationException($"Unexpected expert '{expert.Name}'.")
        };

        private StepEnvelope Enrich()
        {
            EnrichCalls++;
            return new StepEnvelope("enriched context");
        }

        private StepEnvelope RespondAsAgent(Dictionary<string, object> context)
        {
            AgentCalls++;
            if (context["conversation"] is Conversation conversation)
            {
                var toolResultMessage = conversation.Messages.FirstOrDefault(message =>
                    message.Contents.OfType<FunctionResultContent>().Any());
                AgentSawToolResult |= toolResultMessage is not null;
                ToolResultMessageRole ??= toolResultMessage?.Role;
            }
            if (context.TryGetValue("tools", out var tools) && tools is IList<AITool> agentTools)
                AgentToolName = agentTools.Single().Name;

            if (AgentSawToolResult)
                return new StepEnvelope("tool result incorporated");

            context["tool_calls"] = (IReadOnlyList<FunctionCallContent>)
            [
                new FunctionCallContent(
                    "call-read-1",
                    "Read",
                    new Dictionary<string, object?> { ["path"] = "notes.txt" })
            ];
            return new StepEnvelope(string.Empty);
        }

        private StepEnvelope Verify()
        {
            VerifyCalls++;
            return new StepEnvelope("verified answer");
        }
    }

    private sealed class EmptyArtifactStore : IRunnerArtifactStore
    {
        public Task<RunArtifact> SaveAsync(RunArtifactWriteRequest request, Stream content, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<RunnerArtifactRead?> OpenAsync(string artifactId, CancellationToken ct) =>
            Task.FromResult<RunnerArtifactRead?>(null);

        public Task DeleteAsync(string artifactId, CancellationToken ct) => Task.CompletedTask;
    }
}
