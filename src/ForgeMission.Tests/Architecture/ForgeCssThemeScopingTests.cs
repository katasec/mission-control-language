using System.Text.RegularExpressions;

namespace ForgeMission.Tests.Architecture;

/// <summary>
/// Phase 43.20 Task 1 — the Workbench product theme must stay reachable only through
/// <c>data-surface-theme="workbench"</c>. ForgeUI selects no surface theme, so a Workbench value
/// that leaked into an unscoped block would silently re-skin it. This is the structural guard for
/// that, rather than a comment asking a later editor to remember.
/// </summary>
public sealed class ForgeCssThemeScopingTests
{
    private const string SurfaceThemeAttribute = "[data-surface-theme=\"workbench\"]";
    private const string MissionsThemeAttribute = "[data-surface-theme=\"forge-desktop-dark\"]";

    [Fact]
    public void EveryWorkbenchValue_IsReachableOnlyThroughTheSurfaceThemeAttribute()
    {
        foreach (var (selector, _) in TokenBlocks())
        {
            if (!selector.Contains(SurfaceThemeAttribute, StringComparison.Ordinal))
                AssertNoWorkbenchValues(selector);
        }
    }

    [Fact]
    public void TheWorkbenchTheme_DefinesBothALightAndADarkMap()
    {
        var workbench = TokenBlocks()
            .Where(block => block.Selector.Contains(SurfaceThemeAttribute, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, workbench.Count); // light, automatic dark, forced dark
        Assert.Single(workbench, block => block.Body.Contains("color-scheme: light", StringComparison.Ordinal));
        Assert.Equal(2, workbench.Count(block => block.Body.Contains("color-scheme: dark", StringComparison.Ordinal)));

        // The dark maps pair with light: every colour token light declares, dark restates.
        var light = workbench.Single(block => block.Body.Contains("color-scheme: light", StringComparison.Ordinal));
        foreach (var token in ColourTokens(light.Body))
        {
            foreach (var dark in workbench.Where(block => block.Body.Contains("color-scheme: dark", StringComparison.Ordinal)))
                Assert.Contains(token, ColourTokens(dark.Body));
        }
    }

    // The launcher's geometry lives in the Workbench map, so the surface that renders it must
    // actually select the theme; without the attribute those tokens resolve to nothing.
    [Fact]
    public void ThePresentation_SelectsTheWorkbenchSurfaceTheme()
    {
        var index = Path.Combine(RepositoryRoot(), "src", "ForgeMission.Presentation",
            "wwwroot", "index.html");

        Assert.Contains("data-surface-theme=\"workbench\"", File.ReadAllText(index), StringComparison.Ordinal);
    }

    [Fact]
    public void TheWorkbenchTheme_DeclaresTheLayoutGeometryItsSurfacesConsume()
    {
        var light = TokenBlocks().Single(block =>
            block.Selector.Contains(SurfaceThemeAttribute, StringComparison.Ordinal) &&
            block.Body.Contains("color-scheme: light", StringComparison.Ordinal));

        foreach (var token in new[]
                 {
                     "--wb-header-height", "--wb-header-inset", "--wb-card-width", "--wb-card-gap-top",
                     "--wb-card-pad-x", "--wb-card-pad-y", "--wb-card-pad-bottom", "--wb-page-inset",
                     "--wb-field-gutter", "--wb-goal-height",
                     "--wb-name-height", "--wb-location-height", "--wb-gap-title", "--wb-gap-field",
                     "--wb-gap-rule", "--wb-gap-action", "--wb-band-gap", "--wb-band-pad", "--wb-band-max",
                     "--wb-action-width", "--wb-action-height", "--wb-action-pad-x", "--wb-link-gap", "--wb-open-row-gap",
                 })
            Assert.Contains(token, light.Body, StringComparison.Ordinal);
    }

    // Phase 47.1 — the Mission Chat prototype's reference geometry is theme-owned for the same
    // reason the launcher's is: a component that hard-coded a measurement would fragment the token
    // system. These are geometry and type only, which is why the dark maps inherit them and the
    // light/dark colour pairing above is unaffected.
    [Fact]
    public void TheWorkbenchTheme_DeclaresTheMissionChatGeometryItsSurfacesConsume()
    {
        var light = TokenBlocks().Single(block =>
            block.Selector.Contains(SurfaceThemeAttribute, StringComparison.Ordinal) &&
            block.Body.Contains("color-scheme: light", StringComparison.Ordinal));

        foreach (var token in new[]
                 {
                     // Chat shell and columns. The rail is the prototype's own width, because the
                     // frames draw 216px where the shipped rail token is 192px.
                     "--wb-chat-rail-width", "--wb-chat-list-width", "--wb-chat-column-gap",
                     "--wb-chat-block-pad", "--wb-chat-row-pad", "--wb-chat-action-height",
                     "--wb-chat-transcript-gap",
                     // Composer and message type.
                     "--wb-chat-composer-height", "--wb-chat-composer-pad-top",
                     "--wb-chat-composer-pad-bottom", "--wb-chat-send-width",
                     "--wb-chat-composer-font-size", "--wb-chat-message-font-size",
                     // Startup choice (frame 01), which is its own card rather than the launcher's.
                     "--wb-chat-start-card-width", "--wb-chat-start-gap-top",
                     "--wb-chat-start-card-pad-x", "--wb-chat-start-card-pad-y",
                     "--wb-chat-start-card-pad-bottom", "--wb-chat-start-option-height",
                     "--wb-chat-start-option-pad", "--wb-chat-start-tile-size",
                     "--wb-chat-start-rule-gap", "--wb-chat-start-link-gap",
                 })
            Assert.Contains(token, light.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ForgeUiHost_SelectsNoSurfaceTheme()
    {
        var host = Path.Combine(RepositoryRoot(), "src", "ForgeUI", "Pages", "_Host.cshtml");
        if (!File.Exists(host))
            return; // The host page moved; the CSS-side guards above still hold.

        Assert.DoesNotContain("data-surface-theme", File.ReadAllText(host), StringComparison.Ordinal);
    }

    // Phase 45.3 Task 3A — the Missions surface's theme is reached through the same attribute one
    // level down, so it gets the same two guards: its values stay behind the attribute, and it
    // defines both colour modes rather than only the dark one it is named for.
    [Fact]
    public void EveryMissionsValue_IsReachableOnlyThroughTheSurfaceThemeAttribute()
    {
        string[] missionsOnly = ["#181511", "#ff9a4a", "#f4ede5", "#9ee2a7", "#392a1d"];

        foreach (var (selector, body) in TokenBlocks())
        {
            if (selector.Contains(MissionsThemeAttribute, StringComparison.Ordinal))
                continue;
            foreach (var value in missionsOnly)
                Assert.DoesNotContain(value, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TheMissionsTheme_DefinesBothALightAndADarkMap()
    {
        var missions = TokenBlocks()
            .Where(block => block.Selector.Contains(MissionsThemeAttribute, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, missions.Count); // light, automatic dark, forced dark
        Assert.Single(missions, block => block.Body.Contains("color-scheme: light", StringComparison.Ordinal));
        Assert.Equal(2, missions.Count(block => block.Body.Contains("color-scheme: dark", StringComparison.Ordinal)));

        var light = missions.Single(block => block.Body.Contains("color-scheme: light", StringComparison.Ordinal));
        foreach (var token in ColourTokens(light.Body))
        {
            foreach (var dark in missions.Where(block => block.Body.Contains("color-scheme: dark", StringComparison.Ordinal)))
                Assert.Contains(token, ColourTokens(dark.Body));
        }
    }

    // data-theme is the ROOT's colour-mode hook and forge-desktop-dark sits on a nested element,
    // so each map must test the mode on :root and the surface on its descendant. Testing
    // data-theme on the nested element compiles and looks plausible, but the nested element never
    // carries it: the automatic map would always win and both explicit choices would be dead.
    // These three assert the composition that makes an operator's light/dark choice work.
    [Fact]
    public void TheMissionsTheme_BaseMap_IsTheNestedSurfaceAlone()
    {
        var light = MissionsBlocks().Single(block => block.Body.Contains("color-scheme: light", StringComparison.Ordinal));

        Assert.Equal(MissionsThemeAttribute, light.Selector);
        Assert.DoesNotContain("data-theme", light.Selector, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMissionsTheme_AutomaticDarkMap_TestsTheRootColourMode_NotTheNestedSurface()
    {
        var css = CssWithoutComments();
        var automatic = MissionsBlocks().Single(block =>
            block.Selector.StartsWith(":root:not([data-theme=\"light\"])", StringComparison.Ordinal));

        // The mode condition is on :root; the surface is its descendant, with a combinator between.
        Assert.Equal($":root:not([data-theme=\"light\"]) {MissionsThemeAttribute}", automatic.Selector);
        Assert.Contains("color-scheme: dark", automatic.Body, StringComparison.Ordinal);
        // ...and it only applies when the OS asks for dark.
        var selectorAt = css.IndexOf(automatic.Selector, StringComparison.Ordinal);
        var media = css.LastIndexOf("@media (prefers-color-scheme: dark)", selectorAt, StringComparison.Ordinal);
        Assert.InRange(media, 0, selectorAt);
    }

    [Fact]
    public void TheMissionsTheme_ForcedDarkMap_TestsTheRootColourMode_NotTheNestedSurface()
    {
        var forced = MissionsBlocks().Single(block =>
            block.Selector.StartsWith(":root[data-theme=\"dark\"]", StringComparison.Ordinal));

        Assert.Equal($":root[data-theme=\"dark\"] {MissionsThemeAttribute}", forced.Selector);
        Assert.Contains("color-scheme: dark", forced.Body, StringComparison.Ordinal);
    }

    // A colour-mode condition must never be written against the nested surface element, in any map.
    [Fact]
    public void TheMissionsTheme_NeverTestsDataThemeOnTheNestedSurface()
    {
        foreach (var block in MissionsBlocks())
        {
            Assert.DoesNotContain($"{MissionsThemeAttribute}[data-theme", block.Selector, StringComparison.Ordinal);
            Assert.DoesNotContain($"{MissionsThemeAttribute}:not([data-theme", block.Selector, StringComparison.Ordinal);
        }
    }

    private static List<(string Selector, string Body)> MissionsBlocks() =>
        [.. TokenBlocks().Where(block => block.Selector.Contains(MissionsThemeAttribute, StringComparison.Ordinal))];

    // The landing's geometry lives in the theme map too, so a component never hard-codes a
    // reference measurement.
    [Fact]
    public void TheMissionsTheme_DeclaresTheLandingGeometryItsSurfacesConsume()
    {
        var light = TokenBlocks().Single(block =>
            block.Selector.Contains(MissionsThemeAttribute, StringComparison.Ordinal) &&
            block.Body.Contains("color-scheme: light", StringComparison.Ordinal));

        foreach (var token in new[]
                 {
                     "--wb-rail-width", "--wb-page-inset", "--ml-header-height", "--ml-region-top",
                     "--ml-region-max", "--ml-column-gap", "--ml-panel-width", "--ml-block-pad",
                     "--ml-row-pad", "--ml-action-height", "--ml-actions-bottom",
                 })
            Assert.Contains(token, light.Body, StringComparison.Ordinal);
    }

    // A Workbench value outside a Workbench block would re-theme every surface that consumes the
    // token, which is exactly what the named theme exists to avoid.
    private static void AssertNoWorkbenchValues(string selector)
    {
        string[] workbenchOnly = ["#f7faff", "#0f6eeb", "#101d33", "#62748c", "#071426", "#4d9bff"];
        var body = TokenBlocks().First(block => block.Selector == selector).Body;

        foreach (var value in workbenchOnly)
            Assert.DoesNotContain(value, body, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ColourTokens(string body) =>
        Regex.Matches(body, @"(--[a-z0-9-]+)\s*:\s*#", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value)
            .Distinct();

    // A deliberately small reader: it splits on top-level `selector { ... }` pairs, which is all
    // this guard needs and far less than a CSS parser would drag in. It reads both shapes of token
    // block: the document-level `:root...` ones, and a surface theme selected on an element inside
    // the document rather than on <html>.
    private static string Css() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ForgeUI", "wwwroot", "css", "forge.css"));

    // Comments are stripped before the block reader runs: this file documents its selectors in
    // prose, and a comment that happens to contain ":root" would otherwise be read as one.
    private static string CssWithoutComments() =>
        Regex.Replace(Css(), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    private static List<(string Selector, string Body)> TokenBlocks()
    {
        var css = CssWithoutComments();
        var blocks = new List<(string, string)>();

        foreach (Match match in Regex.Matches(css, @"((?::root|\[data-surface-theme)[^{}]*)\{([^{}]*)\}"))
            blocks.Add((match.Groups[1].Value.Trim(), match.Groups[2].Value));

        Assert.NotEmpty(blocks);
        return blocks;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "ForgeMission.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
