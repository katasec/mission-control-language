using System.Text.Json;
using ForgeMission.ChatClients;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Resolution;
using ForgeMission.Core.Runtime;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Parser;
using Katasec.AITools;
using Microsoft.Extensions.AI;
using MclProgram = ForgeMission.Parser.Program;

namespace ForgeMission.ConversationWorker.Janus;

/// <summary>
/// Runs the real Janus mission (<c>Negotiate -&gt; Implement</c>) via <see cref="PipelineRunner"/>,
/// translating every trace fact through <see cref="JanusPipelineProgressMapper"/> into the
/// caller-supplied publish callback. Negotiate and Implement are run as two separate top-level
/// <c>RunAsync</c> calls — not by invoking the umbrella "Janus" mission — because a tool-call pause
/// inside a sub-mission call does not propagate out of <c>PipelineRunner</c>'s own sub-mission step
/// handling; running Implement directly at the top level is what lets its pause reach the caller.
/// </summary>
public static class JanusMissionExecutor
{
    public static async Task<MissionResult> RunFullMissionAsync(
        WorkerMissionContext mission,
        string goal,
        IReadOnlyList<ConversationCapabilityDeclaration> capabilities,
        Func<MappedProgressFact, CancellationToken, Task> publishFactAsync,
        Func<string, CancellationToken, Task> onApprovedPlanAsync,
        CancellationToken ct)
    {
        var runner = new PipelineRunner(mission.Runners, mission.Execution);
        string? approvedPlan = null;

        async Task OnTrace(PipelineTraceEvent traceEvent, CancellationToken innerCt)
        {
            if (traceEvent is PipelineStepCompleted { ExpertName: "Approver" } completed
                && string.Equals(completed.Envelope.Status, "pass", StringComparison.OrdinalIgnoreCase))
            {
                approvedPlan = completed.Envelope.Text;
                await onApprovedPlanAsync(approvedPlan, innerCt);
            }

            foreach (var fact in JanusPipelineProgressMapper.MapTraceEvent(traceEvent, mission.Experts))
                await publishFactAsync(fact, innerCt);
        }

        var negotiateOptions = new PipelineRunOptions(
            MissionName: "Negotiate",
            Vars: new Dictionary<string, string> { ["task"] = goal },
            MissionPath: ["Janus", "Negotiate"],
            OnTrace: OnTrace);
        var negotiateResult = await runner.RunAsync(mission.Ast, mission.Experts, negotiateOptions, ct);

        if (negotiateResult.Status == MissionStatus.Fail || approvedPlan is null)
            return negotiateResult with
            {
                Status = MissionStatus.Fail,
                FailReason = negotiateResult.FailReason ?? "Negotiation did not reach an approved plan.",
            };

        var implementOptions = new PipelineRunOptions(
            MissionName: "Implement",
            Vars: new Dictionary<string, string> { ["plan"] = approvedPlan },
            Tools: ToTools(capabilities),
            MissionPath: ["Janus", "Implement"],
            OnTrace: OnTrace,
            // Janus v1 supports exactly one tool call per request (JanusPipelineProgressMapper);
            // this constrains the provider request itself rather than hoping the model complies
            // (Phase 43.16 Task 8b).
            AllowMultipleToolCalls: false);
        return await runner.RunAsync(mission.Ast, mission.Experts, implementOptions, ct);
    }

    public static async Task<MissionResult> RunContinuationAsync(
        WorkerMissionContext mission,
        string approvedPlan,
        string providerCallId,
        string toolName,
        JsonElement toolArguments,
        ConversationToolResult toolResult,
        IReadOnlyList<ConversationCapabilityDeclaration> capabilities,
        Func<MappedProgressFact, CancellationToken, Task> publishFactAsync,
        CancellationToken ct)
    {
        var runner = new PipelineRunner(mission.Runners, mission.Execution);

        var conversation = new Conversation([
            new ChatMessage(ChatRole.User, "Begin."),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(providerCallId, toolName, ToArguments(toolArguments))]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent(providerCallId, toolResult.Content)]),
        ]);

        async Task OnTrace(PipelineTraceEvent traceEvent, CancellationToken innerCt)
        {
            foreach (var fact in JanusPipelineProgressMapper.MapTraceEvent(traceEvent, mission.Experts))
                await publishFactAsync(fact, innerCt);
        }

        var options = new PipelineRunOptions(
            MissionName: "Implement",
            Vars: new Dictionary<string, string> { ["plan"] = approvedPlan },
            ContextObjects: new Dictionary<string, object> { ["conversation"] = conversation },
            Tools: ToTools(capabilities),
            StartAtAgent: true,
            MissionPath: ["Janus", "Implement"],
            OnTrace: OnTrace,
            // Same one-tool-call-per-request constraint as the initial run above — the resumed
            // Implementer call must stay inside the same v1 contract (Phase 43.16 Task 8b).
            AllowMultipleToolCalls: false);

        return await runner.RunAsync(mission.Ast, mission.Experts, options, ct);
    }

    private static IList<AITool>? ToTools(IReadOnlyList<ConversationCapabilityDeclaration> capabilities)
        => capabilities.Count == 0
            ? null
            : capabilities.Select(c => (AITool)new DeclaredTool(c.Name, c.Description, c.InputSchema)).ToList();

    // Mirrors RunnerToolTurnMapper's JsonElement->arguments conversion (Worker cannot reference
    // ForgeMission.Runner, whose InternalsVisibleTo does not extend here) — same canonical value
    // set, no reflection.
    private static IDictionary<string, object?> ToArguments(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, object?>();

        return input.EnumerateObject().ToDictionary(
            property => property.Name,
            property => ToObject(property.Value),
            StringComparer.Ordinal);
    }

    private static object? ToObject(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.Null => null,
        _ => value.Clone(),
    };
}
