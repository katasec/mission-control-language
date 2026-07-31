namespace ForgeMission.Core.Tools;

// Exact string replacement — same contract as this session's own Edit tool: fails unless
// old_string is unique in the file, unless replace_all is set. File must already exist;
// creating a new file is Write's job, not Edit's.
public sealed class EditToolExecutor : IToolExecutor
{
    public string Name => "Edit";

    public async Task<ToolExecutionResult> ExecuteAsync(
        IDictionary<string, object?>? arguments, CapabilityRegistry capabilities, CancellationToken ct = default)
    {
        if (!ToolArguments.TryGetString(arguments, "file_path", out var filePath))
            return ToolExecutionResult.Error("file_path is required");
        if (!ToolArguments.TryGetString(arguments, "old_string", out var oldString))
            return ToolExecutionResult.Error("old_string is required");
        if (!ToolArguments.TryGetString(arguments, "new_string", out var newString))
            return ToolExecutionResult.Error("new_string is required");

        if (oldString == newString)
            return ToolExecutionResult.Error("new_string must be different from old_string");
        if (!capabilities.TryGetProvider<IFileProvider>(out var files) || files is null)
            return ToolExecutionResult.Error("File capability is unavailable.");

        var replaceAll = ToolArguments.GetBool(arguments, "replace_all");

        if (!files.TryResolvePath(filePath, out var resolved, out var pathError))
            return ToolExecutionResult.Error(pathError!);

        if (!await files.ExistsAsync(resolved, ct))
            return ToolExecutionResult.Error($"File not found: {filePath}. Use Write to create a new file.");

        try
        {
            var content = await files.ReadFileAsync(resolved, ct);
            var occurrences = CountOccurrences(content, oldString);

            if (occurrences == 0)
                return ToolExecutionResult.Error("old_string not found in file");
            if (occurrences > 1 && !replaceAll)
                return ToolExecutionResult.Error(
                    $"old_string is not unique ({occurrences} matches) — pass replace_all: true, " +
                    "or include more surrounding context to make it unique");

            var updated = replaceAll
                ? content.Replace(oldString, newString)
                : ReplaceFirst(content, oldString, newString);

            await files.WriteFileAsync(resolved, updated, ct);
            return new ToolExecutionResult(replaceAll
                ? $"Edited {filePath} ({occurrences} replacements)"
                : $"Edited {filePath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolExecutionResult.Error($"Failed to edit {filePath}: {ex.Message}");
        }
    }

    private static int CountOccurrences(string content, string target)
    {
        var count = 0;
        var index = 0;
        while ((index = content.IndexOf(target, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += target.Length;
        }
        return count;
    }

    private static string ReplaceFirst(string content, string oldString, string newString)
    {
        var index = content.IndexOf(oldString, StringComparison.Ordinal);
        return content[..index] + newString + content[(index + oldString.Length)..];
    }
}
