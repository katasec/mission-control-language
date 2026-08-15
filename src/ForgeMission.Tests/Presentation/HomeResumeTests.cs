using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bunit;
using ForgeMission.ClientRuntime.Presentation.Pages;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Tests.Presentation;

// Phase 43.16 Task 8d — Home.razor's resume UI, against a fake IClientRuntimeChannel (never a real
// HTTP call). Covers exactly the locked design: the resume banner surfaces only after a successful
// DurableConversation session setup; Home resets its own ConversationTranscript immediately before
// a successful resume call; replayed ClientRuntimeEvents (delivered through the channel's own
// Subscribe stream, the same path live events use) rebuild it.
public sealed class HomeResumeTests : BunitContext
{
    [Fact]
    public void ResumeBanner_ShowsCandidatesReturnedByTheChannel_AfterJanusWorkspaceSetup()
    {
        var conversationId = Guid.NewGuid();
        var channel = new FakeClientRuntimeChannel();
        channel.ResumeCandidates.Add(new ResumeCandidate(conversationId, "Janus", ConversationRunStatus.WaitingForTool, DateTimeOffset.UtcNow));
        Services.AddSingleton<IClientRuntimeChannel>(channel);
        var component = Render<Home>();

        SetUpJanusWorkspace(component, "/workspace/a");

        Assert.Contains("Resume a conversation", component.Markup);
        Assert.Contains("Janus", component.Markup);
    }

    [Fact]
    public void ClickingAResumeCandidate_SendsResumeConversationRequest_WithTheSessionAndConversationId()
    {
        var conversationId = Guid.NewGuid();
        var channel = new FakeClientRuntimeChannel();
        channel.ResumeCandidates.Add(new ResumeCandidate(conversationId, "Janus", ConversationRunStatus.WaitingForTool, DateTimeOffset.UtcNow));
        Services.AddSingleton<IClientRuntimeChannel>(channel);
        var component = Render<Home>();
        SetUpJanusWorkspace(component, "/workspace/a");

        component.WaitForElement(".resume-candidate").Click();

        var resumeRequest = Assert.Single(channel.Requests.OfType<ResumeConversationRequest>());
        Assert.Equal(channel.SessionSetupResponse.SessionId, resumeRequest.SessionId);
        Assert.Equal(conversationId, resumeRequest.ConversationId);
    }

    [Fact]
    public void Resume_ResetsTheTranscriptImmediately_BeforeAnyReplayedEventArrives()
    {
        var conversationId = Guid.NewGuid();
        var channel = new FakeClientRuntimeChannel();
        channel.ResumeCandidates.Add(new ResumeCandidate(conversationId, "Janus", ConversationRunStatus.WaitingForTool, DateTimeOffset.UtcNow));
        Services.AddSingleton<IClientRuntimeChannel>(channel);
        var component = Render<Home>();
        SetUpJanusWorkspace(component, "/workspace/a");

        // Seed the transcript with a prior conversation's content, delivered the same way any
        // live event would be — through the channel's own Subscribe stream.
        var priorEvent = NewUserMessageEvent(Guid.NewGuid(), "An earlier conversation.");
        channel.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ConversationEvent, channel.SessionSetupResponse.SessionId, Conversation: priorEvent));
        component.WaitForAssertion(() => Assert.Contains("An earlier conversation.", component.Markup));

        component.WaitForElement(".resume-candidate").Click();

        // The reset happens synchronously before the (fake, immediately-completing) resume call —
        // by the time Click() returns, the old entry is already gone and no new one has arrived.
        Assert.DoesNotContain("An earlier conversation.", component.Markup);
    }

    [Fact]
    public void Resume_RebuildsTheTranscript_FromReplayedClientRuntimeEvents()
    {
        var conversationId = Guid.NewGuid();
        var channel = new FakeClientRuntimeChannel();
        channel.ResumeCandidates.Add(new ResumeCandidate(conversationId, "Janus", ConversationRunStatus.WaitingForTool, DateTimeOffset.UtcNow));
        Services.AddSingleton<IClientRuntimeChannel>(channel);
        var component = Render<Home>();
        SetUpJanusWorkspace(component, "/workspace/a");

        component.WaitForElement(".resume-candidate").Click();

        var replayedEvent = NewUserMessageEvent(conversationId, "Implement a rate limiter.");
        channel.Publish(new ClientRuntimeEvent(ClientRuntimeEventKind.ConversationEvent, channel.SessionSetupResponse.SessionId, Conversation: replayedEvent));

        component.WaitForAssertion(() => Assert.Contains("Implement a rate limiter.", component.Markup));
    }

    private static void SetUpJanusWorkspace(IRenderedComponent<Home> component, string workspaceRoot)
    {
        component.Find(".composer-plus").Click();
        component.Find(".add-folder-menu input").Input(workspaceRoot);
        component.Find(".menu-confirm").Click();

        component.Find(".mission-trigger").Click();
        component.WaitForElement(".mission-item");
        var janusItem = component.FindAll(".mission-item")
            .Single(item => item.TextContent.Contains("Janus", StringComparison.Ordinal));
        janusItem.Click();
        component.WaitForAssertion(() => Assert.Contains("Janus", component.Find(".mission-trigger").TextContent));
    }

    private static ConversationEvent NewUserMessageEvent(Guid conversationId, string text) => new(
        Guid.NewGuid(), 1, conversationId, Guid.NewGuid(), 1,
        ConversationEventKind.UserMessage, ConversationParticipant.User, null, text,
        Reason: null, Approval: null, ToolRequest: null, ToolResult: null, Artifact: null,
        RunStatus: null, OccurredAtUtc: DateTimeOffset.UtcNow);

    private sealed class FakeClientRuntimeChannel : IClientRuntimeChannel
    {
        public SessionSetupResponse SessionSetupResponse { get; } = new("fake-session-1", []);
        public List<ResumeCandidate> ResumeCandidates { get; } = [];
        public List<object> Requests { get; } = [];

        private readonly Channel<ClientRuntimeEvent> _events = System.Threading.Channels.Channel.CreateUnbounded<ClientRuntimeEvent>();

        public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
        {
            Requests.Add(request!);
            object response = request switch
            {
                SessionSetupRequest => SessionSetupResponse,
                ResumeCandidatesRequest => new ResumeCandidatesResponse(ResumeCandidates),
                ResumeConversationRequest resumeRequest =>
                    new ResumeConversationResponse(resumeRequest.ConversationId, ConversationRunStatus.WaitingForTool),
                _ => throw new InvalidOperationException($"Unexpected request type: {typeof(TRequest).Name}"),
            };
            return Task.FromResult((TResponse)response);
        }

        public async IAsyncEnumerable<ClientRuntimeEvent> Subscribe([EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var evt in _events.Reader.ReadAllAsync(ct))
                yield return evt;
        }

        public void Publish(ClientRuntimeEvent evt) => _events.Writer.TryWrite(evt);
    }
}
