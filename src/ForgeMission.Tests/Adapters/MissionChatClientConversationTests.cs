using System.Text.Json;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Runtime;
using Katasec.AnthropicServer;
using Microsoft.Extensions.AI;

namespace ForgeMission.Tests.Adapters;

/// <summary>
/// Goal extraction + conversation structure (Phase 42.1 task 4), verified against the sanitized
/// wire fixture captured from the real claude CLI (2.1.195, 2026-07-16). The CLI's main-loop
/// request carries FOUR text blocks in the last user message — three system-reminder scaffolding
/// blocks followed by the real 106-char prompt (marked with cache_control). The goal must be the
/// LAST TEXT BLOCK, never the concatenation. Re-capture and re-verify on CLI version bumps.
/// </summary>
public sealed class MissionChatClientConversationTests
{
    private const string ExpectedGoal =
        "Read the file /var/folders/ab/c1d2e3f4g5h6i7j8k9l0m1n2o3p4q5/T/forge-probe.txt and tell me the magic word.";

    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "anthropic-wire", "main-loop-4-block-user.json");

    private static List<ChatMessage> LoadFixtureHistory()
    {
        var json = File.ReadAllText(FixturePath);
        var request = JsonSerializer.Deserialize(json, AnthropicJsonContext.Default.AnthropicRequest);
        Assert.NotNull(request);
        return AnthropicServer.BuildChatHistory(request);
    }

    [Fact]
    public void ExtractGoal_FourBlockUserMessage_ReturnsLastTextBlockOnly()
    {
        var goal = MissionChatClient.ExtractGoal(LoadFixtureHistory());

        Assert.Equal(ExpectedGoal, goal);
        Assert.Equal(106, goal.Length);
    }

    [Fact]
    public void BuildChatHistory_PreservesBlockBoundaries()
    {
        var history = LoadFixtureHistory();

        var lastUser = history.Last(m => m.Role == ChatRole.User);
        var blocks   = lastUser.Contents.OfType<TextContent>().ToList();

        Assert.Equal(4, blocks.Count);
        Assert.StartsWith("<system-reminder>", blocks[0].Text);
        Assert.Equal(ExpectedGoal, blocks[3].Text);

        // System blocks survive as a structured system message, not a flattened string.
        var system = Assert.Single(history, m => m.Role == ChatRole.System);
        Assert.Equal(3, system.Contents.OfType<TextContent>().Count());
    }

    [Fact]
    public void Conversation_ToString_RendersRoleTaggedTranscript()
    {
        var conversation = new Conversation(LoadFixtureHistory());
        var transcript   = conversation.ToString();

        Assert.Contains("user: ", transcript);
        Assert.Contains(ExpectedGoal, transcript);          // template consumers see the full turn
        Assert.Contains("<system-reminder>", transcript);   // scaffolding travels in the conversation
    }

    [Fact]
    public void ContextBuilderSeed_AppliesStructuredObjects()
    {
        var ast     = ForgeMission.Parser.MclParser.Parse("mission Noop(goal) = { PassThrough }\noutput(Noop)");
        var objects = new Dictionary<string, object> { ["conversation"] = new Conversation([]) };

        var context = ContextBuilder.Seed(ast, new Dictionary<string, string> { ["goal"] = "hi" }, objects);

        Assert.IsType<Conversation>(context["conversation"]);
        Assert.Equal("hi", context["goal"]);
    }
}
