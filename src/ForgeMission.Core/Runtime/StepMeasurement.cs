namespace ForgeMission.Core.Runtime;

/// <summary>One completed pipeline step, including usage attributed only to that step.</summary>
public sealed record StepMeasurement(
    string StepName,
    string? ParentStep,
    string Kind,
    string? Model,
    string? Role,
    long InputTokens,
    long CachedInputTokens,
    long OutputTokens,
    long ReasoningOutputTokens,
    long TotalTokens,
    long DurationMs,
    long? ToolDurationMs,
    string Status);
