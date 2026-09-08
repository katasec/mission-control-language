using Bunit;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Presentation.Components;
using ForgeMission.Presentation.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using MissionConversationRecord = ForgeMission.Application.Transport.MissionConversationView;
using MissionConversationDetailRecord = ForgeMission.Application.Transport.MissionConversationDetail;

namespace ForgeMission.Tests.Presentation;

public sealed class MissionConversationPresentationTests : BunitContext
{
    [Fact]
    public void MissionConversationAndTrace_AreExplicitMissionRailDocumentStates()
    {
        var conversation = Render<WorkbenchRail>(p => p.Add(x => x.Selected, WorkbenchView.MissionConversation));
        var trace = Render<WorkbenchRail>(p => p.Add(x => x.Selected, WorkbenchView.MissionTrace));

        Assert.Contains("Missions", conversation.Find("[aria-current='page']").TextContent);
        Assert.Contains("Missions", trace.Find("[aria-current='page']").TextContent);
    }

    [Fact]
    public async Task ReturningFromMissionTrace_RestoresTheLoadedConversationAndTurnAnchor()
    {
        Services.AddSingleton<IApplicationChannel>(new NoCallsChannel());
        var page = Render<Home>();
        var turn = new MissionTurnView(Guid.NewGuid(), Guid.NewGuid(), "Inspect this", ConversationRunStatus.Completed, 3, 7);
        var conversation = new MissionConversationRecord(Guid.NewGuid(), Guid.NewGuid(), 2, "No hands", ConversationRunStatus.Completed, 7, DateTimeOffset.UtcNow);
        var detail = new MissionConversationDetailRecord(conversation, [turn], []);

        Set(page.Instance, "missionConversation", detail);
        Set(page.Instance, "missionTrace", new MissionTurnTrace(conversation.ConversationId, turn.TurnId, turn.TurnAttemptId, []));
        Set(page.Instance, "view", WorkbenchView.MissionTrace);

        var back = typeof(Home).GetMethod("BackToMissionConversation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)back.Invoke(page.Instance, [])!;

        Assert.Equal(WorkbenchView.MissionConversation, Get<WorkbenchView>(page.Instance, "view"));
        Assert.Same(detail, Get<MissionConversationDetailRecord>(page.Instance, "missionConversation"));
        Assert.Null(Get<MissionTurnTrace?>(page.Instance, "missionTrace"));
        Assert.Equal(turn.TurnAttemptId, Get<MissionConversationDetailRecord>(page.Instance, "missionConversation").Turns.Single().TurnAttemptId);
    }

    [Fact]
    public void DecliningProfileConfirmation_CreatesNoConversation()
    {
        var created = 0;
        var view = Render<MissionConversationsView>(p => p
            .Add(x => x.Versions, [Version()])
            .Add(x => x.Create, EventCallback.Factory.Create<Guid>(this, _ => created++)));

        view.Find(".mcv-primary").Click();
        view.Find(".mcv-quiet").Click();

        Assert.Equal(0, created);
    }

    [Fact]
    public void CreateFiresOnlyAfterExactProfileConfirmation()
    {
        Guid? created = null;
        var version = Version();
        var view = Render<MissionConversationsView>(p => p
            .Add(x => x.Versions, [version])
            .Add(x => x.Create, EventCallback.Factory.Create<Guid>(this, value => created = value)));

        Assert.Null(created);
        view.Find(".mcv-primary").Click();
        Assert.Null(created);
        Assert.Contains("NoHands", view.Markup);
        view.FindAll(".mcv-primary")[1].Click();

        Assert.Equal(version.MissionId, created);
    }

    private static MissionVersionPickerItem Version() => new(Guid.NewGuid(), "Planner", Guid.NewGuid(), 4,
        "sha256:test", MissionHandsProfile.NoHands);

    private static void Set<T>(Home home, string field, T value) => typeof(Home).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(home, value);
    private static T Get<T>(Home home, string field) => (T)typeof(Home).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(home)!;

    private sealed class NoCallsChannel : IApplicationChannel
    {
        public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct) => throw new InvalidOperationException("This navigation test makes no application request.");
        public async IAsyncEnumerable<ApplicationEvent> Subscribe([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) { await Task.Delay(Timeout.Infinite, ct); yield break; }
    }
}
