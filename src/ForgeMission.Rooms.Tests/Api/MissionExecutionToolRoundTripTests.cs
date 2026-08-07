using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.Api;
using ForgeMission.Billing;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using ForgeMission.Runner;
using ForgeMission.Runner.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// API A's complete client → API → runner tool loop. The two-file plant makes every hop load-bearing:
/// the final word exists only in file B, whose path exists only in file A. The runner transport is
/// an in-process HTTP handler, but it serializes the production RunRequest/RunStreamEvent NDJSON wire.
/// </summary>
public sealed class MissionExecutionToolRoundTripTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly string _missionDirectory =
        Path.Combine(Path.GetTempPath(), $"forge-api-tool-round-trip-{Guid.NewGuid():N}");

    private BillingService Billing => fixture.Services.GetRequiredService<BillingService>();
    private IWriteStore Writes => fixture.Services.GetRequiredService<IWriteStore>();

    [Fact]
    public async Task Execute_chained_tool_round_trip_runs_enrichment_once_and_settles_only_the_terminal_turn()
    {
        var magicWord = $"XYZZY-{Guid.NewGuid():N}";
        var fileB = Path.Combine(Path.GetTempPath(), $"forge-plant-b-{Guid.NewGuid():N}.txt");
        var fileA = Path.Combine(Path.GetTempPath(), $"forge-plant-a-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(fileB, $"the magic word is {magicWord}");
        await File.WriteAllTextAsync(fileA, $"the word lives in {fileB}");

        try
        {
            var member = await NewMemberAsync();
            await Billing.GrantStartingCreditAsync(member.Id);
            var startingBalance = await Billing.GetBalanceMicroUsdAsync(member.Id);
            var scripted = new ChainedToolRunnerState();
            var runner = new MissionRunHandler(
                await CreateRegistryAsync(),
                new EmptyRunnerArtifactStore(),
                NullLogger<MissionRunHandler>.Instance,
                new InMemoryEnrichmentCache(),
                (_, usage) => new ChainedToolRunner(scripted, usage));
            var service = NewService(new LiveRunnerHandler(runner));

            var client = new MockExecuteMissionClient(
                service, new PlatformKeyContext(member.Id, startingBalance), Billing);
            var terminal = await client.RunAsync($"Read {fileA}, follow it, and tell me the magic word.", startingBalance);

            Assert.Equal([fileA, fileB], client.ReadPaths);
            Assert.Equal(2, client.ToolUseResponses);
            Assert.Contains(magicWord, terminal.Answer);
            Assert.True(terminal.Verified);
            Assert.Contains("VERIFIED:", terminal.Answer);
            Assert.Equal(1, scripted.EnrichCalls);
            Assert.Equal(3, scripted.AgentCalls);
            Assert.Equal(1, scripted.VerifyCalls);
            Assert.True(terminal.Usage.CostMicroUsd > 0);
            Assert.Equal(startingBalance - terminal.Usage.CostMicroUsd, terminal.BalanceMicroUsd);
            Assert.Equal(terminal.BalanceMicroUsd, await Billing.GetBalanceMicroUsdAsync(member.Id));
        }
        finally
        {
            File.Delete(fileA);
            File.Delete(fileB);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_missionDirectory))
            Directory.Delete(_missionDirectory, recursive: true);
    }

    private async Task<Member> NewMemberAsync() =>
        await Writes.AddMemberAsync(new Member
        {
            Kind = MemberKind.Human,
            DisplayName = "Tool Round Trip Tester",
            Issuer = "dev",
            Subject = $"sub-{Guid.NewGuid():N}",
        });

    private MissionExecutionService NewService(HttpMessageHandler runner) => new(
        new StaticMissionCatalog([new MissionInfo("WebSearch", "tool round-trip test mission")]),
        new InMemoryRunStore(),
        new EmptyApiArtifactStore(),
        new SingleClientFactory(new HttpClient(runner) { BaseAddress = new Uri("http://runner.test") }),
        Billing,
        NullLogger<MissionExecutionService>.Instance);

    private async Task<RunnerRegistry> CreateRegistryAsync()
    {
        Directory.CreateDirectory(_missionDirectory);
        await File.WriteAllTextAsync(Path.Combine(_missionDirectory, "mission.mcl"), """
            mission WebSearch(goal) = {
              Enrich
              -> Respond
              -> Verify
            }

            output(WebSearch)
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
            [("WebSearch", "tool round-trip test mission", Path.Combine(_missionDirectory, "mission.mcl"))]);
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

    private sealed class MockExecuteMissionClient(
        MissionExecutionService service,
        PlatformKeyContext principal,
        BillingService billing)
    {
        private const int MaxHops = 8;
        private readonly List<ForgeMission.Api.TurnMessage> _history = [];

        public List<string> ReadPaths { get; } = [];
        public int ToolUseResponses { get; private set; }

        public async Task<ExecuteMissionResponse> RunAsync(string prompt, long startingBalance)
        {
            var clientToken = $"tok-{Guid.NewGuid():N}";
            for (var hop = 0; hop < MaxHops; hop++)
            {
                var response = await service.ExecuteAsync(new ExecuteMission
                {
                    Mission = "websearch",
                    Input = prompt,
                    ClientToken = clientToken,
                    History = _history.Count == 0 ? null : _history,
                    Tools =
                    [
                        new ForgeMission.Api.MissionToolDecl
                        {
                            Name = "Read",
                            Description = "Reads a file.",
                            InputSchema = ParseElement("""{"type":"object","required":["file_path"]}""")
                        }
                    ]
                }, principal, CancellationToken.None);

                Assert.Null(response.ResponseStatus.ErrorCode);
                if (response.ToolUse is not { Count: > 0 } toolUse)
                    return response;

                ToolUseResponses++;
                Assert.Equal(0, response.Usage.CostMicroUsd);
                Assert.Equal(startingBalance, response.BalanceMicroUsd);
                Assert.Equal(startingBalance, await billing.GetBalanceMicroUsdAsync(principal.MemberId));

                if (_history.Count == 0)
                    _history.Add(new ForgeMission.Api.TurnMessage
                    {
                        Role = "user",
                        Content = [new ForgeMission.Api.TurnContent { Type = "text", Text = prompt }]
                    });
                _history.Add(new ForgeMission.Api.TurnMessage
                {
                    Role = "assistant",
                    Content = toolUse.Select(call => new ForgeMission.Api.TurnContent
                    {
                        Type = "tool_use",
                        ToolUseId = call.Id,
                        ToolName = call.Name,
                        ToolInput = call.Arguments,
                    }).ToList()
                });
                _history.Add(new ForgeMission.Api.TurnMessage
                {
                    Role = "user",
                    Content = toolUse.Select(call => new ForgeMission.Api.TurnContent
                    {
                        Type = "tool_result",
                        ToolUseId = call.Id,
                        ToolResult = Execute(call),
                    }).ToList()
                });
            }

            throw new InvalidOperationException($"Tool loop did not terminate in {MaxHops} hops.");
        }

        private string Execute(ForgeMission.Api.ToolUseCall call)
        {
            if (call.Name != "Read")
                return $"tool {call.Name} is not available";

            var path = call.Arguments.GetProperty("file_path").GetString()
                ?? throw new InvalidOperationException("Read call did not include file_path.");
            ReadPaths.Add(path);
            return File.ReadAllText(path);
        }
    }

    private sealed class ChainedToolRunnerState
    {
        public int EnrichCalls { get; set; }
        public int AgentCalls { get; set; }
        public int VerifyCalls { get; set; }
    }

    private sealed class ChainedToolRunner(ChainedToolRunnerState state, UsageAccumulator usage) : IExpertRunner
    {
        public Task<StepEnvelope> RunAsync(
            ExpertDefinition expert,
            Dictionary<string, object> context,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            usage.Add(new UsageDetails { InputTokenCount = 20, OutputTokenCount = 10 });
            return Task.FromResult(Respond(expert, context));
        }

        public async IAsyncEnumerable<string> StreamAsync(
            ExpertDefinition expert,
            Dictionary<string, object> context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return (await RunAsync(expert, context, ct)).Text;
        }

        private StepEnvelope Respond(ExpertDefinition expert, Dictionary<string, object> context) => expert.Name switch
        {
            "Enrich" => Enrich(),
            "Respond" => RespondAsAgent(context),
            "Verify" => Verify(context),
            _ => throw new InvalidOperationException($"Unexpected expert '{expert.Name}'.")
        };

        private StepEnvelope Enrich()
        {
            state.EnrichCalls++;
            return new StepEnvelope("enriched context");
        }

        private StepEnvelope RespondAsAgent(Dictionary<string, object> context)
        {
            state.AgentCalls++;
            var results = ConversationResults(context);
            return results.Count switch
            {
                0 => ToolCall(context, "call-read-1", PathAfter(UserPrompt(context), "Read ")),
                1 => ToolCall(context, "call-read-2", PathAfter(results[0], "lives in ")),
                _ => new StepEnvelope(results[^1]),
            };
        }

        private StepEnvelope Verify(Dictionary<string, object> context)
        {
            state.VerifyCalls++;
            return new StepEnvelope($"VERIFIED: {context["output"]}");
        }

        private static StepEnvelope ToolCall(Dictionary<string, object> context, string id, string path)
        {
            context["tool_calls"] = (IReadOnlyList<FunctionCallContent>)
            [
                new FunctionCallContent(
                    id,
                    "Read",
                    new Dictionary<string, object?> { ["file_path"] = path })
            ];
            return new StepEnvelope(string.Empty);
        }

        private static string UserPrompt(Dictionary<string, object> context) =>
            ((Conversation)context["conversation"]).Messages
                .Last(message => message.Role == ChatRole.User && message.Contents.OfType<TextContent>().Any())
                .Text;

        private static List<string> ConversationResults(Dictionary<string, object> context) =>
            ((Conversation)context["conversation"]).Messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .Select(result => result.Result?.ToString() ?? string.Empty)
                .ToList();

        private static string PathAfter(string text, string marker)
        {
            var start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            var end = text.IndexOfAny([',', '\n'], start);
            return (end < 0 ? text[start..] : text[start..end]).Trim().TrimEnd('.');
        }
    }

    private sealed class LiveRunnerHandler(MissionRunHandler runner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method != HttpMethod.Post || request.RequestUri?.AbsolutePath != "/run/stream")
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            await using var body = await request.Content!.ReadAsStreamAsync(ct);
            var runRequest = await JsonSerializer.DeserializeAsync(body, RunContractsContext.Default.RunRequest, ct)
                ?? throw new InvalidOperationException("Runner request did not deserialize.");

            var lines = new StringBuilder();
            await foreach (var evt in runner.RunStreamAsync(runRequest, ct))
                lines.AppendLine(JsonSerializer.Serialize(evt, RunContractsContext.Default.RunStreamEvent));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(lines.ToString(), Encoding.UTF8, "application/x-ndjson"),
            };
        }
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class EmptyRunnerArtifactStore : IRunnerArtifactStore
    {
        public Task<RunArtifact> SaveAsync(RunArtifactWriteRequest request, Stream content, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<RunnerArtifactRead?> OpenAsync(string artifactId, CancellationToken ct) =>
            Task.FromResult<RunnerArtifactRead?>(null);

        public Task DeleteAsync(string artifactId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class EmptyApiArtifactStore : IArtifactStore
    {
        public Task<ArtifactSaveResult> SaveAsync(
            ArtifactWriteRequest request,
            Stream content,
            PlatformKeyContext owner,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<ArtifactRead?> OpenAsync(string artifactId, PlatformKeyContext owner, CancellationToken ct) =>
            Task.FromResult<ArtifactRead?>(null);

        public Task DeleteAsync(string artifactId, PlatformKeyContext owner, CancellationToken ct) => Task.CompletedTask;
    }

    private static JsonElement ParseElement(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
