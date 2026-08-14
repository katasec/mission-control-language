# Phase 43.16 Task 8b — Janus one-tool-per-turn contract

> **Status: implementation in progress (2026-08-14).** Prerequisite to
> [Task 8's live product proof](phase-43.16-janus-desktop-local-poc.md#8-product-proof-and-evidence):
> corrects a real defect discovered during Task 8's first live run, where the Implementer's
> provider call emitted two tool calls in one turn and tripped
> `JanusPipelineProgressMapper`'s deliberate "exactly one tool call per request" guard, failing
> the run before any tool hand-off completed. Kept explicitly separate from Task 8's evidence-only
> run, the same way Task 8a was kept separate — this is genuine application build-out, not proof
> narration.

## The finding that opened this task

Task 8's first live-proof attempt (conversation `e2882eae-0616-58eb-a023-3e09c1c91f4c`, real
OpenAI/Anthropic providers through the Kind cluster from Task 8a) validly proved observations
#1–#3 of the live proof and then hit a genuine, reproducible product limitation — not a proof
process error:

- `seq 1-4` — user goal "Implement a rate limiter." submitted; Proposer (attempt 1) asked
  clarifying questions instead of proposing a plan.
- `seq 5-7` — Approver (attempt 1) returned a genuine `revisionRequested` outcome, rendered in the
  browser as a red "Revision requested—" label with the Approver's exact feedback text, telling
  Proposer to pick reasonable defaults and produce a full plan naming specific files.
- `seq 8-9` — Proposer (attempt 2) produced a concrete 3-file plan: `rate_limiter.py`,
  `server.py`, `test_rate_limiter.py`, with class/function signatures and test cases.
- `seq 10-12` — Approver (attempt 2) returned `approved`, rendered as a green "Approved" label.
- `seq 13-14` — Implementer (attempt 1) started.
- `seq 15` — **`error`**: `"Janus v1 supports exactly one tool call per request; got 2."`
- `seq 16` — `runStatus: failed`.

Root cause, confirmed by reading the code rather than guessed:
`src/ForgeMission.ConversationWorker/Janus/JanusPipelineProgressMapper.cs:107-109`
(`MapToolRequested`) deliberately throws when `calls.Count != 1` — a documented v1 design
decision ("Unknown experts and zero/multiple tool calls fail visibly (throw) rather than guess",
same file, line 37). This is correct, intentional fail-loud behavior, not a bug — but nothing
upstream of it ever constrained the model to emit only one tool call per turn, so a real,
naturally multi-file approved plan (exactly the shape Approver's own feedback pushed Proposer
toward in this run) reliably trips it. `missions/janus/experts/Implementer/expert.md` said
nothing about call cadence, and GPT-4o (the `implementer` provider profile) is free to request
parallel tool calls by default.

Observations #4 (authorized local Implementer tool + durable result), #5 (disconnect/replay), and
#6 (Host interruption) never got a chance to start — the run reached a terminal `failed` state
before any `tool_requested`/`tool_result` event existed to build on. Task 8 remains **active and
unverified** until this task lands and Task 8's live proof reruns.

## Locked decisions

1. **The mapper guard stays byte-for-byte unchanged.** `JanusPipelineProgressMapper.
   MapToolRequested`'s `calls.Count != 1` throw is not weakened, removed, or routed around. Once
   this task lands it becomes true belt-and-suspenders — expected to essentially never fire for
   Implementer — but it remains the same loud failure it is today if a provider ever ignores the
   request-level constraint.
2. **Structural fix at the provider-request boundary, not prompt-only.** `Microsoft.Extensions.AI.
   ChatOptions.AllowMultipleToolCalls` (bool?) is already reachable through the existing
   `IChatClient.GetResponseAsync`/`GetStreamingResponseAsync` path and is honored by the OpenAI
   adapter (mapped to the SDK's own parallel-tool-calls setting). Setting it to `false` enforces
   "at most one tool call per turn" at the boundary that actually owns it — the request the
   provider receives — rather than hoping prompt wording alone is sufficient.
3. **Scoped, not global.** `DirectExpertRunner` is Core's shared `IExpertRunner`, used by every
   mission (CLI, Scout, any other `role: agent` mission), not just Janus. The "exactly one tool
   call" invariant belongs only to `JanusPipelineProgressMapper`, which lives under
   `ForgeMission.ConversationWorker/Janus/`. Hard-coding the restriction inside `DirectExpertRunner`
   would silently change every agentic mission in the product. The fix threads one new, default-null
   option from `PipelineRunOptions` down to the provider call; only Janus's `Implement` step opts
   in.
4. **The seam is a closed context-bag instruction, not a new public interface parameter.**
   `IExpertRunner.RunAsync`/`StreamAsync` take only `(ExpertDefinition, Dictionary<string, object>,
   CancellationToken)` — no `PipelineRunOptions` parameter exists to extend. The fix mirrors the
   existing `context["tools"]` pattern exactly: `PipelineRunner` writes a small, Core-internal,
   non-mission-authorable key into the same context bag right before the runner call and removes it
   in the same `finally` block that already removes `"tools"`; `DirectExpertRunner` reads it back.
   A named type (`PipelineRuntimeInstructions`) owns the key instead of an inline magic string.
5. **Prompt wording is defense-in-depth, not the guarantee.** `missions/janus/experts/Implementer/
   expert.md` gains one short paragraph telling the model it will be invoked again with each tool's
   result, so it should proceed incrementally. This helps the model's own reasoning quality but the
   actual guarantee is `AllowMultipleToolCalls = false` at the request level — documented as such so
   the prompt line is never mistaken for the real contract.

## Provider scope

Janus's Implementer step runs on the `implementer` profile, which `missions/janus/forge.toml`
binds to `provider = "openai"` — the same profile Proposer also uses (no tools attach at
Negotiate, so `AllowMultipleToolCalls` is inert there regardless). `Microsoft.Extensions.AI`'s
OpenAI adapter honors `ChatOptions.AllowMultipleToolCalls`, mapping it to the OpenAI SDK's own
parallel-tool-calls setting — so this is a real, supported constraint for the provider Implementer
actually runs on today, not a speculative one. Approver runs on the `architect` profile
(`provider = "anthropic"`) and never receives tools (only the `Implement` step is
`ToTools(capabilities)`-equipped, per `JanusMissionExecutor.cs:99`/`:134`) — it is unaffected by
this change either way. If Janus's Implementer profile is ever repointed at a provider that
doesn't honor `AllowMultipleToolCalls`, or a future SDK regression stops honoring it for OpenAI,
`JanusPipelineProgressMapper`'s unchanged guard remains the safe, visible fallback — the run still
fails loudly with the existing Error/Failed status rather than silently accepting or guessing
among the extra calls.

## Files

- `src/ForgeMission.Core/Runtime/PipelineRuntimeInstructions.cs` (new) — the one named,
  Core-internal context-bag key.
- `src/ForgeMission.Core/Runtime/PipelineRunOptions.cs` — new `bool? AllowMultipleToolCalls = null`
  field. Null preserves today's behavior for every caller that doesn't set it.
- `src/ForgeMission.Core/Runtime/PipelineRunner.cs` — writes the instruction into the context bag
  alongside `"tools"`; removes it in the same `finally` block.
- `src/ForgeMission.Core/Adapters/DirectExpertRunner.cs` — reads the instruction in both `RunAsync`
  and `StreamAsync` when tools are attached, applying it to `ChatOptions.AllowMultipleToolCalls`.
- `src/ForgeMission.ConversationWorker/Janus/JanusMissionExecutor.cs` — sets
  `AllowMultipleToolCalls: false` on the initial `Implement` run's options and on
  `RunContinuationAsync`'s options. `Negotiate`'s options are untouched (no tools attached there).
- `missions/janus/experts/Implementer/expert.md` — one added paragraph on incremental,
  one-action-at-a-time execution.
- Tests: `src/ForgeMission.Tests/Runtime/AgentToolPipelineTests.cs` (extended),
  `src/ForgeMission.ConversationWorker.Tests/JanusMissionExecutorToolCallOptionsTests.cs` (new,
  with its own local synthetic mission source/experts — not shared with
  `MissionCommandProcessorTests`, whose equivalents are private to that class),
  `src/ForgeMission.ConversationWorker.Tests/MissionCommandProcessorTests.cs` (extended).

## Tests

1. `AgentToolPipelineTests` (Core, real `PipelineRunner` → `DirectExpertRunner` →
   `ScriptedPipelineClient`, end-to-end through the real seam):
   - `AllowMultipleToolCalls: false` → captured `ChatOptions.AllowMultipleToolCalls == false`
     (non-streaming).
   - Default (null) → captured `ChatOptions.AllowMultipleToolCalls == null` (non-streaming) —
     proves every non-Janus caller's behavior is provably unchanged, not just assumed.
   - `AllowMultipleToolCalls: false` with a non-null `StepWriter` (forces
     `DirectExpertRunner.StreamAsync`) → same assertion, proving the streaming path without
     reaching into the internal instruction key from the test project.
2. `JanusMissionExecutorToolCallOptionsTests` (ConversationWorker, provider-free — a real
   `DirectExpertRunner` wrapping a local capturing fake `IChatClient`, own local synthetic mission
   source/experts): proves `JanusMissionExecutor.RunFullMissionAsync` and `RunContinuationAsync`
   themselves supply `AllowMultipleToolCalls: false` on the Implementer's call — not just that the
   option exists.
3. `MissionCommandProcessorTests` (extended, reusing `FakeExpertRunner`): a new scenario scripting
   the Implementer to return one tool call per invocation across three `ContinueAfterTool` rounds
   for the real 3-file rate-limiter plan from the failed live run, asserting all three
   `tool_requested`/`tool_result` pairs land in order and the run reaches a real terminal success —
   proving the sequential hand-off actually completes a multi-file plan end-to-end through the
   Worker.
4. `JanusPipelineProgressMapperTests.MultipleToolCalls_FailsVisibly` — unchanged, still required to
   pass exactly as today.

## Kind rollout (after code review/merge only)

Deploying the corrected Worker is part of this task's Done-when, but only after its code PR is
reviewed and merged — no separate Worker-only deployment path. From a clean `main` checkout, run
`make 350-conversation-kind-up` (the single established target from Task 8a) — it builds, loads,
SHA-tags, and rolls out **both** `conversation-host` and `mission-worker` images together by
design, even though `conversation-host`'s own code doesn't change here. Record the new `main`
commit SHA and the same rollout observations Task 8a captured (image tags, `kubectl rollout
status` for both Deployments, pod `Running` state) in a follow-up docs PR. No `525` layer
involvement.

## Done when

- The context-bag seam, `PipelineRunOptions.AllowMultipleToolCalls`, and Janus's opt-in are
  implemented exactly as scoped above; the mapper guard is unchanged.
- All four test items above pass; `dotnet build src/ForgeMission.slnx` and
  `dotnet test src/ForgeMission.slnx` are both clean.
- This code/docs PR is reviewed and merged to `main`.
- `make 350-conversation-kind-up` has been run from that clean `main` checkout and its rollout
  evidence (new SHA, both Deployments' rollout status, pod state) is recorded in a follow-up docs
  PR.
- Task 8's live proof is explicitly reauthorized and rerun — using the same "Implement a rate
  limiter." goal that originally failed, not an artificially single-file-scoped one — before Task 8
  itself is marked done.
