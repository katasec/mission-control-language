namespace ForgeMission.Core.Tools;

public sealed class ReadToolExecutor : IToolExecutor
{
    public string Name => "Read";

    public async Task<ToolExecutionResult> ExecuteAsync(
        IDictionary<string, object?>? arguments, IWorkspace workspace, CancellationToken ct = default)
    {
        if (!ToolArguments.TryGetString(arguments, "file_path", out var filePath))
            return ToolExecutionResult.Error("file_path is required");

        if (!workspace.TryResolvePath(filePath, out var resolved, out var pathError))
            return ToolExecutionResult.Error(pathError!);

        if (!await workspace.ExistsAsync(resolved, ct))
            return ToolExecutionResult.Error($"File not found: {filePath}");

        try
        {
            var content = await workspace.ReadFileAsync(resolved, ct);
            var lines   = ReadLines(content);

            // offset = lines to skip from the start (0 = from the beginning); limit = how many
            // lines to return after that. Both optional, matching this session's own Read tool.
            var offset = ToolArguments.TryGetInt(arguments, "offset") is { } o and >= 0 ? o : 0;
            var limit  = ToolArguments.TryGetInt(arguments, "limit")  is { } l and >= 0 ? l : lines.Count;

            var slice = lines.Skip(offset).Take(limit);
            return new ToolExecutionResult(string.Join('\n', slice));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolExecutionResult.Error($"Failed to read {filePath}: {ex.Message}");
        }
    }

    private static List<string> ReadLines(string content)
    {
        var lines = new List<string>();
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
            lines.Add(line);
        return lines;
    }
}
