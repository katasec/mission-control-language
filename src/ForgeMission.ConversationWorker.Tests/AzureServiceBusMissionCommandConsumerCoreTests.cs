using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationWorker.Janus;
using ForgeMission.ConversationWorker.Messaging;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Runtime;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Parser;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeMission.ConversationWorker.Tests;

/// <summary>
/// Via the narrow <c>InternalsVisibleTo</c> friend declaration (Phase 43.16 Task 8c), calls
/// <see cref="AzureServiceBusMissionCommandConsumer.ProcessCommandCoreAsync"/> directly with
/// injected session-load/save delegates — proving structurally that unaddressable input never
/// touches session state or the processor, without needing an uninstantiable real
/// <c>ProcessSessionMessageEventArgs</c>. Uses its own local synthetic mission source/experts, not
/// <c>MissionCommandProcessorTests</c>'s (private to that class, per the Task 8b review
/// correction).
/// </summary>
public class AzureServiceBusMissionCommandConsumerCoreTests
{
    private const string MissionSource = """
        mission Negotiate(task) loop(5) = {
            Proposer using implementer
            -> Approver using implementer
        }

        mission Implement(plan) = {
            Implementer using implementer
        }
        """;

    private static Dictionary<string, ExpertDefinition> Experts() => new(StringComparer.Ordinal)
    {
        ["Proposer"] = new("Proposer", "in", "out", "prompt", Role: ""),
        ["Approver"] = new("Approver", "in", "out", "prompt", Role: "judge"),
        ["Implementer"] = new("Implementer", "in", "out", "prompt", Role: "agent"),
    };

    private static ServiceBusReceivedMessage RawMessage(string body, string? sessionId = null, string? messageId = null, IDictionary<string, object>? properties = null) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: messageId ?? Guid.NewGuid().ToString("N"),
            sessionId: sessionId ?? "kind-verifier-00000000-0000-0000-0000-000000000000",
            properties: properties ?? new Dictionary<string, object> { ["tenant_id"] = "dev" });

    private static AzureServiceBusMissionCommandConsumer BuildConsumer(IExpertRunner runner)
    {
        var fakeClient = new ServiceBusClient(
            "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=fake;SharedAccessKey=ZmFrZQ==");
        var mission = new WorkerMissionContext
        {
            Ast = MclParser.Parse(MissionSource),
            Experts = Experts(),
            Runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal) { ["implementer"] = runner },
            Execution = new ExecutionConfig(),
        };
        return new AzureServiceBusMissionCommandConsumer(
            fakeClient, new ConversationServiceBusOptions(), new WorkerMissionResolver(mission, mission),
            new NoOpPublisher(), NullLogger<AzureServiceBusMissionCommandConsumer>.Instance);
    }

    [Fact]
    public async Task InvalidJsonBody_NeverInvokesLoadSessionState_OrTheProcessor()
    {
        var consumer = BuildConsumer(new ThrowingExpertRunner());
        var message = RawMessage("kind-verifier-8b6b3a8e-3f2b-4a9f-8f7b-2f7c1a2b3c4d");

        await consumer.ProcessCommandCoreAsync(
            message,
            loadSessionAsync: _ => throw new InvalidOperationException("loadSessionAsync must not be invoked for unaddressable input."),
            saveSessionAsync: (_, _) => throw new InvalidOperationException("saveSessionAsync must not be invoked for unaddressable input."),
            CancellationToken.None);

        // No exception -- both delegates (and the throwing processor/runner) were structurally
        // never reached.
    }

    [Fact]
    public async Task MissingTenantId_NeverInvokesLoadSessionState_OrTheProcessor()
    {
        var consumer = BuildConsumer(new ThrowingExpertRunner());
        var conversationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = new ConversationCommand(
            commandId, conversationId, Guid.NewGuid(), ConversationCommandKind.StartMission, "Janus", "goal", [], null);
        var body = System.Text.Json.JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: commandId.ToString("N"), properties: new Dictionary<string, object>());

        await consumer.ProcessCommandCoreAsync(
            message,
            loadSessionAsync: _ => throw new InvalidOperationException("loadSessionAsync must not be invoked for unaddressable input."),
            saveSessionAsync: (_, _) => throw new InvalidOperationException("saveSessionAsync must not be invoked for unaddressable input."),
            CancellationToken.None);
    }

    [Fact]
    public async Task ValidCommand_LoadsSessionState_AndRunsMissionCommandProcessor()
    {
        var runner = new FakeExpertRunner((expert, _) => expert.Name switch
        {
            "Proposer" => new StepEnvelope("proposal", "pass"),
            "Approver" => new StepEnvelope("approved plan", "pass"),
            "Implementer" => new StepEnvelope("done, no tool needed", "pass"),
            _ => throw new InvalidOperationException($"Unexpected expert '{expert.Name}'."),
        });
        var consumer = BuildConsumer(runner);

        var conversationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var command = new ConversationCommand(
            commandId, conversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);
        var body = System.Text.Json.JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: commandId.ToString("N"));

        var loadInvoked = false;
        WorkerSessionState? saved = null;

        await consumer.ProcessCommandCoreAsync(
            message,
            loadSessionAsync: _ => { loadInvoked = true; return Task.FromResult<WorkerSessionState?>(null); },
            saveSessionAsync: (s, _) => { saved = s; return Task.CompletedTask; },
            CancellationToken.None);

        Assert.True(loadInvoked);
        Assert.NotNull(saved);
        Assert.Equal(WorkerSessionPhase.Terminal, saved!.Phase);
    }

    private sealed class NoOpPublisher : IConversationProgressPublisher
    {
        public Task PublishAsync(ConversationProgress progress, string tenantId, CancellationToken ct) => Task.CompletedTask;
    }
}
