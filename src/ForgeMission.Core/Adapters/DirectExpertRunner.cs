using System.Runtime.CompilerServices;
using System.Text.Json;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Adapters;

public class DirectExpertRunner(IChatClient chatClient) : IExpertRunner
{
    // Explicit closed schema for structured output. GetResponseAsync<StepEnvelope> auto-derives a
    // schema from the record, but StepEnvelope.Meta is an open dictionary, which produces an object
    // without `additionalProperties: false`. Anthropic's structured output rejects that ("For 'object'
    // type, 'additionalProperties' must be explicitly set to false") while OpenAI tolerates it — so a
    // hand-written closed schema (text/status/reason only; Meta is never set by the LLM path) is the
    // only shape both providers accept. All three fields are `required` to satisfy OpenAI strict mode;
    // `reason` is nullable. The backing JsonDocument is held statically so its JsonElement stays valid.
    private const string StepEnvelopeSchemaJson = """
    {
      "type": "object",
      "properties": {
        "text":   { "type": "string" },
        "status": { "type": "string", "enum": ["pass", "fail"] },
        "reason": { "type": ["string", "null"] }
      },
      "required": ["text", "status", "reason"],
      "additionalProperties": false
    }
    """;

    private static readonly JsonDocument _schemaDoc = JsonDocument.Parse(StepEnvelopeSchemaJson);

    private static readonly ChatResponseFormat _stepFormat = ChatResponseFormat.ForJsonSchema(
        _schemaDoc.RootElement,
        schemaName: "step_envelope",
        schemaDescription: "A step result: the answer text plus a pass/fail status and optional reason.");

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
        // Non-generic call with an explicit closed schema (see _stepFormat) rather than
        // GetResponseAsync<StepEnvelope>, whose derived schema is rejected by Anthropic. Deserialize
        // via the source-gen context (AOT-safe); fall back to the raw text if the model returns
        // non-JSON for any reason.
        var options = new ChatOptions { ResponseFormat = _stepFormat };

        // Tool-capable agent expert (42.3): the PipelineRunner put the client's (allowlist-filtered)
        // tools in the bag for this step only. Free-form reply instead of the forced envelope —
        // the model must be able to answer OR call a tool; the raw-text fallback below still applies.
        if (context.TryGetValue("tools", out var t) && t is IList<AITool> tools)
        {
            options.Tools          = tools;
            options.ResponseFormat = null;
        }

        var response = await chatClient.GetResponseAsync(messages, options, cancellationToken: ct);

        // The model called a tool: hand the calls back through the bag — the pipeline returns them
        // to the client (which executes) instead of running any further steps.
        var toolCalls = response.Messages.LastOrDefault()?.Contents.OfType<FunctionCallContent>().ToList();
        if (toolCalls is { Count: > 0 })
        {
            context["tool_calls"] = toolCalls;
            var text = response.Messages.LastOrDefault()?.Contents
                .OfType<TextContent>().Select(c => c.Text).FirstOrDefault() ?? string.Empty;
            return new StepEnvelope(text);
        }

        StepEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize(response.Text, StepEnvelopeContext.Default.StepEnvelope)
                       ?? new StepEnvelope(response.Text);
        }
        catch (JsonException)
        {
            envelope = new StepEnvelope(response.Text);
        }

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
