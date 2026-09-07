using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;
using ForgeMission.ClientRuntime;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;
using ForgeMission.Core.Experts;
using ForgeMission.ConversationWorker.Messaging;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>
/// Grain-level integration tests against real Azurite (Phase 43.16 Task 4). Each test uses a
/// fresh, unique tenant/conversation/run ID and starts a second Host/Silo against the same
/// Azurite endpoint to prove reactivation/replay rather than reading in-memory state.
/// </summary>
[Collection("Azurite")]
public class ConversationGrainTests(AzuriteFixture fixture)
{
    private static ConversationAddress NewAddress() => new("dev", Guid.NewGuid());

    private static string SerializeCommand(ConversationCommand command)
        => JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);

    private static string SerializeProgress(ConversationProgress progress)
        => JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);

    private static List<ConversationEvent> DeserializeEvents(ConversationEventBatch batch)
        => [.. batch.EventJson.Select(json =>
            JsonSerializer.Deserialize(json, ConversationContractsJsonContext.Default.ConversationEvent)!)];

    private static ConversationSnapshot DeserializeSnapshot(ConversationSnapshotResult result)
        => JsonSerializer.Deserialize(result.SnapshotJson, ConversationContractsJsonContext.Default.ConversationSnapshot)!;

    private static async Task<ConversationCommandAcceptance> AcceptStartCommandAsync(
        IConversationGrain grain, ConversationAddress address, Guid runId, string goal = "goal")
    {
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", goal, [], null);
        var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));
        Assert.Equal(ConversationCommandOutcome.Accepted, result.Outcome);
        return result.Acceptance!;
    }

    private static DurableMissionLaunch DurableLaunch(MissionHandsProfile profile)
    {
        const string source = "mission Durable(task) = {\n    Researcher\n}\n";
        const string expert = "---\nname: Researcher\nkind: llm\ninput: task\noutput: answer\nrole: agent\n---\n{{task}}";
        var resolved = new DurableResolvedExpert("Researcher", "inline", "experts/Researcher/expert.md", Hash(expert), expert);
        var packageInput = new DurableMissionPackageInput(1, "", source, "Durable", "task", [
            new DurableResolvedExpertInput(resolved.Name, resolved.LockSource, resolved.LockPath, resolved.LockHash, resolved.ExpertMarkdown)]);
        var package = new DurableMissionPackage(1, DurableMissionPackageValidator.ComputeHash(packageInput), source,
            "Durable", "task", [resolved]);
        const string definition = "mission Example";
        return new DurableMissionLaunch(Guid.NewGuid(), 1, Hash(definition), definition, profile, package);
    }

    [Fact]
    public async Task GenericHands_UsesCanonicalConversationSequence_AndAcceptsOneCorrelatedResult()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        var runId = Guid.NewGuid();
        var launch = DurableLaunch(MissionHandsProfile.ProjectWorkspace);
        var genericStart = new ConversationCommand(Guid.NewGuid(), address.ConversationId, runId,
            ConversationCommandKind.StartMission, "Durable", "goal", [], null, Launch: launch);
        Assert.Equal(ConversationCommandOutcome.Accepted,
            (await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(genericStart)))).Outcome);
        var invalidLaunch = launch with { DefinitionHash = "sha256:deadbeef" };
        var invalidAttachment = new AttachMissionHandsRequest(address.ConversationId, Guid.NewGuid(), Guid.NewGuid(), invalidLaunch);
        Assert.False((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            invalidAttachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).Accepted);
        var firstAttachment = new AttachMissionHandsRequest(address.ConversationId, Guid.NewGuid(), Guid.NewGuid(), launch);
        Assert.True((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            firstAttachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).Accepted);
        Assert.True((await grain.DetachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new DetachMissionHandsRequest(address.ConversationId, firstAttachment.AttachmentId),
            ConversationContractsJsonContext.Default.DetachMissionHandsRequest)))).Accepted);

        var turnAttemptId = genericStart.CommandId;
        var request = new MissionToolRequest(ConversationDeterministicIds.MissionHandsRequest(address.ConversationId, turnAttemptId, 0),
            address.ConversationId, turnAttemptId, "root", "agent", "call", "read", JsonDocument.Parse("{}").RootElement.Clone(), "opaque");
        var forgedRequest = request with { ToolRequestId = Guid.NewGuid(), TurnAttemptId = Guid.NewGuid() };
        var forgedPause = new ConversationProgress(forgedRequest.ToolRequestId, address.ConversationId, runId,
            ConversationEventKind.MissionHandsRequested, ConversationParticipant.Forge, 1, null, null, null,
            null, null, null, null, DateTimeOffset.UtcNow, forgedRequest);
        Assert.Equal(ConversationProgressOutcome.Rejected,
            (await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(forgedPause)))).Outcome);
        // This is the Worker queue fact, not a direct Host-private request. Host accepts it only
        // because its attempt ID is the generic start command it dispatched.
        var workerPause = new ConversationProgress(request.ToolRequestId, address.ConversationId, runId,
            ConversationEventKind.MissionHandsRequested, ConversationParticipant.Forge, 1, null, null, null,
            null, null, null, null, DateTimeOffset.UtcNow, request);
        var awaiting = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(workerPause)));
        Assert.Equal(ConversationProgressOutcome.Appended, awaiting.Outcome);
        Assert.Equal(4, awaiting.Sequence);

        var freshAttachment = firstAttachment with { AttachmentId = Guid.NewGuid(), ApplicationSessionId = Guid.NewGuid() };
        Assert.True((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            freshAttachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).Accepted);
        var work = await grain.GetMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new GetMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, freshAttachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.GetMissionHandsWorkRequest)));
        var workItem = JsonSerializer.Deserialize(work.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)!;
        Assert.True(work.Accepted);
        Assert.NotNull(workItem.Request);
        Assert.Equal(request.ToolRequestId, workItem.Request!.ToolRequestId);
        Assert.Equal(request.TurnAttemptId, workItem.Request.TurnAttemptId);
        Assert.Equal(request.ToolName, workItem.Request.ToolName);
        Assert.True(JsonElement.DeepEquals(request.Arguments, workItem.Request.Arguments));
        var forgedWork = await grain.GetMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new GetMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, Guid.NewGuid()),
            ConversationContractsJsonContext.Default.GetMissionHandsWorkRequest)));
        Assert.False(forgedWork.Accepted);
        var confirmation = await grain.BeginMissionHandsConfirmationAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new BeginMissionHandsConfirmationRequest(address.ConversationId, freshAttachment.AttachmentId, request.ToolRequestId),
            ConversationContractsJsonContext.Default.BeginMissionHandsConfirmationRequest)));
        Assert.True(confirmation.Accepted);
        var awaitingConfirmation = await grain.GetMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new GetMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, freshAttachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.GetMissionHandsWorkRequest)));
        Assert.Equal(MissionHandsStatus.AwaitingToolConfirmation,
            JsonSerializer.Deserialize(awaitingConfirmation.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)!.Status);
        var claimed = await grain.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new ClaimMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, freshAttachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
        var duplicateClaim = await grain.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new ClaimMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, freshAttachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
        Assert.Equal(request.ToolRequestId,
            JsonSerializer.Deserialize(claimed.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)!.Request!.ToolRequestId);
        var duplicateClaimItem = JsonSerializer.Deserialize(duplicateClaim.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)!;
        Assert.Equal(MissionHandsStatus.InFlight, duplicateClaimItem.Status);
        Assert.Null(duplicateClaimItem.Request);
        var result = new SubmitMissionToolResultRequest(address.ConversationId, freshAttachment.AttachmentId,
            request.TurnAttemptId, Guid.NewGuid(), request.ToolRequestId, MissionToolOutcome.Succeeded, "done", null);
        var wrongAttachment = await grain.AcceptMissionHandsResultAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            result with { AttachmentId = Guid.NewGuid() }, ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest)));
        var wrongAttempt = await grain.AcceptMissionHandsResultAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            result with { TurnAttemptId = Guid.NewGuid() }, ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest)));
        Assert.False(wrongAttachment.Accepted);
        Assert.False(wrongAttempt.Accepted);
        var accepted = await grain.AcceptMissionHandsResultAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            result, ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest)));
        var replay = await grain.AcceptMissionHandsResultAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            result, ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest)));
        Assert.True(accepted.Accepted);
        Assert.True(replay.Accepted);
        Assert.Equal(7, JsonSerializer.Deserialize(accepted.ResultJson, ConversationContractsJsonContext.Default.MissionHandsResult)!.AcceptedSequence);
        Assert.Equal(7, JsonSerializer.Deserialize(replay.ResultJson, ConversationContractsJsonContext.Default.MissionHandsResult)!.AcceptedSequence);
        var mismatchedReplay = await grain.AcceptMissionHandsResultAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            result with { Outcome = MissionToolOutcome.Failed, Reason = "altered" },
            ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest)));
        Assert.False(mismatchedReplay.Accepted);

        // The Host appends one canonical result before it emits exactly one deterministic
        // generic continuation. A replay cannot enqueue a second provider resume.
        var continuation = Assert.Single(host.Dispatcher.Sent,
            sent => sent.Command.Kind == ConversationCommandKind.ContinueAfterTool).Command;
        Assert.Equal(ConversationDeterministicIds.MissionHandsContinuation(request.ToolRequestId), continuation.CommandId);
        Assert.Equal(runId, continuation.RunId);
        Assert.Equal(launch.Package!.PackageHash, continuation.Launch!.Package!.PackageHash);
        Assert.Equal(request.OpaqueContinuation, continuation.OpaqueContinuation);
        Assert.Equal(request.ProviderToolCallId, continuation.ProviderToolCallId);

        var events = DeserializeEvents(await grain.ReadAfterAsync(0));
        Assert.Equal([1L, 2L, 3L, 4L, 5L, 6L, 7L], events.Select(@event => @event.Sequence));
        Assert.Equal(ConversationEventKind.MissionHandsRequested, events[2].Kind);
        Assert.Equal(ConversationEventKind.MissionHandsAwaiting, events[3].Kind);
        Assert.Equal(ConversationEventKind.MissionHandsAwaitingToolConfirmation, events[4].Kind);
        Assert.Equal(ConversationEventKind.MissionHandsInFlight, events[5].Kind);
        Assert.Equal(ConversationEventKind.MissionHandsResult, events[6].Kind);
    }

    [Fact]
    public async Task Worker_pause_host_result_one_continuation_and_worker_resume_form_one_chain()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        var launch = DurableLaunch(MissionHandsProfile.ProjectWorkspace);
        var projectId = Guid.NewGuid();
        Assert.Equal(ConversationCommandOutcome.Accepted, (await grain.AcceptProjectMissionContainerCreateAsync(
            new ConversationProjectMissionCreateInput(ConversationDeterministicIds.ProjectMissionContainerCreate(projectId), projectId, "goal"))).Outcome);
        var commandId = Guid.NewGuid();
        Assert.Equal(ConversationCommandOutcome.Accepted,
            (await grain.AcceptProjectMissionRunAsync(new ConversationProjectMissionRunInput(commandId, "Durable", "input",
                JsonSerializer.Serialize(launch, ConversationContractsJsonContext.Default.DurableMissionLaunch)))).Outcome);
        var start = Assert.Single(host.Dispatcher.Sent, item => item.Command.Kind == ConversationCommandKind.StartMission).Command;
        var attachment = new AttachMissionHandsRequest(address.ConversationId, Guid.NewGuid(), Guid.NewGuid(), launch);
        Assert.True((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(attachment,
            ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).Accepted);
        var calls = 0;
        var processor = new MissionCommandProcessor(new ChainRunner((_, context) =>
        {
            if (calls++ == 0) { context["tool_calls"] = (IReadOnlyList<FunctionCallContent>)[new FunctionCallContent("call", "Read", new Dictionary<string, object?>())]; return new StepEnvelope("pause"); }
            return new StepEnvelope("done");
        }));
        var workerFacts = new List<ConversationProgress>();
        var waiting = await processor.ProcessAsync(start, "dev", null, (_, _) => Task.CompletedTask,
            (fact, _, _) => { workerFacts.Add(fact); return Task.CompletedTask; }, CancellationToken.None);
        var pause = Assert.Single(workerFacts, fact => fact.Kind == ConversationEventKind.MissionHandsRequested);
        Assert.Equal(ConversationProgressOutcome.Appended, (await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(pause)))).Outcome);
        var claim = await grain.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new ClaimMissionHandsWorkRequest(address.ConversationId, attachment.AttachmentId, attachment.ApplicationSessionId), ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
        Assert.True(claim.Accepted);
        var request = pause.MissionHandsRequest!;
        Assert.True((await grain.AcceptMissionHandsResultAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new SubmitMissionToolResultRequest(address.ConversationId, attachment.AttachmentId, request.TurnAttemptId, Guid.NewGuid(), request.ToolRequestId, MissionToolOutcome.Succeeded, "ok", null), ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest)))).Accepted);
        var continuation = Assert.Single(host.Dispatcher.Sent, item => item.Command.Kind == ConversationCommandKind.ContinueAfterTool).Command;
        var resumed = new List<ConversationProgress>();
        var terminal = await processor.ProcessAsync(continuation, "dev", waiting, (_, _) => Task.CompletedTask,
            (fact, _, _) => { resumed.Add(fact); return Task.CompletedTask; }, CancellationToken.None);
        Assert.Equal(WorkerSessionPhase.Terminal, terminal.Phase);
        Assert.Equal(2, calls);
        Assert.Equal(ConversationRunStatus.Completed, resumed[^1].RunStatus);
    }

    private sealed class ChainRunner(Func<ExpertDefinition, Dictionary<string, object>, StepEnvelope> run) : IExpertRunner
    {
        public Task<StepEnvelope> RunAsync(ExpertDefinition expert, Dictionary<string, object> context, CancellationToken ct = default) => Task.FromResult(run(expert, context));
        public async IAsyncEnumerable<string> StreamAsync(ExpertDefinition expert, Dictionary<string, object> context, [EnumeratorCancellation] CancellationToken ct = default) { yield return run(expert, context).Text; await Task.CompletedTask; }
    }

    [Fact]
    public async Task GenericHands_CancellationTerminalizesTheHostRequest_AndRejectsLateResult()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, Guid.NewGuid());
        const string definition = "mission Cancel";
        var launch = new DurableMissionLaunch(Guid.NewGuid(), 1, Hash(definition), definition, MissionHandsProfile.ProjectWorkspace);
        var attachment = new AttachMissionHandsRequest(address.ConversationId, Guid.NewGuid(), Guid.NewGuid(), launch);
        Assert.True((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            attachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).Accepted);
        var attempt = Guid.NewGuid();
        var request = new MissionToolRequest(Guid.NewGuid(), address.ConversationId, attempt, "mission", "agent", "call", "Read",
            JsonDocument.Parse("""{"file_path":"notes.md"}""").RootElement.Clone(), "continuation");
        Assert.True((await grain.RecordMissionHandsRequestAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            request, ConversationContractsJsonContext.Default.MissionToolRequest)))).Accepted);

        var cancelled = await grain.CancelMissionHandsAttemptAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new CancelMissionHandsAttemptRequest(address.ConversationId, attachment.AttachmentId, attempt, request.ToolRequestId, "operator cancelled"),
            ConversationContractsJsonContext.Default.CancelMissionHandsAttemptRequest)));
        Assert.True(cancelled.Accepted);
        Assert.Equal(MissionHandsStatus.Cancelled,
            JsonSerializer.Deserialize(cancelled.ResultJson, ConversationContractsJsonContext.Default.MissionHandsResult)!.Status);
        var late = await grain.AcceptMissionHandsResultAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new SubmitMissionToolResultRequest(address.ConversationId, attachment.AttachmentId, attempt, Guid.NewGuid(), request.ToolRequestId,
                MissionToolOutcome.Succeeded, "must not resume", null), ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest)));
        Assert.False(late.Accepted);
        var work = await grain.GetMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new GetMissionHandsWorkRequest(address.ConversationId, attachment.AttachmentId, attachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.GetMissionHandsWorkRequest)));
        Assert.Equal(MissionHandsStatus.Cancelled,
            JsonSerializer.Deserialize(work.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)!.Status);
        var nextRequest = request with { ToolRequestId = Guid.NewGuid(), TurnAttemptId = Guid.NewGuid(), ProviderToolCallId = "next" };
        Assert.True((await grain.RecordMissionHandsRequestAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            nextRequest, ConversationContractsJsonContext.Default.MissionToolRequest)))).Accepted);
        var nextClaim = await grain.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new ClaimMissionHandsWorkRequest(address.ConversationId, attachment.AttachmentId, attachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
        Assert.Equal(nextRequest.ToolRequestId,
            JsonSerializer.Deserialize(nextClaim.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)!.Request!.ToolRequestId);
        var nextResult = await grain.AcceptMissionHandsResultAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new SubmitMissionToolResultRequest(address.ConversationId, attachment.AttachmentId, nextRequest.TurnAttemptId,
                Guid.NewGuid(), nextRequest.ToolRequestId, MissionToolOutcome.Succeeded, "next succeeds", null),
            ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest)));
        Assert.True(nextResult.Accepted);
        Assert.Equal(7, DeserializeEvents(await grain.ReadAfterAsync(0)).Count);
    }

    [Fact]
    public async Task GenericHands_ReactivationRedeliversOnlyTheExactOutstandingRequest()
    {
        var address = NewAddress();
        var attachmentId = Guid.NewGuid();
        var applicationSessionId = Guid.NewGuid();
        var attempt = Guid.NewGuid();
        var request = new MissionToolRequest(Guid.NewGuid(), address.ConversationId, attempt, "mission", "agent", "call", "Read",
            JsonDocument.Parse("""{"file_path":"notes.md"}""").RootElement.Clone(), "continuation");
        await using (var firstHost = await fixture.StartHostAsync())
        {
            var grain = firstHost.GetConversationGrain(address);
            await AcceptStartCommandAsync(grain, address, Guid.NewGuid());
            const string definition = "mission Recovery";
            var launch = new DurableMissionLaunch(Guid.NewGuid(), 1, Hash(definition), definition, MissionHandsProfile.ProjectWorkspace);
            var attachment = new AttachMissionHandsRequest(address.ConversationId, attachmentId, applicationSessionId, launch);
            Assert.True((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                attachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).Accepted);
            Assert.True((await grain.RecordMissionHandsRequestAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                request, ConversationContractsJsonContext.Default.MissionToolRequest)))).Accepted);
        }

        await using var recoveredHost = await fixture.StartHostAsync();
        var recovered = recoveredHost.GetConversationGrain(address);
        var work = await recovered.GetMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new GetMissionHandsWorkRequest(address.ConversationId, attachmentId, applicationSessionId),
            ConversationContractsJsonContext.Default.GetMissionHandsWorkRequest)));
        var item = JsonSerializer.Deserialize(work.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)!;
        Assert.True(work.Accepted);
        Assert.Equal(request.ToolRequestId, item.Request!.ToolRequestId);
        Assert.Equal(request.TurnAttemptId, item.Request.TurnAttemptId);
    }

    [Fact]
    public async Task GenericHands_ConcurrentClaimsInvokeBobOnceAndAppendOneResult()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, Guid.NewGuid());
        const string definition = "mission Claim";
        var launch = new DurableMissionLaunch(Guid.NewGuid(), 1, Hash(definition), definition, MissionHandsProfile.ProjectWorkspace);
        var attachment = new AttachMissionHandsRequest(address.ConversationId, Guid.NewGuid(), Guid.NewGuid(), launch);
        Assert.True((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            attachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).Accepted);
        using var arguments = JsonDocument.Parse("""{"file_path":"effect.txt","content":"one"}""");
        var request = new MissionToolRequest(Guid.NewGuid(), address.ConversationId, Guid.NewGuid(), "mission", "agent", "call", "Write",
            arguments.RootElement.Clone(), "continuation");
        Assert.True((await grain.RecordMissionHandsRequestAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            request, ConversationContractsJsonContext.Default.MissionToolRequest)))).Accepted);

        var claimInput = new MissionHandsJsonInput(JsonSerializer.Serialize(
            new ClaimMissionHandsWorkRequest(address.ConversationId, attachment.AttachmentId, attachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest));
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => grain.ClaimMissionHandsWorkAsync(claimInput)));
        var claimed = claims.Select(claim => JsonSerializer.Deserialize(claim.ResultJson,
            ConversationContractsJsonContext.Default.MissionHandsWorkItem)!).Where(item => item.Request is not null).ToArray();
        Assert.Single(claimed);

        var root = Path.Combine(Path.GetTempPath(), $"forge-claim-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await using var bob = ClientExecutionSession.CreateForMission(root, MissionExecutionProfile.ProjectWorkspace,
                CapabilityAuthorizationPolicy.Default, AcceptingConfirmation.Instance, CancellationToken.None);
            var execution = await bob.DispatchAsync("file", new WriteFileCapabilityRequest("effect.txt", "one"), CancellationToken.None);
            Assert.False(execution.IsError, execution.Content);
            var accepted = await grain.AcceptMissionHandsResultAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                new SubmitMissionToolResultRequest(address.ConversationId, attachment.AttachmentId, request.TurnAttemptId,
                    ConversationDeterministicIds.ClientToolResult(request.ToolRequestId), request.ToolRequestId,
                    MissionToolOutcome.Succeeded, execution.Content, null),
                ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest)));
            Assert.True(accepted.Accepted);
            Assert.Equal("one", await File.ReadAllTextAsync(Path.Combine(root, "effect.txt")));
            Assert.Single(DeserializeEvents(await grain.ReadAfterAsync(0)), @event => @event.Kind == ConversationEventKind.MissionHandsResult);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task GenericHands_FreshAttachmentWaitsForOldBobDrainBeforeExactRedelivery()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, Guid.NewGuid());
        const string definition = "mission Handoff";
        var launch = new DurableMissionLaunch(Guid.NewGuid(), 1, Hash(definition), definition, MissionHandsProfile.ProjectWorkspace);
        var oldAttachment = new AttachMissionHandsRequest(address.ConversationId, Guid.NewGuid(), Guid.NewGuid(), launch);
        Assert.True((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            oldAttachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).Accepted);
        using var args = JsonDocument.Parse("""{"file_path":"handoff.txt","content":"fresh only"}""");
        var request = new MissionToolRequest(Guid.NewGuid(), address.ConversationId, Guid.NewGuid(), "mission", "agent", "call", "Write",
            args.RootElement.Clone(), "continuation");
        Assert.True((await grain.RecordMissionHandsRequestAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            request, ConversationContractsJsonContext.Default.MissionToolRequest)))).Accepted);
        var oldClaim = await grain.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new ClaimMissionHandsWorkRequest(address.ConversationId, oldAttachment.AttachmentId, oldAttachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
        Assert.NotNull(JsonSerializer.Deserialize(oldClaim.ResultJson,
            ConversationContractsJsonContext.Default.MissionHandsWorkItem)!.Request);

        var root = Path.Combine(Path.GetTempPath(), $"forge-handoff-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var confirmation = new BlockingConfirmation();
        var oldBob = ClientExecutionSession.CreateForMission(root, MissionExecutionProfile.ProjectWorkspace,
            new CapabilityAuthorizationPolicy([new KeyValuePair<string, CapabilityAuthorizationRule>("file",
                new CapabilityAuthorizationRule(AuthorizationOutcome.RequiresUserConfirmation))]), confirmation, CancellationToken.None);
        var oldWork = oldBob.DispatchAsync("file", new WriteFileCapabilityRequest("handoff.txt", "old must not write"), CancellationToken.None);
        await confirmation.Started.Task;
        try
        {
            var freshAttachment = oldAttachment with { AttachmentId = Guid.NewGuid(), ApplicationSessionId = Guid.NewGuid() };
            var attached = await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                freshAttachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)));
            Assert.Equal(MissionHandsStatus.InFlight,
                JsonSerializer.Deserialize(attached.ResultJson, ConversationContractsJsonContext.Default.MissionHandsResult)!.Status);
            var held = await grain.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                new ClaimMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, freshAttachment.ApplicationSessionId),
                ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
            var heldItem = JsonSerializer.Deserialize(held.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)!;
            Assert.Equal(MissionHandsStatus.InFlight, heldItem.Status);
            Assert.Null(heldItem.Request);
            Assert.False(File.Exists(Path.Combine(root, "handoff.txt")));

            await oldBob.DisposeAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => oldWork);
            Assert.True((await grain.DetachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                new DetachMissionHandsRequest(address.ConversationId, oldAttachment.AttachmentId),
                ConversationContractsJsonContext.Default.DetachMissionHandsRequest)))).Accepted);

            var redelivered = await grain.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                new ClaimMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, freshAttachment.ApplicationSessionId),
                ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
            Assert.Equal(request.ToolRequestId,
                JsonSerializer.Deserialize(redelivered.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)!.Request!.ToolRequestId);
            await using var freshBob = ClientExecutionSession.CreateForMission(root, MissionExecutionProfile.ProjectWorkspace,
                CapabilityAuthorizationPolicy.Default, AcceptingConfirmation.Instance, CancellationToken.None);
            var result = await freshBob.DispatchAsync("file", new WriteFileCapabilityRequest("handoff.txt", "fresh only"), CancellationToken.None);
            Assert.False(result.IsError, result.Content);
            Assert.True((await grain.AcceptMissionHandsResultAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                new SubmitMissionToolResultRequest(address.ConversationId, freshAttachment.AttachmentId, request.TurnAttemptId,
                    ConversationDeterministicIds.ClientToolResult(request.ToolRequestId), request.ToolRequestId,
                    MissionToolOutcome.Succeeded, result.Content, null), ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest)))).Accepted);
            Assert.Equal("fresh only", await File.ReadAllTextAsync(Path.Combine(root, "handoff.txt")));
        }
        finally
        {
            await oldBob.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GenericHands_ReactivationKeepsFreshAttachmentPayloadFreeUntilOldDetach()
    {
        var address = NewAddress();
        const string definition = "mission SafeHandoffRecovery";
        var launch = new DurableMissionLaunch(Guid.NewGuid(), 1, Hash(definition), definition, MissionHandsProfile.ProjectWorkspace);
        var oldAttachment = new AttachMissionHandsRequest(address.ConversationId, Guid.NewGuid(), Guid.NewGuid(), launch);
        var freshAttachment = oldAttachment with { AttachmentId = Guid.NewGuid(), ApplicationSessionId = Guid.NewGuid() };
        var request = new MissionToolRequest(Guid.NewGuid(), address.ConversationId, Guid.NewGuid(), "mission", "agent", "call", "Read",
            JsonDocument.Parse("""{"file_path":"handoff.md"}""").RootElement.Clone(), "continuation");

        await using (var firstHost = await fixture.StartHostAsync())
        {
            var grain = firstHost.GetConversationGrain(address);
            await AcceptStartCommandAsync(grain, address, Guid.NewGuid());
            Assert.True((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                oldAttachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).Accepted);
            Assert.True((await grain.RecordMissionHandsRequestAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                request, ConversationContractsJsonContext.Default.MissionToolRequest)))).Accepted);
            var oldClaim = await grain.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                new ClaimMissionHandsWorkRequest(address.ConversationId, oldAttachment.AttachmentId, oldAttachment.ApplicationSessionId),
                ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
            Assert.NotNull(JsonSerializer.Deserialize(oldClaim.ResultJson,
                ConversationContractsJsonContext.Default.MissionHandsWorkItem)!.Request);
            Assert.Equal(MissionHandsStatus.InFlight, JsonSerializer.Deserialize((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                freshAttachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).ResultJson,
                ConversationContractsJsonContext.Default.MissionHandsResult)!.Status);
        }

        await using var recoveredHost = await fixture.StartHostAsync();
        var recovered = recoveredHost.GetConversationGrain(address);
        var held = await recovered.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new ClaimMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, freshAttachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
        var heldItem = JsonSerializer.Deserialize(held.ResultJson, ConversationContractsJsonContext.Default.MissionHandsWorkItem)!;
        Assert.Equal(MissionHandsStatus.InFlight, heldItem.Status);
        Assert.Null(heldItem.Request);

        Assert.True((await recovered.DetachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new DetachMissionHandsRequest(address.ConversationId, oldAttachment.AttachmentId),
            ConversationContractsJsonContext.Default.DetachMissionHandsRequest)))).Accepted);
        var redelivered = await recovered.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new ClaimMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, freshAttachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
        Assert.Equal(request.ToolRequestId, JsonSerializer.Deserialize(redelivered.ResultJson,
            ConversationContractsJsonContext.Default.MissionHandsWorkItem)!.Request!.ToolRequestId);
    }

    [Fact]
    public async Task GenericHands_CrashRecoveryInterruptsUnknownClaimWithoutRedelivery()
    {
        var address = NewAddress();
        const string definition = "mission InterruptedHandoff";
        var launch = new DurableMissionLaunch(Guid.NewGuid(), 1, Hash(definition), definition, MissionHandsProfile.ProjectWorkspace);
        var oldAttachment = new AttachMissionHandsRequest(address.ConversationId, Guid.NewGuid(), Guid.NewGuid(), launch);
        var freshAttachment = oldAttachment with { AttachmentId = Guid.NewGuid(), ApplicationSessionId = Guid.NewGuid() };
        var interruptedRequest = new MissionToolRequest(Guid.NewGuid(), address.ConversationId, Guid.NewGuid(), "mission", "agent", "call", "Write",
            JsonDocument.Parse("""{"file_path":"unknown.txt","content":"could have run"}""").RootElement.Clone(), "continuation");

        await using (var firstHost = await fixture.StartHostAsync())
        {
            var grain = firstHost.GetConversationGrain(address);
            await AcceptStartCommandAsync(grain, address, Guid.NewGuid());
            Assert.True((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                oldAttachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).Accepted);
            Assert.True((await grain.RecordMissionHandsRequestAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                interruptedRequest, ConversationContractsJsonContext.Default.MissionToolRequest)))).Accepted);
            var oldClaim = await grain.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                new ClaimMissionHandsWorkRequest(address.ConversationId, oldAttachment.AttachmentId, oldAttachment.ApplicationSessionId),
                ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
            Assert.NotNull(JsonSerializer.Deserialize(oldClaim.ResultJson,
                ConversationContractsJsonContext.Default.MissionHandsWorkItem)!.Request);
            Assert.Equal(MissionHandsStatus.InFlight, JsonSerializer.Deserialize((await grain.AttachMissionHandsAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
                freshAttachment, ConversationContractsJsonContext.Default.AttachMissionHandsRequest)))).ResultJson,
                ConversationContractsJsonContext.Default.MissionHandsResult)!.Status);
        }

        await using var recoveredHost = await fixture.StartHostAsync();
        var recovered = recoveredHost.GetConversationGrain(address);
        var recovery = new RecoverMissionHandsInFlightRequest(address.ConversationId, freshAttachment.AttachmentId,
            freshAttachment.ApplicationSessionId);
        var interrupted = await recovered.RecoverMissionHandsInFlightAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            recovery, ConversationContractsJsonContext.Default.RecoverMissionHandsInFlightRequest)));
        var interruptedResult = JsonSerializer.Deserialize(interrupted.ResultJson,
            ConversationContractsJsonContext.Default.MissionHandsResult)!;
        Assert.True(interrupted.Accepted);
        Assert.Equal(MissionHandsStatus.Interrupted, interruptedResult.Status);

        var status = await recovered.GetMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new GetMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, freshAttachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.GetMissionHandsWorkRequest)));
        var interruptedWork = JsonSerializer.Deserialize(status.ResultJson,
            ConversationContractsJsonContext.Default.MissionHandsWorkItem)!;
        Assert.Equal(MissionHandsStatus.Interrupted, interruptedWork.Status);
        Assert.Null(interruptedWork.Request);
        Assert.Contains("interrupted", interruptedWork.Reason, StringComparison.Ordinal);

        var noRedelivery = await recovered.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new ClaimMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, freshAttachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
        Assert.Null(JsonSerializer.Deserialize(noRedelivery.ResultJson,
            ConversationContractsJsonContext.Default.MissionHandsWorkItem)!.Request);
        var late = await recovered.AcceptMissionHandsResultAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new SubmitMissionToolResultRequest(address.ConversationId, oldAttachment.AttachmentId, interruptedRequest.TurnAttemptId,
                ConversationDeterministicIds.ClientToolResult(interruptedRequest.ToolRequestId), interruptedRequest.ToolRequestId,
                MissionToolOutcome.Succeeded, "too late", null), ConversationContractsJsonContext.Default.SubmitMissionToolResultRequest)));
        Assert.False(late.Accepted);
        var duplicateRecovery = await recovered.RecoverMissionHandsInFlightAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            recovery, ConversationContractsJsonContext.Default.RecoverMissionHandsInFlightRequest)));
        Assert.Equal(interruptedResult.AcceptedSequence, JsonSerializer.Deserialize(duplicateRecovery.ResultJson,
            ConversationContractsJsonContext.Default.MissionHandsResult)!.AcceptedSequence);
        Assert.Single(DeserializeEvents(await recovered.ReadAfterAsync(0)), @event => @event.Kind == ConversationEventKind.MissionHandsInterrupted);

        var retry = interruptedRequest with { ToolRequestId = Guid.NewGuid(), TurnAttemptId = Guid.NewGuid(), ProviderToolCallId = "retry" };
        Assert.True((await recovered.RecordMissionHandsRequestAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            retry, ConversationContractsJsonContext.Default.MissionToolRequest)))).Accepted);
        var retryClaim = await recovered.ClaimMissionHandsWorkAsync(new MissionHandsJsonInput(JsonSerializer.Serialize(
            new ClaimMissionHandsWorkRequest(address.ConversationId, freshAttachment.AttachmentId, freshAttachment.ApplicationSessionId),
            ConversationContractsJsonContext.Default.ClaimMissionHandsWorkRequest)));
        Assert.Equal(retry.ToolRequestId, JsonSerializer.Deserialize(retryClaim.ResultJson,
            ConversationContractsJsonContext.Default.MissionHandsWorkItem)!.Request!.ToolRequestId);
    }

    private static string Hash(string definition) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition))).ToLowerInvariant();

    private sealed class AcceptingConfirmation : ICapabilityConfirmationHandler
    {
        public static readonly AcceptingConfirmation Instance = new();
        public Task<bool> ConfirmAsync(CapabilityConfirmationRequest request, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class BlockingConfirmation : ICapabilityConfirmationHandler
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<bool> ConfirmAsync(CapabilityConfirmationRequest request, CancellationToken ct)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return false;
        }
    }

    // ── 1. Ordered events, fresh-Host replay ────────────────────────────────────

    [Fact]
    public async Task Command_CreatesOrderedEvents_FreshHostReactivatesAndReplaysInOrder()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        Guid commandId;
        ConversationCommandAcceptance acceptance;

        await using (var host1 = await fixture.StartHostAsync())
        {
            var grain = host1.GetConversationGrain(address);
            commandId = Guid.NewGuid();
            var command = new ConversationCommand(
                commandId, address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal text", [], null);
            var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));
            Assert.Equal(ConversationCommandOutcome.Accepted, result.Outcome);
            acceptance = result.Acceptance!;
        }

        Assert.Equal(2, acceptance.AcceptedSequence);
        Assert.Equal(ConversationRunStatus.Queued, acceptance.Status);

        await using var host2 = await fixture.StartHostAsync();
        var grain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await grain2.ReadAfterAsync(0));

        Assert.Equal(2, events.Count);
        Assert.Equal(commandId, events[0].EventId); // UserMessage's EventId is the CommandId.
        Assert.Equal(1, events[0].Sequence);
        Assert.Equal(ConversationEventKind.UserMessage, events[0].Kind);
        Assert.Equal(2, events[1].Sequence);
        Assert.Equal(ConversationEventKind.RunStatus, events[1].Kind);
        Assert.Equal(ConversationRunStatus.Queued, events[1].RunStatus);
    }

    // ── 2. Duplicate command/event IDs ───────────────────────────────────────────

    [Fact]
    public async Task RepeatingCommandId_ReturnsPriorAcceptance_NoNewEventOrSequence()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);
        var commandJson = SerializeCommand(command);

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);

        var first = await grain.AcceptCommandAsync(new ConversationCommandInput(commandJson));
        var second = await grain.AcceptCommandAsync(new ConversationCommandInput(commandJson));

        Assert.Equal(ConversationCommandOutcome.Accepted, first.Outcome);
        Assert.Equal(ConversationCommandOutcome.Accepted, second.Outcome);
        Assert.Equal(first.Acceptance!.AcceptedSequence, second.Acceptance!.AcceptedSequence);
        Assert.Equal(first.Acceptance.Status, second.Acceptance.Status);
        Assert.Equal(2, (await grain.ReadAfterAsync(0)).EventJson.Length);
    }

    [Fact]
    public async Task RepeatingCommandId_WithChangedMissionRef_IsTypedConflict()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var original = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(original)));

        // Same CommandId, but MissionRef differs — not reflected in the UserMessage event's own
        // fields at all, so this specifically proves the associated-command-JSON comparison, not
        // just the event-text check.
        var changed = original with { MissionRef = "DifferentMission" };

        var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(changed)));

        Assert.Equal(ConversationCommandOutcome.Conflict, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
        Assert.Equal(2, (await grain.ReadAfterAsync(0)).EventJson.Length); // no new event appended
    }

    [Fact]
    public async Task RepeatingCommandId_WithChangedGoal_IsTypedConflict()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var original = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "original goal", [], null);

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(original)));

        var changed = original with { Goal = "different goal" };

        var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(changed)));

        Assert.Equal(ConversationCommandOutcome.Conflict, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
        Assert.Equal(2, (await grain.ReadAfterAsync(0)).EventJson.Length); // no new event appended
    }

    [Fact]
    public async Task RepeatingProgressEventId_ReturnsAlreadyRecorded_NoNewEvent()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var progress = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        var progressJson = SerializeProgress(progress);

        var first = await grain.RecordProgressAsync(new ConversationProgressInput(progressJson));
        var second = await grain.RecordProgressAsync(new ConversationProgressInput(progressJson));

        Assert.Equal(ConversationProgressOutcome.Appended, first.Outcome);
        Assert.Equal(ConversationProgressOutcome.AlreadyRecorded, second.Outcome);
        Assert.Equal(first.Sequence, second.Sequence);
        Assert.Equal(3, (await grain.ReadAfterAsync(0)).EventJson.Length); // UserMessage + Queued + this one, no duplicate
    }

    [Fact]
    public async Task ProgressEventId_SameIdDifferentPayload_FailsLoudlyAsRejected()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var eventId = Guid.NewGuid();
        var progress1 = new ConversationProgress(
            eventId, address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        var progress2 = progress1 with { Attempt = 2 }; // same EventId, different content

        var accepted = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress1)));
        var rejected = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress2)));

        Assert.Equal(ConversationProgressOutcome.Appended, accepted.Outcome);
        Assert.Equal(ConversationProgressOutcome.Rejected, rejected.Outcome);
        Assert.NotNull(rejected.RejectionReason);
    }

    // ── 3. Crash-shaped pending-transition repair ────────────────────────────────

    [Fact]
    public async Task CrashShapedPendingTransition_RepairedOnceOnActivation_NoGapOrDuplicate()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var commandId = Guid.NewGuid();

        await using (var host1 = await fixture.StartHostAsync(
            store => new ThrowOnNthAppendEventStore(store, throwOnCallNumber: 2)))
        {
            var grain = host1.GetConversationGrain(address);
            var command = new ConversationCommand(
                commandId, address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);

            // The UserMessage append (call #1) succeeds and its PendingTransition is cleared; the
            // RunStatus(Queued) append (call #2) fails after its PendingTransition was already
            // durably written — a genuine crash-shaped dangling transition.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command))));
        }

        await using var host2 = await fixture.StartHostAsync(); // fresh Silo, healthy store
        var grain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await grain2.ReadAfterAsync(0));

        Assert.Equal(2, events.Count);
        Assert.Equal(1, events[0].Sequence);
        Assert.Equal(ConversationEventKind.UserMessage, events[0].Kind);
        Assert.Equal(2, events[1].Sequence); // repaired exactly once — no gap, no duplicate row
        Assert.Equal(ConversationEventKind.RunStatus, events[1].Kind);
        Assert.Equal(ConversationRunStatus.Queued, events[1].RunStatus);

        var snapshot = DeserializeSnapshot(await grain2.GetSnapshotAsync());
        Assert.Equal(2, snapshot.LastSequence);
        Assert.Equal(ConversationRunStatus.Queued, snapshot.Status);
    }

    // ── 3c. PendingRunStart: the literal gap between the two start facts (Task 6) ────────
    // The test above faults inside AppendAsync itself, after a Queued-side PendingTransition was
    // already durably written. This one faults strictly BEFORE that — at the Queued event's own
    // existence check inside CompletePendingRunStartAsync, which runs after the UserMessage is
    // already fully durable/advanced but before any Queued-side PendingTransition would ever be
    // written. Only PendingRunStart's own preallocated QueuedEventId/timestamp (durably written
    // before either start fact was attempted) can recover this gap.

    [Fact]
    public async Task StartPairCrash_BetweenUserMessageAndQueuedLookup_RepairedOnceOnActivation_NoGapOrDuplicate()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = new ConversationCommand(
            commandId, address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);

        await using (var host1 = await fixture.StartHostAsync(
            store => new ThrowOnNthFindByEventIdEventStore(store, throwOnCallNumber: 3)))
        {
            var grain = host1.GetConversationGrain(address);

            // Call #1: AcceptCommandAsync's own duplicate-check (not found — fresh conversation).
            // Call #2: CompletePendingRunStartAsync's UserMessage-existence check (not found; the
            // UserMessage is then appended and fully advanced for real). Call #3: the Queued
            // event's OWN existence check — throws here, strictly before any Queued-side
            // PendingTransition exists.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command))));
        }

        await using var host2 = await fixture.StartHostAsync(); // fresh Silo, healthy store
        var grain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await grain2.ReadAfterAsync(0));

        Assert.Equal(2, events.Count);
        Assert.Equal(1, events[0].Sequence);
        Assert.Equal(ConversationEventKind.UserMessage, events[0].Kind);
        Assert.Equal(2, events[1].Sequence); // repaired exactly once — no gap, no duplicate row
        Assert.Equal(ConversationEventKind.RunStatus, events[1].Kind);
        Assert.Equal(ConversationRunStatus.Queued, events[1].RunStatus);

        // The paired n + 1 duplicate-acceptance formula survived the repair: a resubmission of the
        // same CommandId still returns the identical, original acceptance, with no new event.
        var retry = await grain2.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));
        Assert.Equal(ConversationCommandOutcome.Accepted, retry.Outcome);
        Assert.Equal(2, retry.Acceptance!.AcceptedSequence);
        Assert.Equal(ConversationRunStatus.Queued, retry.Acceptance.Status);
        Assert.Equal(2, (await grain2.ReadAfterAsync(0)).EventJson.Length);
    }

    private sealed class ThrowOnNthFindByEventIdEventStore(IConversationEventStore inner, int throwOnCallNumber) : IConversationEventStore
    {
        private int _callCount;

        public Task<StoredConversationEvent?> FindByEventIdAsync(ConversationAddress address, Guid eventId, CancellationToken ct)
        {
            if (Interlocked.Increment(ref _callCount) == throwOnCallNumber)
                throw new InvalidOperationException(
                    "Simulated crash: strictly between the UserMessage's own completion and the Queued event's existence check.");
            return inner.FindByEventIdAsync(address, eventId, ct);
        }

        public Task<ConversationEvent> AppendAsync(
            ConversationAddress address, ConversationEvent @event, string? associatedCommandJson, CancellationToken ct)
            => inner.AppendAsync(address, @event, associatedCommandJson, ct);

        public IAsyncEnumerable<ConversationEvent> ReadAfterAsync(ConversationAddress address, long sequence, CancellationToken ct)
            => inner.ReadAfterAsync(address, sequence, ct);

        public Task<ConversationEvent?> ReadLatestForRunAsync(ConversationAddress address, Guid runId, CancellationToken ct)
            => inner.ReadLatestForRunAsync(address, runId, ct);
    }

    private sealed class ThrowOnNthAppendEventStore(IConversationEventStore inner, int throwOnCallNumber) : IConversationEventStore
    {
        private int _callCount;

        public Task<StoredConversationEvent?> FindByEventIdAsync(ConversationAddress address, Guid eventId, CancellationToken ct)
            => inner.FindByEventIdAsync(address, eventId, ct);

        public Task<ConversationEvent> AppendAsync(
            ConversationAddress address, ConversationEvent @event, string? associatedCommandJson, CancellationToken ct)
        {
            if (Interlocked.Increment(ref _callCount) == throwOnCallNumber)
                throw new InvalidOperationException(
                    "Simulated crash: PendingTransition was durably persisted, but AppendAsync never completed.");
            return inner.AppendAsync(address, @event, associatedCommandJson, ct);
        }

        public IAsyncEnumerable<ConversationEvent> ReadAfterAsync(ConversationAddress address, long sequence, CancellationToken ct)
            => inner.ReadAfterAsync(address, sequence, ct);

        public Task<ConversationEvent?> ReadLatestForRunAsync(ConversationAddress address, Guid runId, CancellationToken ct)
            => inner.ReadLatestForRunAsync(address, runId, ct);
    }

    // ── 3b. Crash after the Table append, at the MissionRunGrain-notification stage ─────
    // The test above faults inside AppendAsync itself, so it never exercises the "keep
    // PendingTransition until the owed notification succeeds" rule — the event was never
    // durably appended in that scenario. This one faults strictly AFTER a real, successful
    // Table append, only at the MissionRunGrain.ApplyDurableEventAsync call, proving recovery:
    // reuses the original event's sequence (no duplicate/gap), replays the owed notification,
    // and clears PendingTransition so the next transition on this conversation succeeds cleanly.

    [Fact]
    public async Task NotificationFailureAfterTableAppend_RepairsOnFreshHost_ReplaysNotificationAndClearsPending()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using (var host1 = await fixture.StartHostAsync(
            decorateGrainFactory: factory => new ThrowOnNthNotifyGrainFactory(factory, throwOnCallNumber: 3)))
        {
            var grain = host1.GetConversationGrain(address);

            // Calls #1 (UserMessage) and #2 (RunStatus Queued) notify successfully.
            await AcceptStartCommandAsync(grain, address, runId);

            // Call #3: this ParticipantStarted progress's OWN Table append succeeds for real —
            // only its MissionRunGrain notification fails, after the fact.
            var progress = new ConversationProgress(
                Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
                ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress))));

            // The event is durably in the Table despite the notification failure.
            var eventsBeforeRepair = DeserializeEvents(await grain.ReadAfterAsync(0));
            Assert.Equal(3, eventsBeforeRepair.Count);
            Assert.Equal(ConversationEventKind.ParticipantStarted, eventsBeforeRepair[2].Kind);
        }

        // Fresh Host/Silo, healthy (undecorated) grain factory — activation repairs the dangling
        // notify-only transition.
        await using var host2 = await fixture.StartHostAsync();
        var grain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await grain2.ReadAfterAsync(0));

        // Reuses the original event ID/sequence — no duplicate, no gap.
        Assert.Equal(3, events.Count);
        Assert.Equal([1L, 2L, 3L], events.Select(e => e.Sequence));
        Assert.Equal(ConversationEventKind.ParticipantStarted, events[2].Kind);

        // The owed MissionRunGrain notification was replayed during repair: ParticipantStarted
        // maps to Running, which is not MissionRunCheckpoint's zero-value default — an
        // unambiguous signal the notify actually ran (vs. merely coinciding with a default).
        var runGrain2 = host2.GetMissionRunGrain(address.TenantId, runId);
        Assert.Equal(ConversationRunStatus.Running, await runGrain2.GetStatusAsync());

        // PendingTransition was cleared: a subsequent transition on this conversation succeeds
        // cleanly at the correct next sequence, with no interference from the repaired one.
        var followUp = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.Proposer, 1, "proposal text", null, null, null, null, null, null,
            DateTimeOffset.UtcNow);
        var followUpAcceptance = await grain2.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(followUp)));

        Assert.Equal(ConversationProgressOutcome.Appended, followUpAcceptance.Outcome);
        Assert.Equal(4, followUpAcceptance.Sequence);
    }

    // Test-only IGrainFactory decorator (see AzuriteFixture.StartHostAsync's
    // decorateGrainFactory parameter) — delegates every member to the real Orleans factory
    // except IMissionRunGrain string-key resolution, which it wraps to fail the Nth
    // ApplyDurableEventAsync call across this factory's whole lifetime. Production code (Program.cs,
    // ConversationGrain, MissionRunGrain) is never touched by this seam.
    private sealed class ThrowOnNthNotifyGrainFactory(IGrainFactory inner, int throwOnCallNumber) : IGrainFactory
    {
        private int _callCount;

        public TGrainInterface GetGrain<TGrainInterface>(string primaryKey, string? grainClassNamePrefix = null)
            where TGrainInterface : Orleans.IGrainWithStringKey
        {
            var grain = inner.GetGrain<TGrainInterface>(primaryKey, grainClassNamePrefix);
            if (grain is IMissionRunGrain missionRunGrain)
                return (TGrainInterface)(object)new ThrowOnNthCallMissionRunGrain(missionRunGrain, this);
            return grain;
        }

        internal bool TryConsumeThrow() => Interlocked.Increment(ref _callCount) == throwOnCallNumber;

        public TGrainInterface GetGrain<TGrainInterface>(Guid primaryKey, string? grainClassNamePrefix = null)
            where TGrainInterface : Orleans.IGrainWithGuidKey
            => inner.GetGrain<TGrainInterface>(primaryKey, grainClassNamePrefix);

        public TGrainInterface GetGrain<TGrainInterface>(long primaryKey, string? grainClassNamePrefix = null)
            where TGrainInterface : Orleans.IGrainWithIntegerKey
            => inner.GetGrain<TGrainInterface>(primaryKey, grainClassNamePrefix);

        public TGrainInterface GetGrain<TGrainInterface>(Guid primaryKey, string keyExtension, string? grainClassNamePrefix = null)
            where TGrainInterface : Orleans.IGrainWithGuidCompoundKey
            => inner.GetGrain<TGrainInterface>(primaryKey, keyExtension, grainClassNamePrefix);

        public TGrainInterface GetGrain<TGrainInterface>(long primaryKey, string keyExtension, string? grainClassNamePrefix = null)
            where TGrainInterface : Orleans.IGrainWithIntegerCompoundKey
            => inner.GetGrain<TGrainInterface>(primaryKey, keyExtension, grainClassNamePrefix);

        public TGrainObserverInterface CreateObjectReference<TGrainObserverInterface>(Orleans.IGrainObserver obj)
            where TGrainObserverInterface : Orleans.IGrainObserver
            => inner.CreateObjectReference<TGrainObserverInterface>(obj);

        public void DeleteObjectReference<TGrainObserverInterface>(Orleans.IGrainObserver obj)
            where TGrainObserverInterface : Orleans.IGrainObserver
            => inner.DeleteObjectReference<TGrainObserverInterface>(obj);

        public Orleans.IGrain GetGrain(Type grainInterfaceType, Guid grainPrimaryKey) => inner.GetGrain(grainInterfaceType, grainPrimaryKey);
        public Orleans.IGrain GetGrain(Type grainInterfaceType, long grainPrimaryKey) => inner.GetGrain(grainInterfaceType, grainPrimaryKey);
        public Orleans.IGrain GetGrain(Type grainInterfaceType, string grainPrimaryKey) => inner.GetGrain(grainInterfaceType, grainPrimaryKey);
        public Orleans.IGrain GetGrain(Type grainInterfaceType, Guid grainPrimaryKey, string keyExtension) => inner.GetGrain(grainInterfaceType, grainPrimaryKey, keyExtension);
        public Orleans.IGrain GetGrain(Type grainInterfaceType, long grainPrimaryKey, string keyExtension) => inner.GetGrain(grainInterfaceType, grainPrimaryKey, keyExtension);

        public TGrainInterface GetGrain<TGrainInterface>(Orleans.Runtime.GrainId grainId)
            where TGrainInterface : Orleans.Runtime.IAddressable
            => inner.GetGrain<TGrainInterface>(grainId);

        public Orleans.Runtime.IAddressable GetGrain(Orleans.Runtime.GrainId grainId) => inner.GetGrain(grainId);

        public Orleans.Runtime.IAddressable GetGrain(Orleans.Runtime.GrainId grainId, Orleans.Runtime.GrainInterfaceType interfaceType)
            => inner.GetGrain(grainId, interfaceType);

        public Orleans.Runtime.IAddressable GetGrain(Type interfaceType, Orleans.Runtime.IdSpan grainKey, string grainClassNamePrefix)
            => inner.GetGrain(interfaceType, grainKey, grainClassNamePrefix);

        public Orleans.Runtime.IAddressable GetGrain(Type interfaceType, Orleans.Runtime.IdSpan grainKey)
            => inner.GetGrain(interfaceType, grainKey);
    }

    private sealed class ThrowOnNthCallMissionRunGrain(IMissionRunGrain inner, ThrowOnNthNotifyGrainFactory factory) : IMissionRunGrain
    {
        public Task ApplyDurableEventAsync(MissionRunEventInput @event)
        {
            if (factory.TryConsumeThrow())
                throw new InvalidOperationException(
                    "Simulated crash: the event Table append already succeeded, but the MissionRunGrain notification never completed.");
            return inner.ApplyDurableEventAsync(@event);
        }

        public Task<ConversationRunStatus> GetStatusAsync() => inner.GetStatusAsync();
    }

    // ── 4. MissionRunGrain activation repair ─────────────────────────────────────

    [Fact]
    public async Task ExecutingProvider_ReactivatesAsInterrupted_AndReportsThroughConversationGrain_NoProviderOrQueueCall()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using (var host1 = await fixture.StartHostAsync())
        {
            var conversationGrain = host1.GetConversationGrain(address);
            await AcceptStartCommandAsync(conversationGrain, address, runId);

            var runGrain = host1.GetMissionRunGrain(address.TenantId, runId);
            await runGrain.ApplyDurableEventAsync(new MissionRunEventInput(
                Guid.NewGuid(), runId, address.ConversationId, ConversationEventKind.ParticipantStarted, null));

            Assert.Equal(ConversationRunStatus.Running, await runGrain.GetStatusAsync());
        }

        // Fresh Host/Silo. Task 4 wires no provider/queue at all, so "no provider/queue call" is
        // structurally guaranteed — the only observation available is the durable status/transcript.
        await using var host2 = await fixture.StartHostAsync();
        var runGrain2 = host2.GetMissionRunGrain(address.TenantId, runId);

        Assert.Equal(ConversationRunStatus.Interrupted, await runGrain2.GetStatusAsync());

        var conversationGrain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await conversationGrain2.ReadAfterAsync(0));
        Assert.Contains(events, e => e.Kind == ConversationEventKind.RunStatus && e.RunStatus == ConversationRunStatus.Interrupted);
    }

    [Fact]
    public async Task WaitingForTool_RemainsWaiting_AfterReactivation()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using (var host1 = await fixture.StartHostAsync())
        {
            var runGrain = host1.GetMissionRunGrain(address.TenantId, runId);
            await runGrain.ApplyDurableEventAsync(new MissionRunEventInput(
                Guid.NewGuid(), runId, address.ConversationId, ConversationEventKind.ToolRequested, null));

            Assert.Equal(ConversationRunStatus.WaitingForTool, await runGrain.GetStatusAsync());
        }

        await using var host2 = await fixture.StartHostAsync();
        var runGrain2 = host2.GetMissionRunGrain(address.TenantId, runId);
        Assert.Equal(ConversationRunStatus.WaitingForTool, await runGrain2.GetStatusAsync());
    }

    // ── ConversationCheckpoint snapshot reflects WaitingForTool (review fix) ────

    [Fact]
    public async Task ToolRequestedProgress_UpdatesSnapshotStatusToWaitingForTool_AndSurvivesReactivation()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        using var argumentsDoc = JsonDocument.Parse("""{"path":"mission/notes.md"}""");
        var toolRequest = new ConversationToolRequest(Guid.NewGuid(), "read_file", argumentsDoc.RootElement);

        await using (var host1 = await fixture.StartHostAsync())
        {
            var grain = host1.GetConversationGrain(address);
            await AcceptStartCommandAsync(grain, address, runId);

            var progress = new ConversationProgress(
                Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ToolRequested,
                ConversationParticipant.Implementer, 1, null, null, null, toolRequest, null, null, null,
                DateTimeOffset.UtcNow);
            await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress)));

            var snapshot = DeserializeSnapshot(await grain.GetSnapshotAsync());
            Assert.Equal(ConversationRunStatus.WaitingForTool, snapshot.Status);
            Assert.Equal(toolRequest.RequestId, snapshot.ExpectedToolRequestId);
        }

        await using var host2 = await fixture.StartHostAsync();
        var grain2 = host2.GetConversationGrain(address);
        var snapshot2 = DeserializeSnapshot(await grain2.GetSnapshotAsync());

        Assert.Equal(ConversationRunStatus.WaitingForTool, snapshot2.Status);
        Assert.Equal(toolRequest.RequestId, snapshot2.ExpectedToolRequestId);
    }

    // ── 6. Tool-shaped JsonElement across a real grain call ──────────────────────

    [Fact]
    public async Task ToolShapedJsonElementPayload_CrossesGrainCall_SurvivesReactivation()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        using var argumentsDoc = JsonDocument.Parse("""{"path":"mission/notes.md","recursive":true}""");
        var toolRequest = new ConversationToolRequest(Guid.NewGuid(), "read_file", argumentsDoc.RootElement);

        await using (var host1 = await fixture.StartHostAsync())
        {
            var grain = host1.GetConversationGrain(address);
            await AcceptStartCommandAsync(grain, address, runId);

            var progress = new ConversationProgress(
                Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ToolRequested,
                ConversationParticipant.Implementer, 1, null, null, null, toolRequest, null, null, null,
                DateTimeOffset.UtcNow);

            var acceptance = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress)));
            Assert.Equal(ConversationProgressOutcome.Appended, acceptance.Outcome);
        }

        await using var host2 = await fixture.StartHostAsync();
        var grain2 = host2.GetConversationGrain(address);
        var events = DeserializeEvents(await grain2.ReadAfterAsync(0));

        var toolRequested = Assert.Single(events, e => e.Kind == ConversationEventKind.ToolRequested);
        Assert.Equal("mission/notes.md", toolRequested.ToolRequest!.Arguments.GetProperty("path").GetString());
        Assert.True(toolRequested.ToolRequest.Arguments.GetProperty("recursive").GetBoolean());
    }

    // ── 7. Follow-up commands (Task 6) ───────────────────────────────────────────

    [Fact]
    public async Task Followup_UsesPinnedCapabilities_NotClientSupplied()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        using var schemaDoc = JsonDocument.Parse("""{"type":"object"}""");
        var capabilities = new[] { new ConversationCapabilityDeclaration("read_file", "Reads a file", schemaDoc.RootElement) };

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);

        var startCommand = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", capabilities, null);
        var startResult = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(startCommand)));
        Assert.Equal(ConversationCommandOutcome.Accepted, startResult.Outcome);

        // Terminate the first run so a follow-up is allowed.
        var completed = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.RunStatus,
            ConversationParticipant.Forge, null, null, null, null, null, null, null,
            ConversationRunStatus.Completed, DateTimeOffset.UtcNow);
        var completeAcceptance = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(completed)));
        Assert.Equal(ConversationProgressOutcome.Appended, completeAcceptance.Outcome);

        var followupResult = await grain.AcceptFollowupCommandAsync(
            new ConversationFollowupCommandInput(Guid.NewGuid(), "follow-up text"));
        Assert.Equal(ConversationCommandOutcome.Accepted, followupResult.Outcome);
        Assert.NotEqual(runId, followupResult.Acceptance!.RunId); // a genuinely new run

        var followupDispatch = Assert.Single(
            host.Dispatcher.Sent, s => s.Command.Kind == ConversationCommandKind.StartMission && s.Command.Goal == "follow-up text");

        Assert.Equal(capabilities.Length, followupDispatch.Command.Capabilities.Length);
        Assert.Equal(capabilities[0].Name, followupDispatch.Command.Capabilities[0].Name);
        Assert.Equal(capabilities[0].Description, followupDispatch.Command.Capabilities[0].Description);
    }

    [Fact]
    public async Task AcceptCommand_ReusingEventIdFromDifferentKind_IsTypedConflict()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var collidingId = Guid.NewGuid();
        var participantStarted = new ConversationProgress(
            collidingId, address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        var progressAcceptance = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(participantStarted)));
        Assert.Equal(ConversationProgressOutcome.Appended, progressAcceptance.Outcome);

        var reusedIdCommand = new ConversationCommand(
            collidingId, address.ConversationId, Guid.NewGuid(), ConversationCommandKind.StartMission, "Janus", "goal", [], null);
        var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(reusedIdCommand)));

        Assert.Equal(ConversationCommandOutcome.Conflict, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task AcceptFollowup_ReusingEventIdFromDifferentKind_IsTypedConflict()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var collidingId = Guid.NewGuid();
        var participantStarted = new ConversationProgress(
            collidingId, address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(participantStarted)));

        var completed = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.RunStatus,
            ConversationParticipant.Forge, null, null, null, null, null, null, null,
            ConversationRunStatus.Completed, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(completed)));

        var result = await grain.AcceptFollowupCommandAsync(new ConversationFollowupCommandInput(collidingId, "some text"));

        Assert.Equal(ConversationCommandOutcome.Conflict, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
    }

    // ── 8. Tool results (Task 6) ─────────────────────────────────────────────────

    [Fact]
    public async Task DuplicateToolResult_ReturnsOriginalAcceptance_NoNewEventOrDispatch()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var toolRequestId = Guid.NewGuid();
        using var argsDoc = JsonDocument.Parse("""{"path":"mission/notes.md"}""");
        var toolRequestedProgress = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer, 1, null, null, null,
            new ConversationToolRequest(toolRequestId, "read_file", argsDoc.RootElement), null, null, null, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(toolRequestedProgress)));

        var input = new ConversationToolResultInput(Guid.NewGuid(), toolRequestId, "file contents", false);

        var first = await grain.AcceptToolResultAsync(input);
        var second = await grain.AcceptToolResultAsync(input);

        Assert.Equal(ConversationCommandOutcome.Accepted, first.Outcome);
        Assert.Equal(ConversationCommandOutcome.Accepted, second.Outcome);
        Assert.Equal(first.Acceptance!.AcceptedSequence, second.Acceptance!.AcceptedSequence);
        Assert.Equal(ConversationRunStatus.WaitingForTool, first.Acceptance.Status);
        Assert.Equal(ConversationRunStatus.WaitingForTool, second.Acceptance.Status);

        var events = DeserializeEvents(await grain.ReadAfterAsync(0));
        Assert.Single(events, e => e.Kind == ConversationEventKind.ToolResult);
        Assert.Single(host.Dispatcher.Sent, s => s.Command.Kind == ConversationCommandKind.ContinueAfterTool);
    }

    // ── 9. Fixed payload bounds (Task 6 correction) ──────────────────────────────

    [Fact]
    public async Task AcceptCommand_OversizedGoal_ReturnsInvalid_NoStateAdvance()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);

        var oversizedGoal = new string('x', 40_000); // pushes serialized ConversationCommand JSON past 32 KiB
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", oversizedGoal, [], null);
        var result = await grain.AcceptCommandAsync(new ConversationCommandInput(SerializeCommand(command)));

        Assert.Equal(ConversationCommandOutcome.Invalid, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
        Assert.Empty((await grain.ReadAfterAsync(0)).EventJson);
    }

    [Fact]
    public async Task AcceptToolResult_OversizedContent_ReturnsInvalid_NoStateAdvance()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var toolRequestId = Guid.NewGuid();
        using var argsDoc = JsonDocument.Parse("""{"path":"mission/notes.md"}""");
        var toolRequested = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, runId, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer, 1, null, null, null,
            new ConversationToolRequest(toolRequestId, "read_file", argsDoc.RootElement), null, null, null, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(toolRequested)));

        var eventsBefore = await grain.ReadAfterAsync(0);
        var oversizedContent = new string('y', 60_000); // pushes the derived ConversationProgress JSON past 48 KiB
        var result = await grain.AcceptToolResultAsync(new ConversationToolResultInput(Guid.NewGuid(), toolRequestId, oversizedContent, false));

        Assert.Equal(ConversationCommandOutcome.Invalid, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
        var eventsAfter = await grain.ReadAfterAsync(0);
        Assert.Equal(eventsBefore.EventJson.Length, eventsAfter.EventJson.Length);
    }

    [Fact]
    public async Task AcceptToolResult_ReusingEventIdFromDifferentKind_IsTypedConflict()
    {
        var address = NewAddress();
        var runId = Guid.NewGuid();

        await using var host = await fixture.StartHostAsync();
        var grain = host.GetConversationGrain(address);
        await AcceptStartCommandAsync(grain, address, runId);

        var collidingId = Guid.NewGuid();
        var participantStarted = new ConversationProgress(
            collidingId, address.ConversationId, runId, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(participantStarted)));

        var result = await grain.AcceptToolResultAsync(new ConversationToolResultInput(collidingId, Guid.NewGuid(), "content", false));

        Assert.Equal(ConversationCommandOutcome.Conflict, result.Outcome);
        Assert.Null(result.Acceptance);
        Assert.NotNull(result.Reason);
    }
}
