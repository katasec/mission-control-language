using ForgeMission.Api;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// Docker-style handle parsing (42.6 task 5a) — pure, no I/O. Implicit and explicit publisher share
/// one path (see <see cref="StaticMissionCatalogTests"/> for the resolution-side guarantee this
/// parsing feeds).
/// </summary>
public sealed class MissionHandleTests
{
    [Fact]
    public void Bare_name_has_no_publisher()
    {
        var handle = MissionHandle.Parse("websearch");
        Assert.Null(handle.Publisher);
        Assert.Equal("websearch", handle.Name);
    }

    [Fact]
    public void Publisher_slash_name_splits_on_the_first_slash()
    {
        var handle = MissionHandle.Parse("forge/websearch");
        Assert.Equal("forge", handle.Publisher);
        Assert.Equal("websearch", handle.Name);
    }

    [Fact]
    public void Parsing_lowercases_both_parts()
    {
        var handle = MissionHandle.Parse("Forge/WebSearch");
        Assert.Equal("forge", handle.Publisher);
        Assert.Equal("websearch", handle.Name);
    }

    [Fact]
    public void Leading_and_trailing_whitespace_is_trimmed()
    {
        var handle = MissionHandle.Parse("  websearch  ");
        Assert.Equal("websearch", handle.Name);
    }

    [Fact]
    public void Only_the_first_slash_is_significant()
    {
        // A namespace/name/extra-segment handle isn't part of today's design, but the parser
        // shouldn't silently drop data — everything past the first slash belongs to Name.
        var handle = MissionHandle.Parse("forge/web/search");
        Assert.Equal("forge", handle.Publisher);
        Assert.Equal("web/search", handle.Name);
    }
}
