using Bunit;
using ForgeMission.ConversationPresentation;

namespace ForgeMission.Tests.Presentation;

/// <summary>
/// Task 1 of 43.18: the shared renderer's three fixed states, their status semantics, and the
/// structural claim that it depends on no Forge surface, transport, or service.
/// </summary>
public sealed class ConversationActivityTests : BunitContext
{
    private IRenderedComponent<ConversationActivity> RenderActivity(ConversationActivityState state)
        => Render<ConversationActivity>(parameters => parameters.Add(p => p.State, state));

    [Fact]
    public void Thinking_ShowsTheActorIsThinking()
    {
        var component = RenderActivity(new("@scout", ConversationActivityKind.Thinking, null));

        Assert.Contains("@scout is thinking…", component.Find(".convo-activity-text").TextContent);
        Assert.Single(component.FindAll(".convo-activity-thinking"));
    }

    [Fact]
    public void Working_ShowsTheActorIsWorking()
    {
        var component = RenderActivity(new("@scout", ConversationActivityKind.Working, null));

        Assert.Contains("@scout is working…", component.Find(".convo-activity-text").TextContent);
        Assert.Single(component.FindAll(".convo-activity-working"));
    }

    [Fact]
    public void Streaming_ShowsTheActorIsRespondingWithACaret()
    {
        var component = RenderActivity(new("Janus", ConversationActivityKind.Streaming, null));

        Assert.Contains("Janus is responding…", component.Find(".convo-activity-text").TextContent);
        Assert.Single(component.FindAll(".convo-activity-caret"));
    }

    [Fact]
    public void EveryState_IsAPoliteStatusRegion()
    {
        foreach (var kind in Enum.GetValues<ConversationActivityKind>())
        {
            var root = RenderActivity(new("@scout", kind, null)).Find(".convo-activity");

            Assert.Equal("status", root.GetAttribute("role"));
            Assert.Equal("polite", root.GetAttribute("aria-live"));
        }
    }

    [Fact]
    public void Detail_ReplacesTheDefaultPhraseForItsKind()
    {
        var component = RenderActivity(new("@scout", ConversationActivityKind.Working, "Searching the web…"));

        var text = component.Find(".convo-activity-text").TextContent;
        Assert.Contains("@scout Searching the web…", text);
        Assert.DoesNotContain("is working", text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AbsentDetail_FallsBackToTheDefaultPhrase(string? detail)
    {
        var component = RenderActivity(new("@scout", ConversationActivityKind.Thinking, detail));

        Assert.Contains("@scout is thinking…", component.Find(".convo-activity-text").TextContent);
    }

    [Fact]
    public void TheRenderer_DependsOnNoForgeAssembly()
    {
        var referenced = typeof(ConversationActivity).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => n.StartsWith("ForgeMission", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(referenced);
    }
}
