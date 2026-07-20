using ForgeMission.Api;
using ForgeMission.Runner.Contracts;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// <see cref="StaticMissionCatalog"/> resolution guarantees (42.6 task 5a, "Mission resolution" in
/// the phase-42.6 spoke): implicit and explicit default-publisher handles resolve identically, an
/// unrecognized publisher fails closed rather than falling through to a name-only match, and an
/// entry whose backing mission ref the runner doesn't advertise simply doesn't resolve (same
/// precedent AgentRegistry sets for a missing provider key).
/// </summary>
public sealed class StaticMissionCatalogTests
{
    private static StaticMissionCatalog CatalogWith(params string[] availableMissionRefs) =>
        new(availableMissionRefs.Select(r => new MissionInfo(r, Description: r)).ToList());

    [Fact]
    public async Task Bare_and_explicit_default_publisher_handles_resolve_identically()
    {
        var catalog = CatalogWith("WebSearch");

        var bare = await catalog.ResolveAsync(MissionHandle.Parse("websearch"), version: null, CancellationToken.None);
        var explicitPublisher = await catalog.ResolveAsync(MissionHandle.Parse("forge/websearch"), version: null, CancellationToken.None);

        Assert.NotNull(bare);
        Assert.NotNull(explicitPublisher);
        Assert.Equal(bare, explicitPublisher);
    }

    [Fact]
    public async Task Unrecognized_publisher_fails_closed()
    {
        var catalog = CatalogWith("WebSearch");

        var result = await catalog.ResolveAsync(MissionHandle.Parse("randomguy/websearch"), version: null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Unknown_mission_name_does_not_resolve()
    {
        var catalog = CatalogWith("WebSearch");

        var result = await catalog.ResolveAsync(MissionHandle.Parse("nonexistent"), version: null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Entry_is_absent_when_the_runner_does_not_advertise_its_mission_ref()
    {
        // No provider key on the runner for WebSearch's backing mission — same "silently skip"
        // behaviour AgentRegistry gives a built-in whose ref isn't currently loadable.
        var catalog = CatalogWith(/* nothing advertised */);

        var result = await catalog.ResolveAsync(MissionHandle.Parse("websearch"), version: null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Search_filters_by_query_and_publisher()
    {
        var catalog = CatalogWith("WebSearch");

        var byQuery = await catalog.SearchAsync("search", publisher: null, CancellationToken.None);
        Assert.Single(byQuery);

        var noMatch = await catalog.SearchAsync("nope", publisher: null, CancellationToken.None);
        Assert.Empty(noMatch);

        var wrongPublisher = await catalog.SearchAsync(query: null, publisher: "randomguy", CancellationToken.None);
        Assert.Empty(wrongPublisher);

        var rightPublisher = await catalog.SearchAsync(query: null, publisher: "forge", CancellationToken.None);
        Assert.Single(rightPublisher);
    }

    [Fact]
    public async Task Ocr_resolves_when_runner_advertises_backing_mission_ref()
    {
        var catalog = CatalogWith("Ocr");

        var result = await catalog.ResolveAsync(MissionHandle.Parse("ocr"), version: null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Ocr", result.MissionRef);
    }
}
