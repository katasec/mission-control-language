namespace ForgeMission.Core.Runtime;

public enum MissionStatus { Pass, Fail }

public record MissionResult(
    string MissionName,
    string Text,
    MissionStatus Status = MissionStatus.Pass,
    string? FailReason = null,
    int Attempts = 1,
    // Set when the agent expert asked the CLIENT to run tools (Phase 42.3): the run ended at the
    // agent segment; post-agent steps did not run and will run on the final continuation instead.
    IReadOnlyList<Microsoft.Extensions.AI.FunctionCallContent>? ToolCalls = null);
