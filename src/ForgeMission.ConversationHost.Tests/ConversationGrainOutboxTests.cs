using System.Text.Json;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>
/// Phase 43.16 Task 5 — the mission-command outbox. Verification items 1 and 2: a queued command
/// is durably sent before completion; a send failure recovers by retrying the identical command ID
/// on the next activation; a normal completion never resends on a later repair pass; a matching
/// ToolResult derives exactly one deterministic ContinueAfterTool command; and an unexpected tool
/// result adds neither a new event nor a command.
/// </summary>
[Collection("Azurite")]
public class ConversationGrainOutboxTests(AzuriteFixture fixture)
{
    private static ConversationAddress NewAddress() => new("dev", Guid.NewGuid());

    private static string SerializeCommand(ConversationCommand command)
        => JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);

    private static string SerializeProgress(ConversationProgress progress)
        => JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);

    [Fact]
    public async Task AcceptCommand_SendsTheStartMissionCommand_ExactlyOnce()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));

        var sent = Assert.Single(host.Dispatcher.Sent);
        Assert.Equal(command.CommandId, sent.Command.CommandId);
        Assert.Equal(ConversationCommandKind.StartMission, sent.Command.Kind);
        Assert.Equal(address, sent.Address);
    }

    [Fact]
    public async Task AcceptCommand_SendFailure_RepairsAndRetriesTheIdenticalCommandId_OnFreshHost()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);

        await using (var host1 = await fixture.StartHostAsync())
        {
            var grain1 = host1.GetConversationGrain(address);
            host1.Dispatcher.FailNextSend = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => grain1.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command))));
            Assert.Empty(host1.Dispatcher.Sent);
        }

        // Activation alone repairs the still-pending, still-NotDispatched transition and retries
        // the send — under the same command ID, never a fresh one.
        await using var host2 = await fixture.StartHostAsync();
        var grain2 = host2.GetConversationGrain(address);
        await grain2.GetSnapshotAsync();

        var sent = Assert.Single(host2.Dispatcher.Sent);
        Assert.Equal(command.CommandId, sent.Command.CommandId);
    }

    [Fact]
    public async Task AcceptCommand_HappyPath_LeavesNothingPending_NoResendOnLaterRepairPass()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);

        await using (var host1 = await fixture.StartHostAsync())
        {
            var grain1 = host1.GetConversationGrain(address);
            await grain1.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));
            Assert.Single(host1.Dispatcher.Sent);
        }

        await using var host2 = await fixture.StartHostAsync();
        var grain2 = host2.GetConversationGrain(address);
        await grain2.GetSnapshotAsync(); // triggers OnActivateAsync's repair pass — nothing pending.

        Assert.Empty(host2.Dispatcher.Sent);
    }

    [Fact]
    public async Task MatchingToolResult_SendsOneDeterministicContinueAfterToolCommand_PreservingMissionGoalAndCapabilities()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        using var schemaDoc = JsonDocument.Parse("""{"type":"object"}""");
        var capabilities = new[] { new ConversationCapabilityDeclaration("Read", "reads a file", schemaDoc.RootElement) };
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal text", capabilities, null);

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));

        using var argsDoc = JsonDocument.Parse("""{"path":"a.txt"}""");
        var toolRequest = new ConversationToolRequest(Guid.NewGuid(), "Read", argsDoc.RootElement);
        var toolRequestedEventId = Guid.NewGuid();
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(new ConversationProgress(
            toolRequestedEventId, address.ConversationId, runId, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer, 1, null, null, null, toolRequest, null, null, null, DateTimeOffset.UtcNow))));

        var toolResultEventId = Guid.NewGuid();
        var toolResult = new ConversationToolResult(toolRequest.RequestId, "file contents", IsError: false);
        var acceptance = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(new ConversationProgress(
            toolResultEventId, address.ConversationId, runId, ConversationEventKind.ToolResult,
            ConversationParticipant.Forge, null, null, null, null, null, toolResult, null, null, DateTimeOffset.UtcNow))));

        Assert.Equal(ConversationProgressOutcome.Appended, acceptance.Outcome);

        // AcceptCommand's own StartMission send plus this ContinueAfterTool send.
        Assert.Equal(2, host.Dispatcher.Sent.Count);
        var continuation = host.Dispatcher.Sent[1].Command;
        Assert.Equal(ConversationDeterministicIds.Continuation(toolResultEventId), continuation.CommandId);
        Assert.Equal(ConversationCommandKind.ContinueAfterTool, continuation.Kind);
        Assert.Equal("Janus", continuation.MissionRef);
        Assert.Equal("goal text", continuation.Goal);
        Assert.Equal(runId, continuation.RunId);
        Assert.Single(continuation.Capabilities);
        Assert.Equal("Read", continuation.Capabilities[0].Name);
        Assert.NotNull(continuation.ToolResult);
        Assert.Equal(toolRequest.RequestId, continuation.ToolResult!.RequestId);
        Assert.Equal("file contents", continuation.ToolResult.Content);
    }

    [Fact]
    public async Task UnexpectedToolResult_IsRejected_AddsNoEventAndNoCommand()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));
        var sentAfterAccept = host.Dispatcher.Sent.Count;

        // No ToolRequested was ever recorded — ExpectedToolRequestId is null — so this ToolResult
        // matches nothing.
        var toolResult = new ConversationToolResult(Guid.NewGuid(), "unsolicited", IsError: false);
        var acceptance = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ToolResult,
            ConversationParticipant.Forge, null, null, null, null, null, toolResult, null, null, DateTimeOffset.UtcNow))));

        Assert.Equal(ConversationProgressOutcome.Rejected, acceptance.Outcome);
        Assert.Equal(sentAfterAccept, host.Dispatcher.Sent.Count); // no new send.

        // Only AcceptCommand's own UserMessage + RunStatus(Queued) — the rejected result never
        // reached PlanAppendAdvanceAsync, so it added no third event.
        var events = await grain.ReadAfterAsync(0);
        Assert.Equal(2, events.EventJson.Length);
    }
}
