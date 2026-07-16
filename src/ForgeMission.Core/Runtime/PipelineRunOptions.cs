using Microsoft.Extensions.AI;
using Scout;

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
    Action<string, string>? OnStepStart = null,
    // Fired for each sub-search a kind:search backend narrates while a single search step runs (41.7
    // Task 2) — e.g. Grok's per-query web_search_call actions. Fills the long search step with live
    // detail; backends that can't narrate simply never call it.
    Action<WebSearchProgress>? OnSearchProgress = null,
    // Structured objects seeded into the context bag alongside Vars (Phase 42.1) — e.g. the full
    // client Conversation. The bag is untyped but not stringly-typed: real objects go in as-is.
    IReadOnlyDictionary<string, object>? ContextObjects = null,
    // Client-declared tools (Phase 42.3), already allowlist-filtered at the wire. They attach to
    // the `role: agent` expert's provider call ONLY; a tool call from it ends the run immediately
    // (the client executes and continues with a fresh request).
    IList<AITool>? Tools = null);
