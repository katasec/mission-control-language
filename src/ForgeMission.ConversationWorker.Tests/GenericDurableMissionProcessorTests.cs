using System.Security.Cryptography;
using System.Text;
using ForgeMission.ConversationWorker.Messaging;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Runtime;
using Microsoft.Extensions.AI;

namespace ForgeMission.ConversationWorker.Tests;

public sealed class GenericDurableMissionProcessorTests
{
    [Fact]
    public async Task Authored_package_runs_without_worker_catalog_or_image_content()
    {
        var package = Package();
        var launch = new DurableMissionLaunch(Guid.NewGuid(), 1, "sha256:definition", "definition", MissionHandsProfile.NoHands, package);
        var command = new ConversationCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationCommandKind.StartMission,
            "Durable", "hello", [], null, Launch: launch);
        var published = new List<ConversationProgress>();
        var processor = new MissionCommandProcessor(new FakeExpertRunner((_, context) =>
            new StepEnvelope($"answer:{context["task"]}")));

        var state = await processor.ProcessAsync(command, "dev", null,
            (_, _) => Task.CompletedTask,
            (progress, _, _) => { published.Add(progress); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(WorkerSessionPhase.Terminal, state.Phase);
        Assert.Contains(published, progress => progress.Kind == ConversationEventKind.ParticipantMessage && progress.Text == "answer:hello");
        Assert.Equal(ConversationRunStatus.Completed, published[^1].RunStatus);
    }

    [Fact]
    public async Task Tampered_package_is_rejected_before_runner_access()
    {
        var package = Package() with { PackageHash = "sha256:tampered" };
        var command = new ConversationCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationCommandKind.StartMission,
            "Durable", "hello", [], null, Launch: new DurableMissionLaunch(Guid.NewGuid(), 1, "sha256:definition", "definition", MissionHandsProfile.NoHands, package));
        var published = new List<ConversationProgress>();
        var processor = new MissionCommandProcessor(new ThrowingExpertRunner());

        await processor.ProcessAsync(command, "dev", null, (_, _) => Task.CompletedTask,
            (progress, _, _) => { published.Add(progress); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Contains(published, progress => progress.Kind == ConversationEventKind.Error);
        Assert.Equal(ConversationRunStatus.Failed, published[^1].RunStatus);
    }

    [Fact]
    public async Task Root_tool_pause_becomes_an_opaque_hands_request_without_provider_transcript()
    {
        var package = Package(agent: true);
        var command = new ConversationCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationCommandKind.StartMission,
            "Durable", "hello", [], null, Launch: new DurableMissionLaunch(Guid.NewGuid(), 1, "sha256:definition", "definition",
                MissionHandsProfile.ProjectWorkspace, package));
        var published = new List<ConversationProgress>();
        var processor = new MissionCommandProcessor(new FakeExpertRunner((_, context) =>
        {
            context["tool_calls"] = (IReadOnlyList<FunctionCallContent>)[new FunctionCallContent("provider-call", "Read",
                new Dictionary<string, object?> { ["file_path"] = "notes.txt" })];
            return new StepEnvelope("requesting read");
        }));

        var state = await processor.ProcessAsync(command, "dev", null, (_, _) => Task.CompletedTask,
            (progress, _, _) => { published.Add(progress); return Task.CompletedTask; }, CancellationToken.None);

        var request = Assert.Single(published, progress => progress.Kind == ConversationEventKind.MissionHandsRequested);
        Assert.Equal(WorkerSessionPhase.WaitingForHands, state.Phase);
        Assert.NotNull(request.MissionHandsRequest);
        Assert.Equal("provider-call", request.MissionHandsRequest!.ProviderToolCallId);
        Assert.DoesNotContain("providerMessages", request.MissionHandsRequest.OpaqueContinuation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("originalMessages", request.MissionHandsRequest.OpaqueContinuation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exact_hands_result_resumes_the_generic_worker_once()
    {
        var package = Package(agent: true);
        var launch = new DurableMissionLaunch(Guid.NewGuid(), 1, "sha256:definition", "definition",
            MissionHandsProfile.ProjectWorkspace, package);
        var start = new ConversationCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationCommandKind.StartMission,
            "Durable", "hello", [], null, Launch: launch);
        var calls = 0;
        var processor = new MissionCommandProcessor(new FakeExpertRunner((_, context) =>
        {
            if (calls++ == 0)
            {
                context["tool_calls"] = (IReadOnlyList<FunctionCallContent>)[new FunctionCallContent("provider-call", "Read",
                    new Dictionary<string, object?> { ["file_path"] = "notes.txt" })];
                return new StepEnvelope("requesting read");
            }
            return new StepEnvelope("resumed answer");
        }));
        var initialProgress = new List<ConversationProgress>();
        var waiting = await processor.ProcessAsync(start, "dev", null, (_, _) => Task.CompletedTask,
            (progress, _, _) => { initialProgress.Add(progress); return Task.CompletedTask; }, CancellationToken.None);
        var request = Assert.Single(initialProgress, progress => progress.Kind == ConversationEventKind.MissionHandsRequested)
            .MissionHandsRequest!;
        var resume = start with
        {
            CommandId = ConversationDeterministicIds.MissionHandsContinuation(request.ToolRequestId),
            Kind = ConversationCommandKind.ContinueAfterTool,
            ToolResult = new ConversationToolResult(request.ToolRequestId, "file contents", false),
            OpaqueContinuation = request.OpaqueContinuation,
            ProviderToolCallId = request.ProviderToolCallId,
        };
        var resumedProgress = new List<ConversationProgress>();

        var terminal = await processor.ProcessAsync(resume, "dev", waiting, (_, _) => Task.CompletedTask,
            (progress, _, _) => { resumedProgress.Add(progress); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(WorkerSessionPhase.Terminal, terminal.Phase);
        Assert.Equal(2, calls);
        Assert.Contains(resumedProgress, progress => progress.Text == "resumed answer");
        Assert.Equal(ConversationRunStatus.Completed, resumedProgress[^1].RunStatus);
    }

    [Fact]
    public async Task Redelivery_after_provider_boundary_is_interrupted_without_runner_replay()
    {
        var command = new ConversationCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ConversationCommandKind.StartMission,
            "Durable", "hello", [], null, Launch: new DurableMissionLaunch(Guid.NewGuid(), 1, "sha256:definition", "definition", MissionHandsProfile.NoHands, Package()));
        var published = new List<ConversationProgress>();
        var state = new WorkerSessionState(command.CommandId, command.RunId, WorkerSessionPhase.ExecutingProvider, 0, null, Package().PackageHash);
        var processor = new MissionCommandProcessor(new ThrowingExpertRunner());

        var final = await processor.ProcessAsync(command, "dev", state, (_, _) => Task.CompletedTask,
            (progress, _, _) => { published.Add(progress); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(WorkerSessionPhase.Terminal, final.Phase);
        Assert.Equal(ConversationRunStatus.Interrupted, Assert.Single(published).RunStatus);
    }

    private static DurableMissionPackage Package(bool agent = false)
    {
        const string mission = "mission Durable(task) = {\n    Researcher\n}\n";
        var expert = "---\nname: Researcher\nkind: llm\ninput: task\noutput: answer\n" + (agent ? "role: agent\n" : "") + "---\n{{task}}";
        var entry = new DurableResolvedExpert("Researcher", "inline", "experts/Researcher/expert.md", Hash(expert), expert);
        var input = new DurableMissionPackageInput(1, "", mission, "Durable", "task", [
            new DurableResolvedExpertInput(entry.Name, entry.LockSource, entry.LockPath, entry.LockHash, entry.ExpertMarkdown)]);
        return new DurableMissionPackage(1, DurableMissionPackageValidator.ComputeHash(input), mission, "Durable", "task", [entry]);
    }

    private static string Hash(string value) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
