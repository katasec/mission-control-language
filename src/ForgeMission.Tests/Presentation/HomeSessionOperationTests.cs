using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bunit;
using ForgeMission.ClientRuntime.Presentation.Pages;
using ForgeMission.ClientRuntime.Transport;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Tests.Presentation;

/// <summary>
/// Phase 43.17 Task 3 — Home owns one cancellable session/view operation. The fake channel counts
/// concurrent subscriptions, so "exactly one subscriber survives a replacement" is a measured peak
/// rather than an inference from reading the page.
/// </summary>
public sealed class HomeSessionOperationTests : BunitContext
{
    private readonly FakeClientRuntimeChannel channel = new();

    public HomeSessionOperationTests() => Services.AddSingleton<IClientRuntimeChannel>(channel);

    [Fact]
    public async Task RepeatedFolderAndMissionReplacement_LeavesExactlyOneSubscription()
    {
        var page = RenderHome();

        await AddFolderAsync(page, "/one");
        await AddFolderAsync(page, "/two");
        await SelectMissionAsync(page, "Janus");
        await SelectMissionAsync(page, "ChatGPT");

        Assert.Equal(5, channel.SubscriptionsStarted);
        Assert.Equal(1, channel.ActiveSubscriptions);
        Assert.Equal(1, channel.PeakSubscriptions);
    }

    [Fact]
    public async Task Replacement_AwaitsTheOldSubscription_SoEachEventIsAppliedOnce()
    {
        var page = RenderHome();
        await AddFolderAsync(page, "/one");
        await AddFolderAsync(page, "/two");

        channel.HoldNextPrompt();
        await SendPromptAsync(page, "build it");
        channel.Publish(Delta("answer"));

        page.WaitForAssertion(() => Assert.Contains("answer", page.Find(".response-card").TextContent));
        // A surviving second subscriber would have applied the same delta twice.
        Assert.Equal("answer", page.Find(".response-card").TextContent);
    }

    [Fact]
    public async Task ReplacementWhileEventsAreRendering_CompletesWithoutDeadlock()
    {
        var page = RenderHome();
        await AddFolderAsync(page, "/one");

        channel.HoldNextPrompt();
        await SendPromptAsync(page, "streaming");
        for (var index = 0; index < 200; index++)
            channel.Publish(Delta("x"));

        // The replacement cancels and awaits a subscription that is mid-render. If awaiting the
        // event loop from a UI handler could deadlock, this never returns.
        await AddFolderAsync(page, "/two");

        Assert.Equal(1, channel.ActiveSubscriptions);
        Assert.Empty(page.FindAll(".response-card"));
    }

    [Fact]
    public async Task StalePromptResult_AfterReplacement_CannotMutateTheNewSession()
    {
        var page = RenderHome();
        await AddFolderAsync(page, "/one");

        channel.HoldNextPrompt();
        await SendPromptAsync(page, "slow prompt");
        await AddFolderAsync(page, "/two");

        channel.ReleaseHeldPrompt(new PromptResponse("stale answer"));
        await Task.Delay(50);

        Assert.DoesNotContain("stale answer", page.Markup);
        Assert.DoesNotContain("slow prompt", page.Markup);
        Assert.Empty(page.FindAll(".error-banner"));
        Assert.Empty(page.FindAll(".connection-banner"));
        // The discarded operation did not clear the replacement's own sending state.
        Assert.Equal("Send", page.Find(".composer-send").TextContent.Trim());
    }

    [Fact]
    public async Task ExpectedCancellation_IsSilent()
    {
        var page = RenderHome();
        await AddFolderAsync(page, "/one");
        await SelectMissionAsync(page, "Janus");

        Assert.Empty(page.FindAll(".error-banner"));
        Assert.Empty(page.FindAll(".connection-banner"));
        Assert.Empty(page.FindAll(".gap-notice"));
    }

    [Fact]
    public async Task UnexpectedStreamFailure_BecomesVisibleRetryableState()
    {
        var page = RenderHome();
        channel.FaultNextSubscription(new IOException("stream reset"));
        await AddFolderAsync(page, "/one");

        page.WaitForAssertion(() => Assert.Contains("stream reset", page.Find(".connection-banner").TextContent));
        Assert.Single(page.FindAll(".connection-retry"));
    }

    [Fact]
    public async Task Retry_OpensANewSubscriptionAndShowsThePersistentGapNotice()
    {
        var page = RenderHome();
        channel.FaultNextSubscription(new IOException("stream reset"));
        await AddFolderAsync(page, "/one");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));

        var before = channel.SubscriptionsStarted;
        await ClickAsync(page, ".connection-retry");

        page.WaitForAssertion(() => Assert.Equal(before + 1, channel.SubscriptionsStarted));
        Assert.Equal(1, channel.ActiveSubscriptions);
        Assert.Empty(page.FindAll(".connection-banner"));
        Assert.Contains("Updates that arrived while disconnected are not shown",
            page.Find(".gap-notice").TextContent);

        // The notice records a permanent gap, so later successful events do not retract it.
        channel.Publish(Delta("later"));
        await Task.Delay(50);
        Assert.Single(page.FindAll(".gap-notice"));
    }

    [Fact]
    public async Task GapNotice_ClearsOnlyWhenANewViewBegins()
    {
        var page = RenderHome();
        channel.FaultNextSubscription(new IOException("stream reset"));
        await AddFolderAsync(page, "/one");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));
        await ClickAsync(page, ".connection-retry");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".gap-notice")));

        await AddFolderAsync(page, "/two");

        Assert.Empty(page.FindAll(".gap-notice"));
    }

    [Fact]
    public async Task WhileDisconnected_DurableConversationPromptsAreBlocked_AndMissionPromptsAreNot()
    {
        var page = RenderHome();
        await AddFolderAsync(page, "/one");

        channel.FaultNextSubscription(new IOException("stream reset"));
        await SelectMissionAsync(page, "Janus");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));
        Assert.True(page.Find(".composer-input").HasAttribute("disabled"));

        channel.FaultNextSubscription(new IOException("stream reset"));
        await SelectMissionAsync(page, "ChatGPT");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));
        Assert.False(page.Find(".composer-input").HasAttribute("disabled"));
    }

    [Fact]
    public async Task DisposeAsync_LeavesNoActiveSubscription()
    {
        var page = RenderHome();
        await AddFolderAsync(page, "/one");
        Assert.Equal(1, channel.ActiveSubscriptions);

        await page.Instance.DisposeAsync();

        Assert.Equal(0, channel.ActiveSubscriptions);
    }

    private IRenderedComponent<Home> RenderHome()
    {
        var page = Render<Home>();
        page.WaitForAssertion(() => Assert.Equal(1, channel.ActiveSubscriptions));
        return page;
    }

    private ClientRuntimeEvent Delta(string text) =>
        new(ClientRuntimeEventKind.MissionTextDelta, channel.CurrentSessionId, Text: text);

    private async Task AddFolderAsync(IRenderedComponent<Home> page, string folder)
    {
        if (page.FindAll(".add-folder-menu").Count == 0)
            await ClickAsync(page, ".composer-plus");

        await InputAsync(page, ".add-folder-menu input", folder);
        await StartReplacementAsync(page, ".menu-confirm");
    }

    private async Task SelectMissionAsync(IRenderedComponent<Home> page, string missionName)
    {
        await ClickAsync(page, ".mission-trigger");
        await StartReplacementAsync(page, ".mission-item", missionName);
    }

    // The click itself only starts the replacement; its async handler completes later, so the
    // helper waits for the new subscription (or a surfaced setup failure) before returning.
    private async Task StartReplacementAsync(IRenderedComponent<Home> page, string selector, string? text = null)
    {
        var before = channel.SubscriptionsStarted;
        await ClickAsync(page, selector, text);
        page.WaitForAssertion(() => Assert.True(
            channel.SubscriptionsStarted > before || page.FindAll(".menu-error").Count > 0));
    }

    private static async Task SendPromptAsync(IRenderedComponent<Home> page, string prompt)
    {
        await InputAsync(page, ".composer-input", prompt);
        await ClickAsync(page, ".composer-send");
        page.WaitForAssertion(() => Assert.Contains("Sending", page.Find(".composer-send").TextContent));
    }

    // Find and dispatch inside one dispatcher turn: a concurrent stream render between the two
    // would otherwise invalidate the element bunit found.
    private static Task InputAsync(IRenderedComponent<Home> page, string selector, string value) =>
        page.InvokeAsync(() => page.Find(selector).Input(new ChangeEventArgs { Value = value }));

    private static Task ClickAsync(IRenderedComponent<Home> page, string selector, string? text = null) =>
        page.InvokeAsync(() => Match(page, selector, text).Click());

    private static AngleSharp.Dom.IElement Match(IRenderedComponent<Home> page, string selector, string? text) =>
        text is null
            ? page.Find(selector)
            : page.FindAll(selector).First(candidate => candidate.TextContent.Contains(text, StringComparison.Ordinal));

    private sealed class FakeClientRuntimeChannel : IClientRuntimeChannel
    {
        private readonly Lock gate = new();
        private readonly List<Channel<ClientRuntimeEvent>> streams = [];
        private TaskCompletionSource<PromptResponse>? heldPrompt;
        private Exception? nextSubscriptionFault;
        private int sessionCounter;

        public int ActiveSubscriptions { get; private set; }
        public int PeakSubscriptions { get; private set; }
        public int SubscriptionsStarted { get; private set; }
        public string CurrentSessionId { get; private set; } = "session-0";

        public void FaultNextSubscription(Exception fault) => nextSubscriptionFault = fault;

        public void HoldNextPrompt() =>
            heldPrompt = new TaskCompletionSource<PromptResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseHeldPrompt(PromptResponse response)
        {
            var held = heldPrompt ?? throw new InvalidOperationException("No prompt is being held.");
            heldPrompt = null;
            held.SetResult(response);
        }

        public void Publish(ClientRuntimeEvent message)
        {
            lock (gate)
            {
                foreach (var stream in streams)
                    stream.Writer.TryWrite(message);
            }
        }

        public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
        {
            object response = request switch
            {
                DefaultWorkspaceSessionRequest => new DefaultWorkspaceSessionResponse(NextSession(), [], "/default"),
                SessionSetupRequest => new SessionSetupResponse(NextSession(), []),
                PromptRequest => await PromptAsync(ct),
                _ => throw new InvalidOperationException($"Unexpected request: {typeof(TRequest).Name}."),
            };

            return (TResponse)response;
        }

        public async IAsyncEnumerable<ClientRuntimeEvent> Subscribe([EnumeratorCancellation] CancellationToken ct)
        {
            var stream = Channel.CreateUnbounded<ClientRuntimeEvent>();
            lock (gate)
            {
                streams.Add(stream);
                ActiveSubscriptions++;
                PeakSubscriptions = Math.Max(PeakSubscriptions, ActiveSubscriptions);
                SubscriptionsStarted++;
            }

            try
            {
                if (nextSubscriptionFault is { } fault)
                {
                    nextSubscriptionFault = null;
                    throw fault;
                }

                await foreach (var message in stream.Reader.ReadAllAsync(ct))
                    yield return message;
            }
            finally
            {
                lock (gate)
                {
                    streams.Remove(stream);
                    ActiveSubscriptions--;
                }
            }
        }

        private async Task<PromptResponse> PromptAsync(CancellationToken ct) =>
            heldPrompt is null ? new PromptResponse(string.Empty) : await heldPrompt.Task.WaitAsync(ct);

        private string NextSession() => CurrentSessionId = $"session-{++sessionCounter}";
    }
}
