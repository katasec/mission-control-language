using ForgeMission.Core.Experts;
using ForgeMission.Core.Tools;
using ForgeMission.Parser;
using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Runtime;

// Runs a tool-capable agent mission end-to-end in-process — no wire protocol, no external `claude`
// CLI (Phase 43.1). PipelineRunner.RunAsync returns MissionResult.ToolCalls when the role:agent
// expert wants a tool run; AgenticSession executes each call itself via ToolExecutorRegistry, then
// resumes the SAME mission at the agent segment (StartAtAgent) with the growing conversation, until
// a turn comes back with text instead of tool calls. State lives in a plain in-memory
// List<ChatMessage> for the whole exchange — no IEnrichmentCache/ConversationHash, because those
// solve stateless-HTTP re-derivation and this loop never leaves one process.
public sealed class AgenticSession(
    Program ast,
    Dictionary<string, ExpertDefinition> experts,
    PipelineRunner pipelineRunner,
    CapabilityRegistry capabilities,
    ICapabilityDispatcher dispatcher,
    ToolExecutorRegistry? toolExecutors = null,
    Func<ToolCallNotification, CancellationToken, Task>? notifyToolCall = null)
{
    private readonly ToolExecutorRegistry _toolExecutors = toolExecutors ?? new ToolExecutorRegistry();

    private readonly Func<ToolCallNotification, CancellationToken, Task> _notifyToolCall =
        notifyToolCall ?? ((_, _) => Task.CompletedTask);

    public async Task<MissionResult> RunAsync(PipelineRunOptions options, CancellationToken ct = default)
    {
        var toolOptions = options with { Tools = capabilities.ToolDeclarations.ToList() };
        var result      = await pipelineRunner.RunAsync(ast, experts, toolOptions, ct);

        if (result.ToolCalls is not { Count: > 0 })
            return result;

        // The externally-visible conversation starts at the user's goal — NOT the pre-agent
        // segment's internal enrichment chain, which is an engine detail the model never saw as
        // "the user". Same shape MissionChatClient builds from the real client's wire messages.
        var messages = new List<ChatMessage> { InitialGoalMessage(options) };

        while (result.ToolCalls is { Count: > 0 } calls)
        {
            var resultContents = new List<AIContent>();
            foreach (var call in calls)
            {
                ct.ThrowIfCancellationRequested();

                await _notifyToolCall(new ToolCallNotification(call, ToolCallNotificationState.Running), ct);
                var toolResult = await _toolExecutors.ExecuteAsync(call, dispatcher, ct);
                await _notifyToolCall(new ToolCallNotification(call, ToolCallNotificationState.Done, toolResult), ct);

                resultContents.Add(new FunctionResultContent(call.CallId, toolResult.Content));
            }

            messages.Add(new ChatMessage(ChatRole.Assistant, calls.Cast<AIContent>().ToList()));
            messages.Add(new ChatMessage(ChatRole.Tool, resultContents));

            var continuation = toolOptions with
            {
                StartAtAgent   = true,
                ContextObjects = new Dictionary<string, object> { ["conversation"] = new Conversation(messages) },
            };
            result = await pipelineRunner.RunAsync(ast, experts, continuation, ct);
        }

        return result;
    }

    private ChatMessage InitialGoalMessage(PipelineRunOptions options)
    {
        var mission   = ast.Declarations.OfType<MissionDeclaration>().First(m => m.Name == options.MissionName);
        var paramName = mission.Params.FirstOrDefault() ?? "goal";
        var goal      = options.Vars?.GetValueOrDefault(paramName) ?? string.Empty;
        return new ChatMessage(ChatRole.User, goal);
    }
}
