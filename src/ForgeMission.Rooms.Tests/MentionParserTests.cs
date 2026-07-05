using ForgeMission.Rooms;

namespace ForgeMission.Rooms.Tests;

public sealed class MentionParserTests
{
    private static readonly string[] Handles = ["@forge/hallucination-guard", "@forge/summarizer"];

    [Fact]
    public void Addresses_an_agent_and_extracts_the_prompt()
    {
        var hit = MentionParser.Detect(
            "@forge/hallucination-guard which month has an X in the middle", Handles);

        Assert.NotNull(hit);
        Assert.Equal("@forge/hallucination-guard", hit!.Value.Handle);
        Assert.Equal("which month has an X in the middle", hit.Value.Prompt);
    }

    [Fact]
    public void Strips_surrounding_quotes_from_the_prompt()
    {
        var hit = MentionParser.Detect("@forge/hallucination-guard \"is this bill correct\"", Handles);

        Assert.Equal("is this bill correct", hit!.Value.Prompt);
    }

    [Fact]
    public void Mention_can_trail_the_prompt()
    {
        var hit = MentionParser.Detect("please review the above @forge/summarizer", Handles);

        Assert.Equal("@forge/summarizer", hit!.Value.Handle);
        Assert.Equal("please review the above", hit.Value.Prompt);
    }

    [Fact]
    public void Longest_matching_handle_wins()
    {
        string[] overlapping = ["@forge", "@forge/hallucination-guard"];
        var hit = MentionParser.Detect("@forge/hallucination-guard check this", overlapping);

        Assert.Equal("@forge/hallucination-guard", hit!.Value.Handle);
    }

    [Fact]
    public void Non_addressed_message_is_just_chat()
    {
        Assert.Null(MentionParser.Detect("hey Bob did you see the game last night", Handles));
        Assert.Null(MentionParser.Detect("", Handles));
        Assert.Null(MentionParser.Detect("   ", Handles));
    }

    [Fact]
    public void No_agents_in_room_means_no_mention()
    {
        Assert.Null(MentionParser.Detect("@forge/hallucination-guard hello", []));
    }
}
