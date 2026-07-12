namespace ForgeMission.Core.Runtime;

public record PipelineRunOptions(
    string MissionName,
    IReadOnlyDictionary<string, string>? Vars = null,
    TextWriter? StepWriter = null,
    TextWriter? ContentWriter = null,
    Action<string, StepEnvelope>? OnStepComplete = null,
    // Fired when a step BEGINS, with (expertName, kind). The engine-level progress signal (Phase
    // 41.7): it lands before a long-running step (e.g. kind:search) so a consumer can show "Searching
    // the web…" while it runs, not a frozen spinner. Provider-agnostic — every mission benefits.
    Action<string, string>? OnStepStart = null);
