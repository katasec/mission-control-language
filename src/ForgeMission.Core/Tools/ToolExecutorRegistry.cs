using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Tools;

// Name-keyed dispatch, mirroring PipelineRunner's expert.Kind switch. Consumes FunctionCallContent
// directly — the same type MissionResult.ToolCalls already carries — so the future AgenticSession
// (task 3) can execute a call without any translation step.
public sealed class ToolExecutorRegistry
{
    private readonly IReadOnlyDictionary<string, IToolExecutor> _executors;

    public ToolExecutorRegistry(IEnumerable<IToolExecutor>? executors = null)
        => _executors = (executors ?? Default()).ToDictionary(e => e.Name, StringComparer.Ordinal);

    public static IReadOnlyList<IToolExecutor> Default() =>
        [new ReadToolExecutor(), new EditToolExecutor(), new WriteToolExecutor(), new BashToolExecutor()];

    public Task<ToolExecutionResult> ExecuteAsync(
        FunctionCallContent call,
        ICapabilityDispatcher dispatcher,
        CancellationToken ct = default)
        => _executors.TryGetValue(call.Name, out var executor)
            ? executor.ExecuteAsync(call.Arguments, dispatcher, ct)
            : Task.FromResult(ToolExecutionResult.Error($"Unknown tool: {call.Name}"));

    // Lets a caller reject an unsupported tool name before constructing a call/request for it —
    // used by the durable Janus tool hand-off (Task 7), which must not attempt to dispatch a
    // name this registry has no executor for.
    public bool CanExecute(string name) => _executors.ContainsKey(name);
}
