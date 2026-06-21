using System.Runtime.CompilerServices;
using System.Text.Json;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Adapters;

public class DirectExpertRunner(IChatClient chatClient) : IExpertRunner
{
    // StepEnvelopeContext.Default.Options has PropertyNameCaseInsensitive = true
    // and a TypeInfoResolver — required for AOT's GetResponseAsync<T>.
    private static readonly JsonSerializerOptions _jsonOptions = StepEnvelopeContext.Default.Options;

    // Appended to system prompts in streaming mode (structured output not available for streaming).
    private const string JudgeStreamingInstruction = """


Respond with this exact JSON format and nothing else:
{"text": "<your complete response>", "status": "pass"}
Or on failure:
{"text": "<brief summary>", "status": "fail", "reason": "<which criterion failed>"}
""";

    private const string CriticStreamingInstruction = """


Respond with this exact JSON format and nothing else — status must always be "pass":
{"text": "<your complete response>", "status": "pass"}
""";

    public async Task<StepEnvelope> RunAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        CancellationToken ct = default)
    {
        var (userMessage, systemPrompt) = BuildMessages(expert, context);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userMessage)
        };
        var response = await chatClient.GetResponseAsync<StepEnvelope>(messages, _jsonOptions, cancellationToken: ct);
        var envelope = response.Result;

        // Non-judge experts always continue the pipeline — enforce pass regardless of LLM output.
        return expert.IsJudge ? envelope : envelope with { Status = "pass", Reason = null };
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ExpertDefinition expert,
        Dictionary<string, object> context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var instruction = expert.IsJudge ? JudgeStreamingInstruction : CriticStreamingInstruction;
        var (userMessage, systemPrompt) = BuildMessages(expert, context);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt + instruction),
            new(ChatRole.User, userMessage)
        };
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, cancellationToken: ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    private static (string userMessage, string systemPrompt) BuildMessages(
        ExpertDefinition expert,
        Dictionary<string, object> context)
    {
        var userMessage  = context.TryGetValue("output", out var output) && !string.IsNullOrWhiteSpace(output?.ToString())
            ? output.ToString()!
            : "Begin.";
        var systemPrompt = ContextInterpolator.Interpolate(expert.SystemPrompt, context);
        return (userMessage, systemPrompt);
    }
}
