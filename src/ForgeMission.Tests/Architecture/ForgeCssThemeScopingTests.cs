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
    public void TheClientRuntimePresentation_SelectsTheWorkbenchSurfaceTheme()
    {
        var index = Path.Combine(RepositoryRoot(), "src", "ForgeMission.ClientRuntime.Presentation",
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
                     // 43.20 task 3 — the workbench shell's own geometry.
                     "--wb-rail-width", "--wb-rail-pad-x", "--wb-rail-pad-y", "--wb-rail-gap",
                     "--wb-rail-item-pad-x", "--wb-rail-item-pad-y", "--wb-rail-marker-width",
                     "--wb-section-gap",
                 })
            Assert.Contains(token, light.Body, StringComparison.Ordinal);
    }

    // 43.20 task 3 — the rail is a distinct dark surface in BOTH colour modes, so unlike every
    // other colour token its dark values are deliberately IDENTICAL to its light ones. The general
    // pairing test above only proves each token is restated; this proves it is restated unchanged,
    // which is the actual design decision and the thing a later "fix the dark mode" edit would
    // silently undo.
    [Fact]
    public void TheRailTokens_HoldTheSameValuesInBothColourModes()
    {
        var workbench = TokenBlocks()
            .Where(block => block.Selector.Contains(SurfaceThemeAttribute, StringComparison.Ordinal))
            .ToList();

        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["--wb-rail-surface"] = "#06284c",
            ["--wb-rail-surface-selected"] = "#0c4475",
            ["--wb-rail-text"] = "#ffffff",
            ["--wb-rail-text-muted"] = "#b9d6f5",
            ["--wb-rail-marker"] = "#32d9f2",
        };

        Assert.Equal(3, workbench.Count); // light, automatic dark, forced dark
        foreach (var block in workbench)
        {
            foreach (var (token, value) in expected)
                Assert.Equal(value, Declared(block.Body, token));
        }
    }

    private static string Declared(string body, string token)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            body, $@"{System.Text.RegularExpressions.Regex.Escape(token)}\s*:\s*([^;]+);");
        Assert.True(match.Success, $"{token} is not declared in this Workbench block.");
        return match.Groups[1].Value.Trim();
    }

    [Fact]
    public void ForgeUiHost_SelectsNoSurfaceTheme()
    {
        var host = Path.Combine(RepositoryRoot(), "src", "ForgeUI", "Pages", "_Host.cshtml");
        if (!File.Exists(host))
            return; // The host page moved; the CSS-side guards above still hold.

        Assert.DoesNotContain("data-surface-theme", File.ReadAllText(host), StringComparison.Ordinal);
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
    // this guard needs and far less than a CSS parser would drag in.
    private static List<(string Selector, string Body)> TokenBlocks()
    {
        var css = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ForgeUI", "wwwroot", "css", "forge.css"));
        var blocks = new List<(string, string)>();

        foreach (Match match in Regex.Matches(css, @"(:root[^{}]*)\{([^{}]*)\}"))
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
