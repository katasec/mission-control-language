namespace ForgeMission.Core.Tools;

public sealed class WriteToolExecutor : IToolExecutor
{
    public string Name => "Write";

    public async Task<ToolExecutionResult> ExecuteAsync(
        IDictionary<string, object?>? arguments, string workspaceRoot, CancellationToken ct = default)
    {
        if (!ToolArguments.TryGetString(arguments, "file_path", out var filePath))
            return ToolExecutionResult.Error("file_path is required");
        if (!ToolArguments.TryGetString(arguments, "content", out var content))
            return ToolExecutionResult.Error("content is required");

        if (!WorkspaceGuard.TryResolve(workspaceRoot, filePath, out var resolved, out var pathError))
            return ToolExecutionResult.Error(pathError!);

        try
        {
            await File.WriteAllTextAsync(resolved, content, ct);
            return new ToolExecutionResult($"Wrote {filePath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolExecutionResult.Error($"Failed to write {filePath}: {ex.Message}");
        }
    }
}
