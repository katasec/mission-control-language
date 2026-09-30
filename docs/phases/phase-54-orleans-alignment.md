# Phase 54 — Orleans alignment (forge-conversations Host)

> **Status: design (2026-09-30). Not build-ready.** Selected by Ameer 2026-09-30; Tasks 1 and 2 are
> being designed together (they both change where a conversation's state lives).
> Origin: the [53.8 incident](phase-53.8-token-streaming.md#incident-2026-09-30--host-deadlock-after-a-mid-turn-restart-blocks-task-8)
> and a review of the Host against Orleans guidance, cross-checked online the same day.

**Goal:** make the conversation Host use Orleans the way Orleans is designed to be used, so that
future work layers on its capabilities instead of working around them. Principles 1, 2 and 7
(one owner, no duplicate paths, least code) apply directly.

## Review against Orleans guidance (2026-09-30)

Each claim below is cited from the official docs or the dotnet/orleans repo.

| # | Practice | Host today | Verdict | Source |
|---|---|---|---|---|
| 1 | Grain calls never form a cycle (non-reentrant grains deadlock) | Fixed in forge-conversations#12: `MissionRunGrain` no longer calls back | Confirmed; fix is canonical, no reentrancy needed | [request scheduling](https://learn.microsoft.com/en-us/dotnet/orleans/grains/request-scheduling), [best practices](https://learn.microsoft.com/en-us/dotnet/orleans/resources/best-practices) |
| 2 | Activation: an exception in `OnActivateAsync` fails activation | `ConversationGrain.OnActivateAsync` replays the pending transition, reads the Table tail, calls the run grain, and throws on log corruption | Corrected: docs don't forbid calls there, but failures make the grain unusable. Keep activation to local repair; the corruption throw (fail-closed) must be an explicit decision | [grains](https://learn.microsoft.com/en-us/dotnet/orleans/grains/), [persistence](https://learn.microsoft.com/en-us/dotnet/orleans/grains/grain-persistence/) |
| 3 | Don't split state that changes together ("chatty grains may be better combined") | `MissionRunGrain` only mirrors facts `ConversationGrain` recorded; nothing in production reads it | Confirmed: two owners for one fact | [best practices](https://learn.microsoft.com/en-us/dotnet/orleans/resources/best-practices) |
| 4 | Atomic state change | Events in our Table log, checkpoint in Orleans grain storage, reconciled by a hand-rolled `PendingTransition` | Corrected: a custom event log is fine; the defect is two stores with no shared commit. Table entity-group transactions are atomic within one partition (≤100 ops, 4 MiB). `JournaledGrain` (CustomStorage) is optional; `Microsoft.Orleans.Journaling` is alpha | [log-consistency providers](https://learn.microsoft.com/en-us/dotnet/orleans/grains/event-sourcing/log-consistency-providers), [entity group transactions](https://learn.microsoft.com/en-us/rest/api/storageservices/performing-entity-group-transactions) |
| 5 | Push to clients: per-host observers with resubscribe; broadcast channels fan out to grains, not clients | In-process `ConversationEventHub` feeds SSE; correct only with one silo | Confirmed: works now; silently drops events with 2+ silos (inference, untested) | [observers](https://learn.microsoft.com/en-us/dotnet/orleans/grains/observers), [GPS/SignalR sample](https://learn.microsoft.com/en-us/samples/dotnet/samples/orleans-gps-device-tracker-sample/), [broadcast channel](https://learn.microsoft.com/en-us/dotnet/orleans/streaming/broadcast-channel) |
| 6 | `[ReadOnly]` for reads; keep frequent ephemeral calls off the non-reentrant queue | No `[ReadOnly]`; each 53.8 delta is a full grain turn | Confirmed | [request scheduling](https://learn.microsoft.com/en-us/dotnet/orleans/grains/request-scheduling), [one-way](https://learn.microsoft.com/en-us/dotnet/orleans/grains/oneway) |
| 7 | Reminders for durable retry; at-most-once messaging → app dedupe | Outbox reminder (30 s due, 1 min period); `command_id` dedupe; Service Bus consumers | Confirmed. Keep Service Bus (no Orleans provider for it) | [timers and reminders](https://learn.microsoft.com/en-us/dotnet/orleans/grains/timers-and-reminders), [streaming](https://learn.microsoft.com/en-us/dotnet/orleans/streaming/) |
| 8 | Call timeouts, deactivation, rollover | 30 s default timeout broke the deadlock; revision rollover briefly runs two silos (~30 s observed) | Set `[ResponseTimeout]` on long methods; never persist in `OnDeactivateAsync`; the rollover window can drop live events via the in-process hub | [grains](https://learn.microsoft.com/en-us/dotnet/orleans/grains/), [ACA deployment](https://learn.microsoft.com/en-us/dotnet/orleans/deployment/deploy-to-azure-container-apps) |

## Candidate tasks (priority order; each needs its own design)

| # | Task | Effect |
|---|---|---|
| 1 | Fold or delete `MissionRunGrain`; run state lives in `ConversationGrain` | Removes the only cross-grain edge and the run-grain call in activation |
| 2 | Checkpoint row in the event partition, committed with the events in one entity-group transaction; remove `PendingTransition` | Removes the most complex code in the Host |
| 3 | Deltas off the non-reentrant queue (publish without a grain turn, or `[AlwaysInterleave]` with no state change) plus delta sequence dedupe | Streaming no longer queues behind commands |
| 4 | `[ReadOnly]` on read methods (after confirming they don't mutate) | Reads stop waiting on each other |
| 5 | Activation limited to local repair; explicit decision on the fail-closed corruption check | Predictable activation |
| 6 | Before any second silo: SSE hosts subscribe as grain observers with resubscribe and log catch-up | Correct fan-out on multiple silos and during rollover |
| 7 | `[ResponseTimeout]` on long methods | Explicit timeouts |

Not changing: the Table event log itself, Service Bus ingress/progress, reminders, `command_id` dedupe.

## Design findings for Tasks 1–2 (read-only investigation, 2026-09-30)

Measured in dev (read-only queries against `stforgeconvdev`); atomicity was spiked on Azurite only.

| Fact | Evidence |
|---|---|
| Nothing in production reads `MissionRunGrain`; `ConversationGrain` already holds everything needed; project run history doesn't use it | `MissionRunGrain.cs:64` (only tests call `GetStatusAsync`); `ProjectRunIndex.cs:11` |
| Event and idempotency rows are already in one partition per conversation and one transaction; only the checkpoint lives elsewhere (`OrleansConversationCheckpoints`, one binary `Data` column, 315–4,806 B, median 1,448 B) | `AzureTableConversationEventStore.cs:59-81`; `Program.cs:77-81` |
| Dev data: 69 conversation checkpoints (55 in the unreachable legacy `dev` tenant, 14 owned by Ameer, 0 pending work); 111 orphaned run-grain rows; 3,454 event-table rows | Table queries |
| Two more non-atomic spots Task 2 removes: the hands result/cancel/recover methods write the event then the checkpoint separately; `BeginRunAsync` validates command size after changing state | `ConversationGrain.cs:671-679, 809-810, 849-866, 141-148, 404-417` |
| Spike (Azurite): checkpoint + event + idempotency rows in one batch commit atomically; a stale ETag → 412 and no event rows; duplicate sequence → 409 and checkpoint unchanged; ~4 MB → 413 | scratchpad `p54-spike` (not in any repo) |

**Proposed design.**
- Delete `MissionRunGrain` (and its types, storage and the `notifyMissionRun` path).
- Checkpoint row `6-checkpoint` in the conversation's event partition. Its state is System.Text.Json in chunked binary columns (≤64 KiB each, capped at 960 KiB), written by the event store's new `CommitAsync(address, state, expectedETag, appends)` in one entity-group transaction (≤5 ops, ~1.3 MiB worst case).
- `ConversationGrain` drops `IPersistentState` and uses the row ETag for concurrency.
- `PendingTransition`, `PendingRunStart` and both repair methods are removed. An `OwedDispatchJson` outbox field is committed with the event and cleared after Service Bus accepts it (reminder retry kept).
- Activation is one point read.

**Decisions for Ameer (recommendations from the investigation):**

| # | Decision | Recommendation |
|---|---|---|
| D1 | Fold or delete `MissionRunGrain` | Delete |
| D2 | Dev data migration (Type-1) | (c′) a new event table (e.g. `forgeconversationstate`) via the 350 layer, old tables left untouched (rollback-safe, no mixed-version rows during rollover). Alternative: a one-off copy job to keep the 14 conversations. Check first what the client does with stale IDs in its on-disk `ProjectManifest`. |
| D3 | Fail-closed corruption check at activation | Remove: the atomic batch plus ETag makes it impossible; a stray row fails at the next append |
| D4 | Owed-dispatch send failure | Throw to the caller (today's behaviour) |
| D5 | State mutation style | In place, `DeactivateOnIdle` on commit failure, and validate before mutating |
| D6 | Tasks 3–5 | Task 5 is absorbed by 1+2; Tasks 3 and 4 stay separate, after Task 2 |

**Task breakdown:** 1 delete run grain → 2a store `ReadCheckpointAsync`/`CommitAsync` → 2b grain on one commit
per state change → 2c data per D2 → 2d deploy (525) and default path: two `forge chat` turns with memory,
a Host restart mid-turn still completes, run history lists runs.

**Risks noted:** the rollover overlap (removed by D2 (c′)); Azurite is not real Azure (2d is the live
proof); a latent size limit: Azure Table strings are UTF-16, so `MaxInlineEventJsonBytes` (48 KiB) exceeds
what real Azure accepts and `AcceptedCommandJson` (32 KiB) sits at the limit (separate item unless folded
in); an unbounded `OpaqueContinuation` (the state cap fails loudly); resends after the 10-minute Service Bus
dedupe window rely on the runner.

## Next

1. The Tasks 1–2 design investigation is done (above). Verify its key claims in code before relying
   on them.
2. Lock decisions D1–D6 with Ameer, one at a time, starting with D2 (Type-1 data).
3. Then mark Tasks 1–2 build-ready and run them through the supervisor loop.
