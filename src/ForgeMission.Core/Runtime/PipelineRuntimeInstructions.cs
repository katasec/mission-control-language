namespace ForgeMission.Core.Runtime;

/// <summary>Closed, Core-internal keys the runner uses to pass one-shot instructions to an
/// <see cref="IExpertRunner"/> through the step context bag — never mission-authorable, always
/// removed by the runner before the next step. Mirrors the existing "tools" context-bag
/// convention, named here instead of inlined so the two call sites (<see cref="PipelineRunner"/>,
/// <see cref="Adapters.DirectExpertRunner"/>) share one symbol rather than two independently
/// typed string literals.</summary>
internal static class PipelineRuntimeInstructions
{
    /// <summary>bool value mirroring <see cref="Microsoft.Extensions.AI.ChatOptions.AllowMultipleToolCalls"/>.
    /// Set only when <see cref="PipelineRunOptions.AllowMultipleToolCalls"/> is non-null and the
    /// step is a tool-capable agent expert (Phase 43.16 Task 8b).</summary>
    public const string AllowMultipleToolCalls = "__pipeline_allow_multiple_tool_calls";
}
