using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Parser;
using ForgeMission.Core.Runtime;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ForgeMission.Tests.Adapters;

/// <summary>
/// Integration tests against a real LLM. Skipped automatically if OPENAI_API_KEY is not set.
/// Run with: OPENAI_API_KEY=sk-... dotnet test --filter Category=Integration
/// </summary>
public class MafExpertRunnerIntegrationTests
{
    private static readonly string? ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    private static IChatClient BuildChatClient()
    {
        var openAiClient = new OpenAIClient(ApiKey!);
        return openAiClient.GetChatClient("gpt-4o-mini").AsIChatClient();
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task SingleExpert_RealLlm_ReturnsNonEmptyResponse()
    {
        Skip.If(string.IsNullOrWhiteSpace(ApiKey), "OPENAI_API_KEY not set — skipping integration test");

        var expert = new ExpertDefinition(
            "Summariser",
            "Text",
            "Summary",
            "You are a concise summariser. Summarise the input in one sentence.");

        var runner = new MafExpertRunner(BuildChatClient());
        var context = new Dictionary<string, object> { ["output"] = "The sky is blue because of Rayleigh scattering of sunlight." };
        var result = await runner.RunAsync(expert, context);

        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task TwoStepPipeline_RealLlm_ContextFlowsBetweenSteps()
    {
        Skip.If(string.IsNullOrWhiteSpace(ApiKey), "OPENAI_API_KEY not set — skipping integration test");

        var ast = FmsParser.Parse("""
            mission TestMission =
                Summariser
                |> Reviewer
            """);

        var experts = new Dictionary<string, ExpertDefinition>
        {
            ["Summariser"] = new("Summariser", "Text", "Summary",
                "You are a concise summariser. Summarise the input in exactly one sentence."),
            ["Reviewer"]   = new("Reviewer", "Summary", "Review",
                "You are a reviewer. Say whether the summary you received is concise. Reply with only 'Yes' or 'No'.")
        };

        var stepWriter = new StringWriter();
        var runner     = new PipelineRunner(new MafExpertRunner(BuildChatClient()));
        var options    = new PipelineRunOptions("TestMission", StepWriter: stepWriter);

        var result = await runner.RunAsync(ast, experts, options);

        Assert.False(string.IsNullOrWhiteSpace(result.Text));
        Assert.Contains("Summariser", stepWriter.ToString());
        Assert.Contains("Reviewer", stepWriter.ToString());
    }
}
