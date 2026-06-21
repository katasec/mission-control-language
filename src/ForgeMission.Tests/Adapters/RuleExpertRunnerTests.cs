using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;

namespace ForgeMission.Tests.Adapters;

public class RuleExpertRunnerTests
{
    private static ExpertDefinition RuleExpert(string check, string onFail = "") =>
        new("WordCheck", "Text", "Result", "", Kind: "rule", Check: check, OnFail: onFail);

    [Fact]
    public async Task RunAsync_CheckPasses_ReturnsPassEnvelope()
    {
        var runner  = new RuleExpertRunner();
        var expert  = RuleExpert("word_count > 2");
        var context = new Dictionary<string, object> { ["output"] = "one two three" };

        var result = await runner.RunAsync(expert, context);

        Assert.Equal("pass",            result.Status);
        Assert.Equal("one two three",   result.Text);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task RunAsync_CheckFails_ReturnsFailEnvelopeWithOnFailMessage()
    {
        var runner  = new RuleExpertRunner();
        var expert  = RuleExpert("word_count > 10", onFail: "Response is too short, add more detail.");
        var context = new Dictionary<string, object> { ["output"] = "too short" };

        var result = await runner.RunAsync(expert, context);

        Assert.Equal("fail",                                      result.Status);
        Assert.Equal("Response is too short, add more detail.",   result.Reason);
    }

    [Fact]
    public async Task RunAsync_CheckFails_WritesOnFailToContext()
    {
        var runner  = new RuleExpertRunner();
        var expert  = RuleExpert("word_count > 10", onFail: "Write more words.");
        var context = new Dictionary<string, object> { ["output"] = "short" };

        await runner.RunAsync(expert, context);

        Assert.True(context.TryGetValue("feedback", out var fb));
        Assert.Equal("Write more words.", fb?.ToString());
    }

    [Fact]
    public async Task RunAsync_CheckFails_EmptyOnFail_UsesFallbackMessage()
    {
        var runner  = new RuleExpertRunner();
        var expert  = RuleExpert("word_count > 100");
        var context = new Dictionary<string, object> { ["output"] = "short" };

        var result = await runner.RunAsync(expert, context);

        Assert.Equal("fail",               result.Status);
        Assert.Equal("Rule check failed.", result.Reason);
    }

    [Fact]
    public async Task RunAsync_NoOutputInContext_TreatsAsEmptyString()
    {
        var runner  = new RuleExpertRunner();
        var expert  = RuleExpert("word_count == 0");
        var context = new Dictionary<string, object>();

        var result = await runner.RunAsync(expert, context);

        Assert.Equal("pass", result.Status);
    }

    [Fact]
    public async Task StreamAsync_YieldsSingleChunk()
    {
        var runner  = new RuleExpertRunner();
        var expert  = RuleExpert("word_count > 0");
        var context = new Dictionary<string, object> { ["output"] = "hello" };

        var chunks = new List<string>();
        await foreach (var chunk in runner.StreamAsync(expert, context))
            chunks.Add(chunk);

        Assert.Single(chunks);
        Assert.Equal("hello", chunks[0]);
    }
}
