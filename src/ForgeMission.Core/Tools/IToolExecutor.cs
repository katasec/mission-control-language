namespace ForgeMission.Core.Tools;

// One executor per tool, dispatched by name via ToolExecutorRegistry — mirrors the
// expert.Kind switch convention (PipelineRunner.cs) applied to tool names instead of kinds.
public interface IToolExecutor
{
    string Name { get; }

    Task<ToolExecutionResult> ExecuteAsync(
        IDictionary<string, object?>? arguments,
        ICapabilityDispatcher dispatcher,
        CancellationToken ct = default);
}

// Content always populates FunctionResultContent.Result either way — Microsoft.Extensions.AI
// has no dedicated error flag on that type (errors are just descriptive text the model reads).
// IsError is this layer's own signal for callers that want to react differently (logging,
// the future approval hook) without re-parsing the content string.
public sealed record ToolExecutionResult(string Content, bool IsError = false)
{
    public static ToolExecutionResult Error(string message) => new(message, IsError: true);
}
