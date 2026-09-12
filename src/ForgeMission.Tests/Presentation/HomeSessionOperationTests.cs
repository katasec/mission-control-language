using Bunit;
using ForgeMission.Presentation.Components;
using ForgeMission.Presentation.Pages;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Tests.Presentation;

/// <summary>
/// What the pre-workspace surfaces do. The startup choice and the launcher still reach the
/// Application for nothing at all until a person acts. Phase 48 makes the third route real: choosing
/// Chat with a mission sends exactly one typed action, and from then on everything the page shows
/// came from that answer or from the relayed durable stream.
/// </summary>
public sealed class HomeSessionOperationTests : BunitContext
{
    // One channel for the whole context: bUnit fixes its service provider at the first render, and
    // two of these tests render the page twice to prove a reload starts clean.
    private readonly ScriptedChannel channel = new();

    public HomeSessionOperationTests() => Services.AddSingleton<IApplicationChannel>(channel);

    [Fact]
    public void Boot_OnlyRendersTheZeroAuthorityStartChoice()
    {
        var (page, channel) = RenderHome();

        Assert.Contains("Where do you want to start?", page.Markup);
        // The launcher is a choice away, not the boot surface.
        Assert.Empty(page.FindAll(".pl-goal"));
        Assert.Empty(channel.Requests);
        Assert.DoesNotContain("Project Explorer", page.Markup);
    }

    [Fact]
    public void CreateAMission_RevealsTheUnchangedLauncher_AndStillAsksTheApplicationForNothing()
    {
        var (page, channel) = RenderHome();

        Choose(page, "Create a mission");

        Assert.Single(page.FindAll(".pl-goal"));
        // The launcher opens as it always did: the folder disclosure stays closed.
        Assert.Empty(page.FindAll(".pl-open-path"));
        Assert.Empty(channel.Requests);
    }

    [Fact]
    public void OpenAnExistingWorkspace_OpensTheLauncherWithItsFolderRowAlreadyShowing()
    {
        var (page, channel) = RenderHome();

        Choose(page, "Open an existing workspace");

        Assert.Single(page.FindAll(".pl-goal"));
        Assert.Single(page.FindAll(".pl-open-path"));
        Assert.Empty(channel.Requests);
    }

    [Fact]
    public void ChatWithAMission_SendsExactlyOneStartAction_AndRendersWhatItAnswered()
    {
        var (page, channel) = RenderHome();

        Choose(page, "Chat with a mission");

        Assert.Equal([typeof(StartMissionChatRequest)], channel.Requests.Select(request => request.GetType()));
        Assert.Equal("Amber Harbor", page.Find(".wb-brand-name").TextContent);
        Assert.Equal("New chat", page.Find(".mcp-chat-head h2").TextContent);
        Assert.Equal("PINNED · JANUS 1.4", page.Find(".mcp-chip-pinned").TextContent);
        Assert.Single(page.FindAll(".mcp-row"));
        Assert.Empty(page.FindAll(".pl-goal"));
    }

    [Fact]
    public void TheChatSurface_NeverSelectsTheMissionsSurfaceTheme()
    {
        var (page, _) = RenderHome();

        Choose(page, "Chat with a mission");

        // It is a Workbench-themed surface. Inheriting the durable Missions theme would re-skin it to a
        // palette its references never used.
        Assert.DoesNotContain("forge-desktop-dark", page.Markup);
        Assert.DoesNotContain("data-surface-theme", page.Markup);
    }

    [Fact]
    public void SendingAMessage_SubmitsOneTurnForTheOpenChat_AndAddsNoLocalBubble()
    {
        var (page, channel) = RenderHome();
        Choose(page, "Chat with a mission");

        page.Find(".mcp-composer-field").Input("Hold the cohort invite.");
        page.Find(".mcp-send").Click();

        var submitted = Assert.Single(channel.Requests.OfType<SubmitMissionChatTurnRequest>());
        Assert.Equal(ScriptedChannel.Session, submitted.SessionId);
        Assert.Equal(ScriptedChannel.Chat, submitted.ConversationId);
        Assert.Equal("Hold the cohort invite.", submitted.Text);
        Assert.NotEqual(Guid.Empty, submitted.CommandId);
        // The durable user message arrives through the stream, so nothing is invented here.
        Assert.Empty(page.FindAll(".mcp-user-bubble"));
        Assert.Equal("Message sent.", page.Find(".mcp-announce").TextContent);
    }

    [Fact]
    public async Task ARelayedEvent_IsAppliedForTheOpenChat_AndIgnoredForAnyOther()
    {
        var (page, channel) = RenderHome();
        Choose(page, "Chat with a mission");

        await channel.PublishAsync(ScriptedChannel.Event(Guid.NewGuid(), 1, "Belongs to another chat."));
        await channel.PublishAsync(ScriptedChannel.Event(ScriptedChannel.Chat, 1, "Hold the cohort invite."));

        page.WaitForAssertion(() => Assert.Equal("Hold the cohort invite.", page.Find(".mcp-user-bubble").TextContent));
        Assert.Single(page.FindAll(".mcp-user-bubble"));
    }

    [Fact]
    public void NewChat_SendsItsOwnAction_AndSelectingARowSendsOpen()
    {
        var (page, channel) = RenderHome();
        Choose(page, "Chat with a mission");

        Choose(page, "New chat");
        page.FindAll(".mcp-row").First(row => !row.ClassList.Contains("mcp-row-current")).Click();

        Assert.Single(channel.Requests.OfType<CreateMissionChatRequest>());
        Assert.Single(channel.Requests.OfType<OpenMissionChatRequest>());
    }

    [Fact]
    public void AuthorAMission_LeavesTheChatForTheUnchangedLauncher()
    {
        var (page, channel) = RenderHome();
        Choose(page, "Chat with a mission");

        Choose(page, "Author a mission");

        Assert.Single(page.FindAll(".pl-goal"));
        Assert.Empty(page.FindAll(".mcp-shell"));
        Assert.Equal([typeof(StartMissionChatRequest)], channel.Requests.Select(request => request.GetType()));
    }

    [Fact]
    public void AFreshRender_ReturnsToTheStartChoice_WithNoChatStateSurviving()
    {
        var (first, _) = RenderHome();
        Choose(first, "Chat with a mission");
        first.Find(".mcp-composer-field").Input("Something I typed before reloading.");

        // A reload rebuilds the page component, so the startup screen stays the startup screen: Forge
        // does not silently reopen the last chat.
        var (second, _) = RenderHome();

        Assert.Contains("Where do you want to start?", second.Markup);
        Assert.DoesNotContain("Something I typed before reloading.", second.Markup);
        Assert.Empty(second.FindAll(".mcp-shell"));
    }

    private (IRenderedComponent<Home> Page, ScriptedChannel Channel) RenderHome() => (Render<Home>(), channel);

    private static void Choose(IRenderedComponent<Home> page, string label) =>
        page.FindAll("button")
            .First(button => button.TextContent.Contains(label, StringComparison.Ordinal))
            .Click();

    /// <summary>Answers the four Mission Chat actions the way Application would, and refuses anything
    /// else: the launcher and the startup choice still have nothing to ask for.</summary>
    private sealed class ScriptedChannel : IApplicationChannel
    {
        internal const string Session = "managed-session";
        internal static readonly Guid Chat = Guid.Parse("55555555-5555-5555-5555-555555555555");
        internal static readonly Guid Second = Guid.Parse("66666666-6666-6666-6666-666666666666");

        private readonly System.Threading.Channels.Channel<ApplicationEvent> events =
            System.Threading.Channels.Channel.CreateUnbounded<ApplicationEvent>();

        public List<object> Requests { get; } = [];

        public Task PublishAsync(ApplicationEvent message) => events.Writer.WriteAsync(message).AsTask();

        public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
        {
            Requests.Add(request!);
            object response = request switch
            {
                StartMissionChatRequest => new StartMissionChatResponse(
                    new ProjectSession(Session, [], new ProjectSummary(Guid.NewGuid(), "Amber Harbor", "goal", "/tmp/amber")),
                    Chat, Pin, Attached, [Row(Chat, "New chat", false)], [], 0, null),
                CreateMissionChatRequest => new CreateMissionChatResponse(Second, Pin, Attached,
                    [Row(Second, "New chat", false), Row(Chat, "Earlier chat", true)], [], 0, null),
                OpenMissionChatRequest open => new OpenMissionChatResponse(open.ConversationId, Pin, Attached,
                    [Row(Second, "New chat", false), Row(Chat, "Earlier chat", true)], [], 0, null),
                SubmitMissionChatTurnRequest => new SubmitMissionChatTurnResponse(Guid.NewGuid(), 1, ConversationRunStatus.Queued, null),
                _ => throw new InvalidOperationException("The launcher must not make a call before a person acts."),
            };
            return Task.FromResult((TResponse)response);
        }

        public async IAsyncEnumerable<ApplicationEvent> Subscribe([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var message in events.Reader.ReadAllAsync(ct))
                yield return message;
        }

        internal static ApplicationEvent Event(Guid conversationId, long sequence, string text) =>
            new(ApplicationEventKind.ConversationEvent, Session, Conversation: new ConversationEvent(
                Guid.NewGuid(), 1, conversationId, null, sequence, ConversationEventKind.UserMessage,
                ConversationParticipant.User, null, text, null, null, null, null, null, null, DateTimeOffset.UnixEpoch));

        private static MissionChatPin Pin => new("Janus", 1, "1.4", MissionHandsProfile.ProjectWorkspace, true);

        private static MissionChatAccess Attached => new(MissionChatAccessState.Attached, null);

        private static MissionChatRow Row(Guid conversationId, string title, bool hasMessages) =>
            new(conversationId, title, "Janus", 1, "1.4", DateTimeOffset.UnixEpoch, hasMessages);
    }
}
