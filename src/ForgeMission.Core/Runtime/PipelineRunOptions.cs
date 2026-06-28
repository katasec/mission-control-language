namespace ForgeMission.Core.Runtime;

public record PipelineRunOptions(
    string MissionName,
    IReadOnlyDictionary<string, string>? Vars = null,
    TextWriter? StepWriter = null,
    TextWriter? ContentWriter = null,
    Action<string, StepEnvelope>? OnStepComplete = null);
