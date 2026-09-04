using Bunit;
using ForgeMission.ClientRuntime.Presentation.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ForgeMission.Tests.Presentation;

/// <summary>
/// Phase 43.21 Task 2 — the mission picker is fully operable from the keyboard.
///
/// It is a custom control rather than a native <c>&lt;select&gt;</c>, and that choice is only
/// defensible if it behaves like one. A native select's popup is drawn by the operating system: it
/// cannot take the Workbench tokens and cannot be captured in a browser, so the required "picker
/// open" state would have no verifiable evidence — but the price is that every key a select would
/// have handled has to be handled here, and proved.
/// </summary>
public sealed class MissionPickerTests : BunitContext
{
    private static readonly string[] Catalog = ["Janus", "Naive"];

    [Fact]
    public void Closed_ItNamesTheCurrentMission_AndSaysItIsClosed()
    {
        var picker = RenderPicker("Janus");

        Assert.Contains("Janus", picker.Find(".mp-button").TextContent, StringComparison.Ordinal);
        Assert.Equal("false", picker.Find(".mp-button").GetAttribute("aria-expanded"));
        Assert.Equal("listbox", picker.Find(".mp-button").GetAttribute("aria-haspopup"));
        Assert.Equal("Mission", picker.Find(".mp-button").GetAttribute("aria-label"));
        Assert.Empty(picker.FindAll(".mp-list"));
    }

    // A Project whose manifest names an unrunnable mission. The control states the absence rather
    // than filling it in, and stays usable so the person can repair it.
    [Fact]
    public void WithNoSelection_ItSaysSo_RatherThanShowingTheDefault()
    {
        var picker = RenderPicker(null);

        Assert.Contains("none selected", picker.Find(".mp-value").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Janus", picker.Find(".mp-value").TextContent, StringComparison.Ordinal);
        Assert.False(picker.Find(".mp-button").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData("ArrowDown")]
    [InlineData("ArrowUp")]
    public void TheButton_OpensOnAnArrowKey(string key)
    {
        var picker = RenderPicker("Janus");

        picker.Find(".mp-button").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Equal(2, picker.FindAll(".mp-option").Count);
        Assert.Equal("true", picker.Find(".mp-button").GetAttribute("aria-expanded"));
    }

    // The exact bug this guards: a browser turns Enter/Space on a button into a click, so the key
    // handler and that click would open the popup and close it again in the same press. The
    // synthesised click is suppressed once, and the popup stays open.
    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void TheButton_OpensOnActivationKeys_AndTheSynthesisedClickDoesNotCloseItAgain(string key)
    {
        var picker = RenderPicker("Janus");
        var button = picker.Find(".mp-button");

        button.KeyDown(new KeyboardEventArgs { Key = key });
        Assert.Equal(2, picker.FindAll(".mp-option").Count);

        // Exactly what a browser sends next: a click with no pointerdown before it.
        picker.Find(".mp-button").Click();
        Assert.Equal(2, picker.FindAll(".mp-option").Count);
        Assert.Equal("true", picker.Find(".mp-button").GetAttribute("aria-expanded"));
    }

    // And the suppression cannot leak onto a real click. A pointer activation always begins with
    // pointerdown, which clears the flag before its own click arrives.
    [Fact]
    public void APointerClickAfterAKeyPress_StillToggles()
    {
        var picker = RenderPicker("Janus");

        picker.Find(".mp-button").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(2, picker.FindAll(".mp-option").Count);

        picker.Find(".mp-button").PointerDown();
        picker.Find(".mp-button").Click();

        Assert.Empty(picker.FindAll(".mp-option"));
    }

    // Opening starts on the mission already selected, so the first arrow press moves from where the
    // person actually is rather than from the top of the list.
    [Fact]
    public void Opening_StartsOnTheCurrentMission()
    {
        var picker = RenderPicker("Naive");

        picker.Find(".mp-button").Click();

        Assert.Equal("mission-option-1", picker.Find(".mp-list").GetAttribute("aria-activedescendant"));
        Assert.Contains("mp-option-active", picker.FindAll(".mp-option")[1].ClassName!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ArrowDown", 1)]
    [InlineData("End", 1)]
    [InlineData("Home", 0)]
    public void TheList_MovesTheActiveOption(string key, int expected)
    {
        var picker = RenderPicker("Janus");
        picker.Find(".mp-button").Click();

        picker.Find(".mp-list").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Equal($"mission-option-{expected}", picker.Find(".mp-list").GetAttribute("aria-activedescendant"));
    }

    [Fact]
    public void ArrowingPastTheEnds_Stops_RatherThanWrapping()
    {
        var picker = RenderPicker("Janus");
        picker.Find(".mp-button").Click();

        picker.Find(".mp-list").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.Equal("mission-option-0", picker.Find(".mp-list").GetAttribute("aria-activedescendant"));

        picker.Find(".mp-list").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        picker.Find(".mp-list").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("mission-option-1", picker.Find(".mp-list").GetAttribute("aria-activedescendant"));
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void TheList_CommitsTheActiveOption_AndCloses(string key)
    {
        var committed = new List<string>();
        var picker = RenderPicker("Janus", committed.Add);
        picker.Find(".mp-button").Click();

        picker.Find(".mp-list").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        picker.Find(".mp-list").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Equal(["Naive"], committed);
        Assert.Empty(picker.FindAll(".mp-list"));
    }

    [Theory]
    [InlineData("Escape")]
    [InlineData("Tab")]
    public void TheList_ClosesWithoutCommitting(string key)
    {
        var committed = new List<string>();
        var picker = RenderPicker("Janus", committed.Add);
        picker.Find(".mp-button").Click();

        picker.Find(".mp-list").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        picker.Find(".mp-list").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Empty(committed);
        Assert.Empty(picker.FindAll(".mp-list"));
    }

    // Escape returns focus to the button, as a listbox should. Tab must NOT: it is already moving
    // focus somewhere on purpose, and pulling it back would trap a keyboard user inside the picker.
    // Measured as the focus call the component actually makes, not inferred from reading it.
    [Fact]
    public void Escape_ReturnsFocusToTheButton_ButTab_LeavesFocusAlone()
    {
        var escape = RenderPicker("Janus");
        escape.Find(".mp-button").Click();
        var beforeEscape = FocusCalls();
        escape.Find(".mp-list").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal(beforeEscape + 1, FocusCalls());

        var tab = RenderPicker("Janus");
        tab.Find(".mp-button").Click();
        var beforeTab = FocusCalls();
        tab.Find(".mp-list").KeyDown(new KeyboardEventArgs { Key = "Tab" });
        Assert.Equal(beforeTab, FocusCalls());
    }

    private int FocusCalls() => JSInterop.Invocations
        .Count(invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus");

    // The listbox itself holds DOM focus, so the option a screen reader announces has to be named
    // explicitly — and the selected one has to be distinguishable from the merely active one.
    [Fact]
    public void TheOpenList_IsALabelledListboxWithSelectionMarked()
    {
        var picker = RenderPicker("Naive");
        picker.Find(".mp-button").Click();

        Assert.Equal("listbox", picker.Find(".mp-list").GetAttribute("role"));
        Assert.Equal("Mission", picker.Find(".mp-list").GetAttribute("aria-label"));

        var options = picker.FindAll(".mp-option");
        Assert.Equal("option", options[0].GetAttribute("role"));
        Assert.Equal("false", options[0].GetAttribute("aria-selected"));
        Assert.Equal("true", options[1].GetAttribute("aria-selected"));
    }

    [Fact]
    public void Disabled_ItCannotBeOpenedAtAll()
    {
        var picker = RenderPicker("Janus", disabled: true);

        Assert.True(picker.Find(".mp-button").HasAttribute("disabled"));
        Assert.Empty(picker.FindAll(".mp-list"));
    }

    // It renders the catalog it is given and holds none of its own: a picker with its own copy
    // could show a mission the Client Runtime would refuse to run.
    [Fact]
    public void ItRendersOnlyTheCatalogItIsGiven()
    {
        var picker = RenderPicker("Janus");

        picker.Find(".mp-button").Click();

        Assert.Equal(["Janus", "Naive"],
            picker.FindAll(".mp-option").Select(option => option.QuerySelector("span")!.TextContent).ToArray());
    }

    private IRenderedComponent<MissionPicker> RenderPicker(
        string? selected, Action<string>? committed = null, bool disabled = false) =>
        Render<MissionPicker>(parameters => parameters
            .Add(picker => picker.Missions, Catalog)
            .Add(picker => picker.Selected, selected)
            .Add(picker => picker.Disabled, disabled)
            .Add(picker => picker.SelectedChanged, mission => committed?.Invoke(mission)));
}
