using Bunit;
using ForgeMission.Presentation.Components;
using ForgeMission.Presentation.Pages;
using ForgeMission.Application.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Tests.Presentation;

/// <summary>
/// What the pre-workspace surfaces are allowed to do before a person acts. Phase 47.1 added a
/// startup choice and a static chat prototype in front of the launcher, so the zero-authority
/// proof now has to cover every route through them: each one renders, routes locally, and reaches
/// the Application for nothing at all.
/// </summary>
public sealed class HomeSessionOperationTests : BunitContext
{
    // One channel for the whole context: bUnit fixes its service provider at the first render, and
    // two of these tests render the page twice to prove a reload starts clean.
    private readonly NoCallsChannel channel = new();

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
    public void ChatWithAMission_OpensTheStaticPreview_WithoutSendingAnything()
    {
        var (page, channel) = RenderHome();

        Choose(page, "Chat with a mission");

        Assert.Contains("WHO IS IN THIS CHAT", page.Markup);
        Assert.Empty(page.FindAll(".pl-goal"));
        Assert.Empty(channel.Requests);
    }

    [Fact]
    public void ThePreview_NeverSelectsTheMissionsSurfaceTheme()
    {
        var (page, _) = RenderHome();

        Choose(page, "Chat with a mission");

        // The prototype is a Workbench-themed surface. Inheriting the durable Missions theme would
        // re-skin it to a palette its references never used.
        Assert.DoesNotContain("forge-desktop-dark", page.Markup);
        Assert.DoesNotContain("data-surface-theme", page.Markup);
    }

    [Fact]
    public void SendingInThePreview_ReachesTheApplicationForNothing()
    {
        var (page, channel) = RenderHome();
        Choose(page, "Chat with a mission");

        page.Find(".mcp-composer-field").Input("Hold the cohort invite.");
        page.Find(".mcp-send").Click();

        Assert.Contains("Hold the cohort invite.", page.Find(".mcp-user-bubble").TextContent);
        Assert.Empty(channel.Requests);
    }

    [Fact]
    public void AuthorAMission_LeavesThePreviewForTheUnchangedLauncher()
    {
        var (page, channel) = RenderHome();
        Choose(page, "Chat with a mission");

        Choose(page, "Author a mission");

        Assert.Single(page.FindAll(".pl-goal"));
        Assert.Empty(page.FindAll(".mcp-shell"));
        Assert.Empty(channel.Requests);
    }

    [Fact]
    public void AFreshRender_ReturnsToTheStartChoice_WithNoPreviewStateSurviving()
    {
        var (first, _) = RenderHome();
        Choose(first, "Chat with a mission");
        first.Find(".mcp-composer-field").Input("Something I typed before reloading.");
        first.Find(".mcp-send").Click();

        // A reload rebuilds the page component, which is the whole of the prototype's reset rule.
        var (second, channel) = RenderHome();

        Assert.Contains("Where do you want to start?", second.Markup);
        Assert.DoesNotContain("Something I typed before reloading.", second.Markup);
        Assert.Empty(channel.Requests);
    }

    private (IRenderedComponent<Home> Page, NoCallsChannel Channel) RenderHome() => (Render<Home>(), channel);

    private static void Choose(IRenderedComponent<Home> page, string label) =>
        page.FindAll("button")
            .First(button => button.TextContent.Contains(label, StringComparison.Ordinal))
            .Click();

    private sealed class NoCallsChannel : IApplicationChannel
    {
        public List<object> Requests { get; } = [];
        public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
        { Requests.Add(request!); throw new InvalidOperationException("The launcher must not make a call before a person acts."); }
        public async IAsyncEnumerable<ApplicationEvent> Subscribe([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) { await Task.Delay(Timeout.Infinite, ct); yield break; }
    }
}
