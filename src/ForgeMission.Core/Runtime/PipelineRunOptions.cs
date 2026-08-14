using Microsoft.Extensions.AI;
using Scout;

namespace ForgeMission.Core.Runtime;

public record PipelineRunOptions(
    string MissionName,
    IReadOnlyDictionary<string, string>? Vars = null,
    TextWriter? StepWriter = null,
    TextWriter? ContentWriter = null,
    // Fired for each sub-search a kind:search backend narrates while a single search step runs (41.7
    // Task 2) — e.g. Grok's per-query web_search_call actions. Fills the long search step with live
    // detail; backends that can't narrate simply never call it. Unlike OnTrace below, this is the
    // existing synchronous callback imposed by Scout's IProgress backend, not a pipeline lifecycle
    // fact — left unchanged by Task 3.
    Action<WebSearchProgress>? OnSearchProgress = null,
    // Structured objects seeded into the context bag alongside Vars (Phase 42.1) — e.g. the full
    // client Conversation. The bag is untyped but not stringly-typed: real objects go in as-is.
    IReadOnlyDictionary<string, object>? ContextObjects = null,
    // Client-declared tools (Phase 42.3), already allowlist-filtered at the wire. They attach to
    // the `role: agent` expert's provider call ONLY; a tool call from it ends the run immediately
    // (the client executes and continues with a fresh request).
    IList<AITool>? Tools = null,
    // Tool continuation (42.3 three-segment rule): skip the pre-agent segment and start at the
    // `role: agent` step — its enrichment context was restored from the cache into Vars.
    bool StartAtAgent = false,
    // Fired once per run, just before the agent step, with the string-valued context snapshot —
    // the caller (MissionChatClient) stores it in the enrichment cache keyed by prefix hash P.
    Action<IReadOnlyDictionary<string, string>>? OnPreAgentComplete = null,
    // Mission names, root to current, for the currently executing mission (Phase 43.16 Task 3).
    // Null at a fresh top-level call — the runner defaults it to [MissionName]; CreateChildOptions
    // appends the child's declared mission name for a nested sub-mission invocation.
    IReadOnlyList<string>? MissionPath = null,
    // Awaited pipeline lifecycle sink (Phase 43.16 Task 3), replacing the former synchronous
    // OnStepStart/OnStepComplete callbacks. Always awaited with the run cancellation token — a
    // throwing sink fails the run rather than silently dropping a fact. No caller is required to
    // supply it, so a normal CLI/API run has unchanged trace overhead and behavior.
    Func<PipelineTraceEvent, CancellationToken, Task>? OnTrace = null,
    // Constrains the agent expert's provider request to at most one tool call per turn (Phase
    // 43.16 Task 8b). Null preserves today's unrestricted behavior for every caller that doesn't
    // set it — only Janus's Implement step (ConversationHost/Worker) opts into `false`, matching
    // JanusPipelineProgressMapper's "exactly one tool call per request" invariant. Threaded to
    // DirectExpertRunner through a closed, Core-internal context-bag instruction (see
    // PipelineRuntimeInstructions) — never a mission-authorable variable, and deliberately not
    // inherited by CreateChildOptions, matching Tools' own non-inheritance rule (it is meaningless
    // without Tools alongside it).
    bool? AllowMultipleToolCalls = null);
