# Phase 62 — One MCL interpreter: pause by replay

> **Status: ✅ complete 2026-10-03.** Core 0.1.7, `forge-runner:0.20.4` live. Release record and
> evidence: [phase-62-single-interpreter-replay_completed.md](phase-62-single-interpreter-replay_completed.md).

## Problem

Core runs the MCL AST with two interpreters (forge-mcl `src/ForgeMission.Core/Runtime/PipelineRunner.cs`):

```
            ┌─ RootTools set? ─ yes ─► B  RootScopedExecution (frames)   can pause, misses ~12 A features
RunAsync ───┤                                                            only `forge chat --hands` + resume
            └─ no ──────────────────► A  recursive (RunCoreAsync…)       every feature, cannot pause
                                                                         forge run, mcp, serve, door, plain chat
```

B was added next to A in Phase 46.2 ([task A](phase-46.2-task-a-core-continuation_completed.md)) as an
opt-in, leaving A untouched. Two ways to run one language is the "multiple code paths" smell, and B's gaps
already affect hands missions that use loop `feedback`, `when` guards on step-written keys, or child missions.

## Decision (locked)

**Delete B; make A pausable by replay.** Prior art: Temporal and Azure Durable Functions resume by
replaying a log of completed steps, not by saving a call stack.

```
snapshot (B, deleted)                          replay (A, kept)
checkpoint = where it stopped + current state  checkpoint = log of completed steps + tool turn
resume = load snapshot, continue               resume = run A from the top; logged steps return
                                                        their recorded result instead of running
```

| | Replay (chosen) | A → B (rejected) |
|---|---|---|
| Interpreter kept | A, all features | B, after filling ~12 gaps |
| Product lines | −400–420 / +250–330 (net −100 to −150) | −370 / +200–350 (about flat) |
| Tests | ~16 re-checked, 7 new | 40–45 rewritten |
| Repos with code | forge-mcl only | forge-mcl, maybe forge-runner |

Trade-off accepted: the log grows with completed steps (B's snapshot only holds current state). It covers
one run (one chat turn), so chat stays small; the Host body limit is 4 MiB
(`forge-conversations/src/ForgeMission.Conversations.Contracts/ConversationBodies.cs:22`).

## Checkpoint

```
checkpoint (opaque PipelineContinuation payload, FormatVersion 2)
├─ fingerprints, tool declarations, ordinal, root inputs   (kept from today's ResumeAsync checks)
├─ Log:  step key → { envelope text + status, context keys the step wrote (string | double) }
├─ PausedKey: step key of the agent waiting on the tool
└─ TurnMessages: the agent's tool calls + results so far   (kept from forge-mcl#50)
```

**Step key** = one segment per call level, root to step: `Mission@attempt#elementIndex[.branchIndex]`.
Names alone collide (`Child -> Child`, a loop retry).

## Rules replay must keep (from the sizing)

| # | Rule | Why |
|---|---|---|
| R1 | Step key is the full call path above | Unique per step execution |
| R2 | A pause returns up through child missions and `parallel` (new; today only root-mission steps return `ToolCalls`) | A's child (`:358`) and parallel (`:527`, `:572`) sites drop it |
| R3 | The one exception to concurrency: a `parallel` block that can reach an agent runs one branch at a time, in source order (B's `CanReachRootToolAgent` rule) | Two simultaneous pauses are not supported anywhere |
| R4 | No trace, `StepWriter` or delta output for replayed steps | The runner would republish progress facts |
| R5 | Log values typed (string or double); never drop step-written keys with the "token" rule of `IsSensitiveKey` (`:1016`) | `json_extract`/`onnx` write doubles; `max_tokens` would vanish |
| R6 | Replay that reaches a key missing from the log before `PausedKey`, or a different paused key, returns `InvalidContinuation` | An env value can flip a `when` guard |
| R7 | Binding (env) values are re-derived on replay, never stored | Today's rule (`AgentToolPipelineTests` env test) |
| R8 | With root tools, the agent step sends `AllowMultipleToolCalls = false`. Errors keep A's one behaviour (throw); the runner already turns exceptions into `Fail` (`MissionCommandProcessor`) | Phase 61; one error path |

## Tasks

All 7 done, see [_completed](phase-62-single-interpreter-replay_completed.md#tasks). Decisions made
during the build are recorded there under [Build decisions](phase-62-single-interpreter-replay_completed.md#build-decisions).

## Gates

- **Security:** N/A for tiers and identity. The checkpoint stays opaque and keeps env values and sensitive
  parameters out (R7).
- **Engineering philosophy:** removes a second code path; adds no option or mode.
- **Default path:** installed `forge` from forge-mcl `main`; `forge chat --hands` against
  `https://api.forge.katasec.com` on the deployed runner.

## Done when

1. `PipelineRunner.cs` has one interpreter (no `RootScopedExecution`); full forge-mcl and forge-runner
   suites pass with 0 warnings.
2. The new tests in task 6 pass.
3. On the default path, in a fresh conversation, `forge chat --hands` reads two files in one turn and
   answers both correctly, and plain `forge chat` answers as before.
