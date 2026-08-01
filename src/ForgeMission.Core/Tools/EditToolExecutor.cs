namespace ForgeMission.Core.Tools;

// Exact string replacement — same contract as this session's own Edit tool: fails unless
// old_string is unique in the file, unless replace_all is set. File must already exist;
// creating a new file is Write's job, not Edit's.
public sealed class EditToolExecutor : IToolExecutor
{
    public string Name => "Edit";

    public async Task<ToolExecutionResult> ExecuteAsync(
        IDictionary<string, object?>? arguments, ICapabilityDispatcher dispatcher, CancellationToken ct = default)
    {
        if (!ToolArguments.TryGetString(arguments, "file_path", out var filePath))
            return await dispatcher.DispatchAsync(
                "file", new InvalidCapabilityRequest("Invalid Edit request", "file_path is required"), ct);
        if (!ToolArguments.TryGetString(arguments, "old_string", out var oldString))
            return await dispatcher.DispatchAsync(
                "file", new InvalidCapabilityRequest("Invalid Edit request", "old_string is required"), ct);
        if (!ToolArguments.TryGetString(arguments, "new_string", out var newString))
            return await dispatcher.DispatchAsync(
                "file", new InvalidCapabilityRequest("Invalid Edit request", "new_string is required"), ct);

        if (oldString == newString)
            return await dispatcher.DispatchAsync("file",
                new InvalidCapabilityRequest("Invalid Edit request", "new_string must be different from old_string"), ct);

        var replaceAll = ToolArguments.GetBool(arguments, "replace_all");
        return await dispatcher.DispatchAsync(
            "file", new EditFileCapabilityRequest(filePath, oldString, newString, replaceAll), ct);
    }
}
