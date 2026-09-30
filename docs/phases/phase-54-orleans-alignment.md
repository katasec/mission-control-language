# Phase 54 — Orleans alignment (forge-conversations Host)

> **Status: Tasks 1–2 done 2026-09-30 (Host 0.6.0 live in dev, default path PASS) — see
> [completed record](phase-54-orleans-alignment_completed.md#tasks-12--delete-missionrungrain-journaledgrain-single-atomic-write-done-2026-09-30).**
> Tasks 3–7 are not designed.
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
| 4 | Atomic state change | Events in our Table log, checkpoint in Orleans grain storage, reconciled by a hand-rolled `PendingTransition` | Root cause is size: plain grain state is one ≤~1 MB object rewritten per save, so the growing history was written outside Orleans and a second write appeared. Orleans' answer is `JournaledGrain`; `CustomStorage` lets our adapter keep events as Table rows in one entity-group transaction (≤100 ops, 4 MiB). `LogStorage` keeps the whole log in one object (docs: not for long logs); `Microsoft.Orleans.Journaling` is alpha and Blob-backed | [log-consistency providers](https://learn.microsoft.com/en-us/dotnet/orleans/grains/event-sourcing/log-consistency-providers), [entity group transactions](https://learn.microsoft.com/en-us/rest/api/storageservices/performing-entity-group-transactions) |
| 5 | Push to clients: per-host observers with resubscribe; broadcast channels fan out to grains, not clients | In-process `ConversationEventHub` feeds SSE; correct only with one silo | Confirmed: works now; silently drops events with 2+ silos (inference, untested) | [observers](https://learn.microsoft.com/en-us/dotnet/orleans/grains/observers), [GPS/SignalR sample](https://learn.microsoft.com/en-us/samples/dotnet/samples/orleans-gps-device-tracker-sample/), [broadcast channel](https://learn.microsoft.com/en-us/dotnet/orleans/streaming/broadcast-channel) |
| 6 | `[ReadOnly]` for reads; keep frequent ephemeral calls off the non-reentrant queue | No `[ReadOnly]`; each 53.8 delta is a full grain turn | Confirmed | [request scheduling](https://learn.microsoft.com/en-us/dotnet/orleans/grains/request-scheduling), [one-way](https://learn.microsoft.com/en-us/dotnet/orleans/grains/oneway) |
| 7 | Reminders for durable retry; at-most-once messaging → app dedupe | Outbox reminder (30 s due, 1 min period); `command_id` dedupe; Service Bus consumers | Confirmed. Keep Service Bus (no Orleans provider for it) | [timers and reminders](https://learn.microsoft.com/en-us/dotnet/orleans/grains/timers-and-reminders), [streaming](https://learn.microsoft.com/en-us/dotnet/orleans/streaming/) |
| 8 | Call timeouts, deactivation, rollover | 30 s default timeout broke the deadlock; revision rollover briefly runs two silos (~30 s observed) | Set `[ResponseTimeout]` on long methods; never persist in `OnDeactivateAsync`; the rollover window can drop live events via the in-process hub | [grains](https://learn.microsoft.com/en-us/dotnet/orleans/grains/), [ACA deployment](https://learn.microsoft.com/en-us/dotnet/orleans/deployment/deploy-to-azure-container-apps) |

## Candidate tasks (priority order; each needs its own design)

| # | Task | Effect |
|---|---|---|
| 1 ✅ | Fold or delete `MissionRunGrain`; run state lives in `ConversationGrain` | Removes the only cross-grain edge and the run-grain call in activation |
| 2 ✅ | Checkpoint row in the event partition, committed with the events in one entity-group transaction; remove `PendingTransition` | Removes the most complex code in the Host |
| 3 | Deltas off the non-reentrant queue (publish without a grain turn, or `[AlwaysInterleave]` with no state change) plus delta sequence dedupe | Streaming no longer queues behind commands |
| 4 | `[ReadOnly]` on read methods (after confirming they don't mutate) | Reads stop waiting on each other |
| 5 | Activation limited to local repair; explicit decision on the fail-closed corruption check | Predictable activation |
| 6 | Before any second silo: SSE hosts subscribe as grain observers with resubscribe and log catch-up | Correct fan-out on multiple silos and during rollover |
| 7 | `[ResponseTimeout]` on long methods | Explicit timeouts |

| 8 | Blob offload: large bodies (answers, tool output) go to Blob, referenced by the event | Removes the ~30K-character per-event ceiling that fails a run today |

Not changing: the Table event log itself, Service Bus ingress/progress, reminders, `command_id` dedupe.

Pre-lock findings from a parallel investigation (a `6-checkpoint` row on plain grain code, a new
event table) were superseded by the locked design below; see the
[completed record](phase-54-orleans-alignment_completed.md#superseded-pre-lock-findings-2026-09-30).

## Tasks 1–2 design (locked 2026-09-30)

Investigation evidence (read-only, 2026-09-30): `MissionRunGrain` is written only at
`ConversationGrain.cs:1515` and read only by tests; project run history comes from the event log
(`ProjectRunIndex`), not the run grain. Dev storage: 69 checkpoints (55 unreachable pre-52.1 `dev`
tenant), 3,454 event-table rows, largest partition 452 KB / 190 events, largest checkpoint 4.8 KB,
0 pending transitions, 0 reminders. No production deployment exists. Orleans 10.0.0
`CustomStorage` behaviour was confirmed by decompiling the published 10.0.0 package.

### Decisions

| # | Decision | Why |
|---|---|---|
| D1 | **Delete `MissionRunGrain`** (grain, interface, checkpoint, `mission-run-checkpoint` provider). Its `ExecutionBoundary` field is dropped (informational, unread). | It mirrors facts `ConversationGrain` owns; one owner per fact; removes the only grain-to-grain edge. |
| D2 | **`ConversationGrain : JournaledGrain<ConversationState, ConversationTransition>`** with the `CustomStorage` log-consistency provider (`Microsoft.Orleans.EventSourcing` 10.0.0; no other package bump). Alternatives rejected: plain state + Blob (two stores or whole-history rewrite per event), built-in Blob/`LogStorage` (whole-history rewrite, all read paths change), Journaling alpha. | Orleans owns versioning, apply and conflict retry; per-event cost stays flat; every read path is unchanged. |
| D3 | **Row layout in the conversation partition** (`v1|{tenant}|{id:N}` in `forgeconversationevents`): `0-{seq}` public event rows and `1-{eventId}` receipt rows (unchanged format), plus one new `s-state` row holding the serialized `ConversationState` and its journal `Version`. One commit = one entity-group transaction: `s-state` (Add on first write, else Replace with If-Match ETag) + new event rows (Add) + their receipts (Add). Max ~5 rows. | One write, atomic. The state row carries the ~20 checkpoint fields that have no public event (pinned capabilities, hands attachment, continuation, pending dispatch). Replay-from-events alone cannot rebuild them. |
| D4 | **Adapter = today's `AzureTableConversationEventStore`**, re-plugged as `ICustomStorageInterface`. `ReadStateFromStorage` = point read of `s-state` (→ `Version`, state, ETag). `ApplyUpdatesToStorage(updates, expectedVersion)` = one transaction; returns `false` on 412/409 (conflict). `AppendAsync` is deleted; the adapter is the only writer of these rows. Existing read methods (`ReadAfterAsync`, `ReadRangeAsync`, `ReadLatestForRunAsync`, `FindByEventIdAsync`) stay. | Reuse; one owner of the storage write. The adapter owns storage correctness (version check, atomic batch, row format). |
| D5 | **State encoding:** `ConversationState` serialized as JSON into binary properties chunked at 64 KiB (Orleans' own Table provider pattern). | Avoids the UTF-16 string limit; state is small (≤5 KB today) but `ActiveStartCommandJson` alone may be 32 KiB. |
| D6 | **Event type:** one Host-internal `[GenerateSerializer] ConversationTransition` per grain call: public event JSONs (0–2), the accepted command JSON (for the receipt), a closed set of internal facts, and a timestamp. All ids, sequences and timestamps are decided before raising, so `TransitionState` is a pure apply (the existing `ApplySnapshotFields` folds into it). | The adapter recomputes the new state from the same pure apply; no hidden inputs. |
| D7 | **Conditional events only.** Every mutating method: validate → build transition → `RaiseConditionalEvent` → on `false`, re-read state and re-validate (receipt row decides "already accepted"). Unconditional `RaiseEvent` is not used. | Orleans 10.0.0 retries unconditional events on top of re-read state without re-validating (`PrimaryBasedLogViewAdaptor.UpdatePrimary`); conditional events are dropped when the version moved. Makes ambiguous commits and the ~30 s dual-silo rollover safe. |
| D8 | **Durable before ack:** a method returns only after its conditional event is confirmed. | The Service Bus ingress/progress consumers complete their message only after the grain replies. |
| D9 | **Storage outage:** accepted behaviour — Orleans retries with backoff (≤~10 s) and never throws; the caller times out (30 s), the message is abandoned and redelivered; the receipt row makes the redelivery a no-op. | Without storage the grain cannot progress anyway; no new knob. |
| D10 | **Outbox:** a transition that owes a mission command records it in state (`DispatchOwed`). Register the reminder before raising; after confirm, send (`MessageId = CommandId`), then raise `DispatchSent` (state-row-only commit) and unregister when nothing is owed. Reminder ticks and activation drain what remains. `DispatchState.BrokerAccepted` is deleted. | Service Bus cannot join the Table transaction; the owed command is durable before the send, and the send is idempotent within the 10-min duplicate-detection window (existing exposure beyond it is unchanged). |
| D11 | **Live publish:** after a confirmed conditional event, the grain publishes exactly that transition's public events to `ConversationEventHub`. Not `OnStateChanged` (it also fires on reads and carries no event list). | A failed or ambiguous commit publishes nothing; SSE clients catch up from the Table. |
| D12 | **Deleted:** `PendingTransition`, `PendingRunStart` and their repair/advance methods, the activation corruption check, the `conversation-checkpoint` grain storage provider, `DispatchState.BrokerAccepted`. **Kept:** outbox reminder, `RecoverMissionHandsInFlightAsync` (domain operation), clustering and reminder tables. | The single transaction makes the crash gap impossible; a foreign or duplicate write fails at commit (ETag 412 / Add 409) instead of blocking activation. |
| D13 | **Inline event cap drops from 48 KiB UTF-8 to 30 KiB**, so an `EventJson` fits a Table string property (64 KiB = 32K UTF-16 chars). Blob offload of large bodies stays unwired (not needed for Tasks 1–2; `PutAsync` has no caller today). | 48 KiB can exceed the Table limit (inference; the build verifies it against real Azure). |
| D14 | **Dev data: reset, no migration** (Ameer: "delete everything"). Done 2026-09-30: tables `OrleansConversationCheckpoints`, `OrleansMissionRunCheckpoints`, `OrleansConversationReminders` deleted; all rows of `forgeconversationevents` deleted (table kept, so no Bicep re-run). Blob container was empty. | No code path for old data remains. Local Desktop Projects holding a dev container id will read not-found. |

### Gates

| Gate | Answer |
|---|---|
| Security Architecture | No change to tiers, identities, queues or data ownership: the Host stays the sole reader/writer of its Table/Blob store. Tables it no longer uses are removed. |
| Engineering Philosophy | One owner per fact (D1); one write seam (D4); no new knob; containment is structural (transaction + ETag + conditional events), not repair code. |
| Failure boundary | Commit conflict → `false` → re-validate (receipt decides). Storage outage → caller timeout → broker redelivery → receipt no-op (D9). Send failure after commit → reminder resend, `MessageId` dedupe (D10). |
| Default path | `forge chat` TUI from `make install` on forge-mcl `main`, after `forge login`, default endpoint, against the Host deployed by `make 525-conversation-app` from merged forge-infra `main`, with runner and ForgeAPI as deployed. |

### Build tasks

Tasks 1+2, deploy, reset and default-path acceptance — done, see the
[completed record](phase-54-orleans-alignment_completed.md).

Not in scope: Blob offload of large bodies; retiring the empty, unreferenced
`forgeconversationindex` table.

## Phase Done when (Ameer, 2026-09-30)

All remaining candidates ship, in dependency order: **Tasks 4+7 → Task 8 → Task 6 → Task 3**
(Task 5 was absorbed by 1–2). Task 3 follows Task 6 so deltas use the new fan-out path rather than
the in-process hub. Each task: design locked (Type-1 decisions with Ameer) → supervisor loop → deploy →
default-path check.

## Tasks 4+7 design (locked 2026-09-30, build-ready)

Evidence: read-only investigation against the decompiled Orleans 10.0.0 runtime
(`ActivationData.MayInvokeRequest`: a ReadOnly request interleaves only with another ReadOnly
request). Reads see only `State` (the confirmed view); commits complete inside the request that raised
them, so a ReadOnly read never observes a half-applied commit.

| # | Decision | Why |
|---|---|---|
| E1 | `[ReadOnly]` on `GetMissionHandsWorkAsync`, `PublishDeltaAsync`, `GetSnapshotAsync`, `ReadAfterAsync`, `ReadProjectCommandAsync` | Pure reads of `State`/the store; reads stop queueing behind each other. |
| E2 | `ReadProjectRuns*` stay non-ReadOnly | They write the ETag-guarded project-run projection; concurrent writers would race to stale answers. |
| E3 | `[ResponseTimeout("00:02:00")]` on `RecordProgressAsync` only; every other method keeps the 30 s default | A storage stall (D9) must not exhaust 10 × 30 s progress redeliveries and fail the run; 2 min stays under the 5 min lock auto-renewal. Ingress commands stay at 30 s because ForgeAPI stops waiting at 30 s. |
| E4 | No new Service Bus retry/lock settings | Receipts make any redelivery a no-op; no new knob. |

Deltas off the queue remain Task 3 (E1 only lets a delta overlap with other reads).

Security: N/A (no tier, identity or data change). Engineering Philosophy: no knob added beyond one
attribute. Failure boundary: a timed-out progress call is abandoned and redelivered; the receipt makes
it a no-op. Done when: suite green; a focused test shows a ReadOnly read completes while another
ReadOnly read is in flight; Host deployed; default path: a `forge chat` turn completes and the
snapshot/events reads work, no Host errors.

## Next

Build Tasks 4+7 (Host 0.6.1). Task 8 design: decision on body storage pending with Ameer. Tasks 6+3: investigation running.
