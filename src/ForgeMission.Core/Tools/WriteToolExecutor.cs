namespace ForgeMission.Core.Tools;

public sealed class WriteToolExecutor : IToolExecutor
{
    public string Name => "Write";

    public async Task<ToolExecutionResult> ExecuteAsync(
        IDictionary<string, object?>? arguments, ICapabilityDispatcher dispatcher, CancellationToken ct = default)
    {
        if (!ToolArguments.TryGetString(arguments, "file_path", out var filePath))
            return await dispatcher.DispatchAsync(
                "file", new InvalidCapabilityRequest("Invalid Write request", "file_path is required"), ct);
        if (!ToolArguments.TryGetString(arguments, "content", out var content))
            return await dispatcher.DispatchAsync(
                "file", new InvalidCapabilityRequest("Invalid Write request", "content is required"), ct);

        return await dispatcher.DispatchAsync("file", new WriteFileCapabilityRequest(filePath, content), ct);
    }
}
