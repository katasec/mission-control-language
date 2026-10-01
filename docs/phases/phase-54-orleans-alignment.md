# Phase 54 — Orleans alignment (forge-conversations Host)

> **Status: complete 2026-10-01.** Every candidate task is done and live in dev (Host 0.8.0, runner 0.18.0[^runner],
> ForgeAPI 0.7.0). Evidence: [completed record](phase-54-orleans-alignment_completed.md).
> Origin: the [53.8 incident](phase-53.8-token-streaming_completed.md#incident-2026-09-30--host-deadlock-after-a-mid-turn-restart-blocks-task-8)
> and a review of the Host against Orleans guidance, cross-checked online the same day.

**Goal:** make the conversation Host use Orleans the way Orleans is designed to be used, so that
future work layers on its capabilities instead of working around them. Principles 1, 2 and 7
(one owner, no duplicate paths, least code) apply directly.

## Candidate tasks (priority order; each needs its own design)

| # | Task | Effect |
|---|---|---|
| 1 ✅ | Fold or delete `MissionRunGrain`; run state lives in `ConversationGrain` | Removes the only cross-grain edge and the run-grain call in activation |
| 2 ✅ | Checkpoint row in the event partition, committed with the events in one entity-group transaction; remove `PendingTransition` | Removes the most complex code in the Host |
| 3 ✅ | Deltas off the non-reentrant queue (publish without a grain turn, or `[AlwaysInterleave]` with no state change) plus delta sequence dedupe | Streaming no longer queues behind commands |
| 4 ✅ | `[ReadOnly]` on read methods (after confirming they don't mutate) | Reads stop waiting on each other |
| 5 ✅ | Activation limited to local repair; explicit decision on the fail-closed corruption check | Predictable activation |
| 6 ✅ | Before any second silo: SSE hosts subscribe as grain observers with resubscribe and log catch-up | Correct fan-out on multiple silos and during rollover |
| 7 ✅ | `[ResponseTimeout]` on long methods | Explicit timeouts |

| 8 ✅ | Large bodies via claim-check: body in Host-owned Blob, event carries a preview + reference | Removes the per-event ceiling (30 KiB today, 256 KB transport) up to a 4 MiB guardrail |

Not changing: the Table event log itself, Service Bus ingress/progress, reminders, `command_id` dedupe.

Pre-lock findings from a parallel investigation (a `6-checkpoint` row on plain grain code, a new
event table) were superseded by the locked design; see the
[completed record](phase-54-orleans-alignment_completed.md#superseded-pre-lock-findings-2026-09-30).

## Review and designs

| Part | Where |
|---|---|
| Review against Orleans guidance (2026-09-30), incl. the "Host today" table | [completed record](phase-54-orleans-alignment_completed.md#review-against-orleans-guidance-2026-09-30) |
| Tasks 1–2 design (D1–D14) | [completed record](phase-54-orleans-alignment_completed.md#tasks-12-design-locked-2026-09-30) |
| Tasks 4+7 design (E1–E5) | [completed record](phase-54-orleans-alignment_completed.md#tasks-47-design-locked-2026-09-30) |
| Task 8 design (B1–B14) | [completed record](phase-54-orleans-alignment_completed.md#task-8-design-locked-2026-09-30) |
| Tasks 6+3 design (F1–F5) | [completed record](phase-54-orleans-alignment_completed.md#tasks-63-design-locked-2026-10-01) |
| How the result works today | [How conversations work](../design/how-conversations-work.md) |

## Phase Done when (Ameer, 2026-09-30)

All remaining candidates ship, in dependency order: **Tasks 4+7 → Task 8 → Task 6 → Task 3**
(Task 5 was absorbed by 1–2). Task 3 follows Task 6 so deltas use the new fan-out path rather than
the in-process hub. Each task: design locked (Type-1 decisions with Ameer) → supervisor loop → deploy →
default-path check.

## Next

Phase complete. Both follow-ups it raised are done: the conversation explainer
([How conversations work](../design/how-conversations-work.md)) and `forge chat --hands`
([Phase 55](phase-55-forge-chat-hands.md)). The Desktop client upgrade stays in the [backlog](../backlog.md).

[^runner]: The runner is now 0.19.0, deployed by [Phase 55](phase-55-forge-chat-hands.md).
