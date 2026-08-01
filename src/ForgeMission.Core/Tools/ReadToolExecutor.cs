namespace ForgeMission.Core.Tools;

public sealed class ReadToolExecutor : IToolExecutor
{
    public string Name => "Read";

    public async Task<ToolExecutionResult> ExecuteAsync(
        IDictionary<string, object?>? arguments, ICapabilityDispatcher dispatcher, CancellationToken ct = default)
    {
        if (!ToolArguments.TryGetString(arguments, "file_path", out var filePath))
            return await dispatcher.DispatchAsync(
                "file", new InvalidCapabilityRequest("Invalid Read request", "file_path is required"), ct);

        // offset = lines to skip from the start (0 = from the beginning); limit = how many
        // lines to return after that. Both optional, matching this session's own Read tool.
        var offset = ToolArguments.TryGetInt(arguments, "offset") is { } o and >= 0 ? o : 0;
        int? limit = ToolArguments.TryGetInt(arguments, "limit") is { } l and >= 0 ? l : null;
        return await dispatcher.DispatchAsync("file", new ReadFileCapabilityRequest(filePath, offset, limit), ct);
    }
}
