# Phase 54 — Orleans alignment: completed work

## Tasks 1+2 — delete `MissionRunGrain`; `JournaledGrain` single atomic write (done 2026-09-30)

Design: [spoke D1–D14](#tasks-12-design-locked-2026-09-30).
Code: [katasec/forge-conversations#13](https://github.com/katasec/forge-conversations/pull/13)
(merge `c3c6eea`). Deploy: [katasec/forge-infra#28](https://github.com/katasec/forge-infra/pull/28),
Host image `forge-conversation-host:0.6.0` (digest `sha256:b61639…61d1`), revision
`ca-forge-conversation-host-dev--0000005`.

### Evidence

| Check | Observation |
|---|---|
| Orleans 10.0.0 behaviour (gate) | `Microsoft.Orleans.EventSourcing` 10.0.0 from nuget.org decompiled (ilspycmd): `ICustomStorageInterface` has only the two methods; `false` or an exception → re-read and retry; `RemoveStaleConditionalUpdates` drops a conditional entry only when the version moved. (The earlier source read was from `main`, not the tag; the package confirmed it.) |
| Build / tests (supervisor rerun) | `dotnet build -warnaserror`: 0 warnings. `dotnet test`: 212/212 on Azurite, incl. stale version across two activations, ambiguous commit, reminder resend, oversize rejection before storage. CI "Verify Conversations packages" passed. |
| D13 premise, real Azure | Scratch rows in `forgeconversationevents`: 30,720 and 32,768-char strings accepted; 49,152 rejected (so the old 48 KiB cap admitted rows Azure rejects). Probe rows deleted. |
| D14 reset | Deleted all 3,454 rows (65 partitions) of `forgeconversationevents` (table kept; Bicep owns it) and tables `OrleansConversationCheckpoints`, `OrleansMissionRunCheckpoints`, `OrleansConversationReminders`. Artifact container had 0 blobs. Kept `OrleansSiloInstances`, `forgeconversationindex`. |
| Deploy | `make 525-conversation-app-what-if`: 1 to modify, image 0.5.0 → 0.6.0 only. `make 525-conversation-app` Succeeded 13:13:21Z; revision `--0000005` Healthy, silo started, no errors. |

### Default-path acceptance

| Fact | Observation |
|---|---|
| Artifact | `forge` 1.0.0+c041dee from `make install` on forge-mcl `main`. |
| Defaults | No `FORGE_API_ENDPOINT` / `ConversationRuntime__*` overrides; existing `forge login`; default project `~/Forge/Projects/chat`. |
| Dependency | ForgeAPI → Host 0.6.0 → runner, as deployed by forge-infra `main`. |
| Starting state | Empty conversation store after the D14 reset. |
| Action | Piped `forge chat`: turn 1 "remember BLUE-HERON-54"; later turns ask for the codeword plus a long story. Turn 4: revision restart issued 13:23:47Z, turn sent 13:24:07Z. |
| Outcome | **PASS.** Every later turn recalled the codeword (memory from events). Reopening replayed prior turns. Turn 4: `userMessage` committed 13:24:10 by the old replica; the new silo started 13:24:18; the completion committed 13:24:30 through the new activation; full answer delivered. Host logs: no errors. One `s-state` row, gapless sequences 1–24. |
| Controlled tests | Azurite + fake dispatcher (above) are non-acceptance evidence. The TUI rendering itself was not exercised (piped mode uses the same binary and Host path). |

Note: an ACA revision restart is rolling — the old replica serves until the new one is ready
(~30 s). Turns 2 and 3 finished before the swap; only turn 4 spans it.

## Superseded pre-lock findings (2026-09-30)

Recorded by a parallel session (#254) before the design was locked; superseded by
[D1–D14](#tasks-12-design-locked-2026-09-30). Kept for its measurements
and the Azurite atomicity spike. Its D1 (delete) and D3/D4 match the locked design; its D2 (new table)
lost to a dev reset, and its D5 (plain grain, `DeactivateOnIdle`) lost to `JournaledGrain`.


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


## Tasks 4+7 + E5 — ReadOnly reads, progress timeout, SSE gap rule (done 2026-09-30)

Design: [spoke E1–E5](#tasks-47-design-locked-2026-09-30).
Code: [katasec/forge-conversations#14](https://github.com/katasec/forge-conversations/pull/14). Deploy:
[katasec/forge-infra#29](https://github.com/katasec/forge-infra/pull/29), Host `0.6.1`
(`sha256:3fbe6e8c…f320`), revision `--0000006`.

| Check | Observation |
|---|---|
| Tests (supervisor rerun) | `-warnaserror` 0 warnings; 217/217. The four new tests fail on the old code (gap test got ids `1,2,4`; overlap test timed out at 35 s; reflection found no ReadOnly). CI "Verify Conversations packages" passed. |
| Deploy | what-if: image 0.6.0 → 0.6.1 only; `make 525-conversation-app` Succeeded 14:11:31Z; revision Healthy, silo started 14:11:53Z. |
| Default path | Piped `forge chat` (forge 1.0.0+c041dee, no overrides): a new turn answered `BLUE-HERON-54` from earlier turns, prior turns replayed; Host logs: no errors, no SSE gap warnings. **PASS.** Timeout proven by reflection only (slow runtime test skipped by decision). |

## Task 8 — every body in Blob via claim-check (done 2026-10-01)

Design: [spoke B1–B14](#task-8-design-locked-2026-09-30).

| Part | PR | Evidence (supervisor re-run) |
|---|---|---|
| 8a Contracts 0.7.0 + Host | [katasec/forge-conversations#15](https://github.com/katasec/forge-conversations/pull/15) (+ B6 amendment `8945ee2`) | 249/249, 0 warnings; tag `forge-conversations-v0.7.0` published Contracts 0.7.0 (`gh api` lists it; run red at the known visibility step). |
| 8b runner | [katasec/forge-runner#16](https://github.com/katasec/forge-runner/pull/16) | 98/98 on published 0.7.0. |
| 8c ForgeAPI edge | [katasec/forge-platform#16](https://github.com/katasec/forge-platform/pull/16) | 109 + 30 + 21 on published 0.7.0 (repo has no CI). |
| 8e forge-client (Bob) | [katasec/forge-client#5](https://github.com/katasec/forge-client/pull/5) | 171/171; tag `client-v0.5.0` published Katasec.Forge.Client 0.5.0. |
| forge-mcl bump | [katasec/forge-mcl#25](https://github.com/katasec/forge-mcl/pull/25) | Build 0 warnings; 460 passed (`MissingApiKey_ThrowsClearly` fails only when the shell exports `MCL_API_KEY`). |
| 8d forge-infra | [katasec/forge-infra#30](https://github.com/katasec/forge-infra/pull/30) | what-if: 525 image; 500 runner image + `ConversationHostBaseUrl` (ForgeUI: unresolved-reference noise only); 550 image. |

**Images:** Host `forge-conversation-host:0.7.0` (`sha256:1377300c…3e80`, local build + crane); runner
`forge-runner:0.18.0` (`sha256:0a80f4c9…0610`, local build + crane — its CI image workflow fails Azure OIDC
login: the extracted repo has no client/tenant id); ForgeAPI `forge-api:0.7.0` (CI).

**Release window:** all seven Service Bus queues (and DLQs) at 0; reset deleted 122 event rows and the
reminders table (blobs: 0); `make 525-conversation-app`, `500-app`, `550-api` Succeeded 21:52–21:54Z; active
revisions Healthy on Host 0.7.0, runner 0.18.0, ForgeAPI 0.7.0.

### Default-path acceptance

| Fact | Observation |
|---|---|
| Artifact | `forge` 1.0.0+290e241 from `make install` on forge-mcl `main`. |
| Defaults | No endpoint overrides; existing `forge login`; default project `~/Forge/Projects/chat`. |
| Dependency | ForgeAPI 0.7.0 → Host 0.7.0 → runner 0.18.0 as deployed from forge-infra `main`. |
| Starting state | Store reset in the release window. |
| Action | Piped `forge chat` with one 404,863-byte line ending "The codeword is VIOLET-KESTREL-8. Reply with only the codeword."; then a small turn. |
| Outcome | **PASS.** The answer was `VIOLET-KESTREL-8` (the runner read the whole body through the Host route); the body is one 404,863-byte blob under `bodies/`; reopening replayed the large message hydrated (all 8,800 filler sentences and the codeword); the small turn completed. Runner and ForgeAPI logs clean. |
| Bob's claim-then-read (B13) | Not live at the time (no default client attached hands); **proven live 2026-10-01 by [Phase 55](phase-55-forge-chat-hands_completed.md)** (`forge chat --hands`). |

**Gotcha:** the Host logged `TableBeingDeleted` for the reminders table at startup because the reset had just
deleted it; Orleans retried and started the reminder service a second later. In a reset, delete Orleans
tables well before the deploy, or leave the reminders table in place.

## Tasks 6+3 — grain observers for N silos; interleaved deltas (done 2026-10-01)

Design: [spoke F1–F5](#tasks-63-design-locked-2026-10-01).
Code: [katasec/forge-conversations#16](https://github.com/katasec/forge-conversations/pull/16). Deploy:
[katasec/forge-infra#33](https://github.com/katasec/forge-infra/pull/33), Host `0.8.0` (`sha256:b693dbd4…ce32`).

| Check | Observation |
|---|---|
| Cluster prerequisite | Two-replica test ([forge-infra#31](https://github.com/katasec/forge-infra/pull/31), reverted by [#32](https://github.com/katasec/forge-infra/pull/32)): both silos `Active`, no suspicions for 6 min; TCP 11111 connections to the sibling and to the previous revision's silo. |
| Tests (supervisor rerun) | 0 warnings; 259/259. Two-silo tests (one cluster, two in-process Hosts): cross-silo order, reactivation + resubscribe catch-up (~32 s), dead and hanging observers do not block commits, delta during a held commit, delta dedupe. They fail with the grain's notify disabled. |
| Default path, one replica | `forge chat` turns complete on 0.8.0 before and after the acceptance window; Host logs clean. |
| Multi-silo window | [forge-infra#34](https://github.com/katasec/forge-infra/pull/34) (two replicas), reverted by [#35](https://github.com/katasec/forge-infra/pull/35) (min = max = 1 confirmed). Three silos `Active`, no suspicions; five `forge chat` turns all completed with full replies. |
| Deltas (controlled check) | `StreamConversationEvents` with `includeDeltas` through ForgeAPI (`api.forge.katasec.com`) while `forge chat` ran: one replica — 12 deltas before the final message; two replicas — 5 of 6 runs 7–11 deltas before the message. Run 1 had all six durable events (85–90) but no deltas; inference, not proven: the grain moved off the draining previous silo and its observer list was empty until the 30 s resubscribe, when the Table catch-up delivered the durable events (deltas are live-only by design). |

**Process note:** the image-tag bumps and the two-replica window edits in forge-infra were made by the
supervisor directly (one-line, what-if reviewed, PR-merged), not by an implementing subagent.

## Design record (moved from the spoke 2026-10-01)

The review and the locked designs below were the spoke's body while the phase ran. They are kept
here unchanged except for heading suffixes and the two "Blob offload" notes, which now point to Task 8.

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
| D10 | **Outbox:** a transition that owes a mission command records it in state (`DispatchOwed`). Register the reminder before raising; after confirm, send (`MessageId = CommandId`), then raise `DispatchSent` (state-row-only commit) and unregister when nothing is owed. What remains is drained by the next reminder tick and after the next call to the grain (`CommitTransitionAsync`); activation does not drain (the grain has no `OnActivateAsync`; corrected 2026-10-01). `DispatchState.BrokerAccepted` is deleted. | Service Bus cannot join the Table transaction; the owed command is durable before the send, and the send is idempotent within the 10-min duplicate-detection window (existing exposure beyond it is unchanged). |
| D11 | **Live publish:** after a confirmed conditional event, the grain publishes exactly that transition's public events to `ConversationEventHub`. Not `OnStateChanged` (it also fires on reads and carries no event list). | A failed or ambiguous commit publishes nothing; SSE clients catch up from the Table. |
| D12 | **Deleted:** `PendingTransition`, `PendingRunStart` and their repair/advance methods, the activation corruption check, the `conversation-checkpoint` grain storage provider, `DispatchState.BrokerAccepted`. **Kept:** outbox reminder, `RecoverMissionHandsInFlightAsync` (domain operation), clustering and reminder tables. | The single transaction makes the crash gap impossible; a foreign or duplicate write fails at commit (ETag 412 / Add 409) instead of blocking activation. |
| D13 | **Inline event cap drops from 48 KiB UTF-8 to 30 KiB**, so an `EventJson` fits a Table string property (64 KiB = 32K UTF-16 chars). Blob offload of large bodies stays unwired (not needed for Tasks 1–2; later done as Task 8). | 48 KiB can exceed the Table limit (inference; the build verifies it against real Azure). |
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

Not in scope then: Blob offload of large bodies (later done as Task 8, below); retiring the empty, unreferenced
`forgeconversationindex` table.

## Tasks 4+7 design (locked 2026-09-30)

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
| E5 | **SSE gap rule (bug fix, found 2026-09-30):** `ConversationSseWriter` emits a live event only when its sequence is `cursor + 1`. A live event beyond that first replays the missing range from the Table (`ReadAfterAsync(cursor)`) in order; an event at or below the cursor is skipped. | Today the writer emits any `Sequence > cursor`, so a skipped publish (D11 ambiguous commit) moves the reader's `id:` past an event it never received, and a reconnect never replays it. Sequences are contiguous, so `cursor + 1` is exact. |

Deltas off the queue remain Task 3 (E1 only lets a delta overlap with other reads).

Security: N/A (no tier, identity or data change). Engineering Philosophy: no knob added beyond one
attribute. Failure boundary: a timed-out progress call is abandoned and redelivered; the receipt makes
it a no-op. Done when: suite green; a focused test shows a ReadOnly read completes while another
ReadOnly read is in flight; a focused test shows a live event two ahead of the cursor delivers the
missing event from the Table first; Host deployed; default path: a `forge chat` turn completes and the
snapshot/events reads work, no Host errors.

## Task 8 design (locked 2026-09-30)

Evidence (read-only investigation 2026-09-30): Service Bus Standard caps every message at 256 KB, so
no body over 256 KB reaches the Host today; the runner has no cap (an over-limit answer loops 10
redeliveries, then dead-letters). `ConversationArtifactReference` exists but `Artifact` belongs only to
`Kind=Artifact`. Consumers: forge-platform, forge-runner, forge-client on Contracts 0.6.0; forge-desktop
on 0.4.0. Host identity has Blob Data Contributor account-wide; runner and edge have no storage role.

| # | Decision | Why |
|---|---|---|
| B1 | **Claim-check (locked with Ameer 2026-09-30).** A large body is stored in the Host-owned `forgeconversationartifacts` Blob container, written only by the Host. The event keeps a preview and a body reference. Producers (runner; edge for client-submitted results) send a large body as ordered chunk messages in the conversation's Service Bus session; the Host appends them to a blob keyed by the event id (idempotent retry) and commits the event after the last chunk. Clients fetch a full body on demand through a query route. | Removes the transport and Table ceilings with one store owner; runner keeps no storage role. Alternatives rejected: split Table columns (stops at 256 KB), Service Bus Premium (~$700/mo, huge payloads on a bus), runner-written Blob (breaks the no-storage-role rule). |
| B2 | **4 MiB per body (locked with Ameer 2026-09-30),** enforced at intake. | A guardrail, not a storage limit: a body larger than ~1M tokens (the model context) is unusable, and client-submitted bodies are untrusted input. Model replies are ≤ ~512 KB (128K output tokens). Comparable to Claude (1M context ≈ 4 MB) and ChatGPT's per-message use. Attachments are a separate later feature with their own route and larger cap. |
| B3 | **One path: every body goes to Blob (locked with Ameer 2026-09-30).** Every body-bearing field (message text, tool/hands results and arguments, goal, error text) is stored in Blob; the event row keeps metadata plus the body reference; body-less events stay plain rows. Every body travels as 1..N chunks (a small body is one chunk). The Host hydrates bodies when serving events, snapshots, SSE and memory composition, so the client-facing `ConversationEvent` is unchanged. No compatibility paths: all consumers update together (Forge is pre-launch, no customers). | One data shape and one code path; one place to look for text. A Blob read per body on replay and a Blob write per body on commit are accepted; performance is revisited only on a measured bottleneck (YAGNI). |
| B4 | Orphan blobs (a body written but its commit never retried) are accepted; retries reuse the deterministic path. The Host's existing account-wide Blob role is unchanged. | No cleanup machinery for a negligible cost; no infra change. |
| B5 | **What is a body:** any string whose size a user, model or tool decides. Bodies: `UserMessage` text (every goal/turn/input field), `ParticipantMessage.Text`, `Error.Reason` from the runner, `ToolResult.Content`, hands result `Content`/`Reason`, tool-request `Arguments`, the runner `OpaqueContinuation`, the composed `MissionInput`, `ProjectGoal`, client cancel `Reason`, evaluation summary/reason. Not bodies: ids, enums, Forge-authored fixed text, the Core-bounded launch package, live deltas. | One rule, applied everywhere (single path). |
| B6 | **Chunk message (both queues):** the Service Bus message body is raw UTF-8 bytes, ≤192 KiB; application properties `message_kind=bodyChunk`, `body_id`, `chunk_index`, `chunk_count`, `body_bytes`, `body_sha256`. `MessageId` is producer-chosen and the Host never validates it (amended 2026-09-30, 8c planning: on `conversation-ingress` duplicate detection would drop a retried request's chunks and the edge would wait for a reply that never comes): the runner uses UUIDv5(`body_id`, index) so a resend is deduplicated; the edge uses a fresh id per chunk. Put Block (block id = index) makes any resend harmless. Contracts extractors reject a null body field (400 at the edge). `body_id` = deterministic `Body(ownerFactOrCommandId, field)` — one blob per field. A fact/command carries `ConversationBodyReference(BodyId, Utf8Bytes, Sha256)` per body field (one typed field each). | Fits the 256 KB cap without base64; duplicate detection drops resends; deterministic ids make retries idempotent. |
| B7 | **Host assembly:** each chunk is a Blob **Put Block** (block id = index; idempotent, order-free); the owning fact/command's handler runs **Put Block List** (create-only) and verifies size + SHA-256 before the grain call. Blob path `{escaped-tenant}/{conversationId:N}/bodies/{bodyId:N}` in `forgeconversationartifacts`. The grain sees only references and compares `BodyId`+SHA-256 for idempotency; its commit does no blob I/O. Missing blocks → typed rejection (progress: Error + Failed). Uncommitted blocks expire after 7 days (B4). | Out-of-order safe on the session-less ingress queue; blob I/O stays out of the grain turn. |
| B8 | **Runner → Host:** chunks first in the conversation's progress session, then the fact with references. The runner's session-state outbox keeps only the small fact; its unused `OpaqueContinuation` copy is removed. | Session state stays far under 256 KB. |
| B9 | **Client → Host:** the ForgeAPI edge extracts body fields from the request it already deserializes (one Contracts helper per message), enforces 4 MiB (413), sends each chunk on `conversation-ingress` and waits for the Host's reply per chunk, then sends the command with references. Clients are unchanged. | The ingress queue has no sessions; per-chunk replies give the ordering. |
| B10 | **Host → runner (locked with Ameer 2026-09-30):** commands to the runner carry references; the runner **reads a body with a direct internal query** `GET /conversations/{id}/bodies/{bodyId}` on the Host (internal ingress, `X-Forge-Member-Id` as ForgeAPI sends it). The runner gets `ConversationHostBaseUrl` (forge-infra 500-app) and no storage role. | Commands on the bus, queries direct; the Host stays the only store owner. |
| B11 | **Hydration:** one `ConversationBodyHydrator` in the Host fills body fields when serving the events route, SSE (replay and live — the grain publishes events with references), snapshots/evaluation projection, project run index/events, hands work items, and memory composition. The events route's existence check uses the snapshot, not a full event read. Memory budget stays a fixed 32 KiB. | Client-facing `ConversationEvent` JSON is unchanged. |
| B12 | **Release:** Contracts 0.7.0 → Host, runner, ForgeAPI, forge-infra together. No mixed-version window: stop traffic, drain both queues, dev reset (as D14), deploy all. | Pre-launch; no compatibility paths. |
| B13 | **Command replies never carry bodies** (found in 8a planning: replies ride the 256 KB reply queue). `ClaimMissionHandsWork` replies with status, reason and tool request id only; Bob then reads the work item (hydrated arguments) through the existing `GetMissionHandsWork` query. Any other command reply that would carry a body (e.g. a duplicate `StartEvaluation`) returns ids and status; data is read by query. | Commands on the bus acknowledge; queries return data. Adds forge-client task 8e. |
| B14 | **Memory budget stays a fixed 32 KiB; a body larger than the budget is left out of memory.** The 8d proof checks the large path by the model answering from the end of the large message, not through memory. | The large-message path is proven where it is used (the runner's body read); no memory redesign (YAGNI). |

**Gates.** Security: one new internal Tier-2 → Tier-2 query route (runner → Host) on the existing internal ingress, no new store, identity role or public entry point; the Host remains the sole Table/Blob owner. Engineering Philosophy: one path for every body; one blob seam (`IConversationBodyStore`); one hydration seam; no size branch; no new knob beyond the 4 MiB cap and 192 KiB chunk constant. Failure boundary: a lost chunk → commit fails → typed rejection (progress: run Failed; ingress: 4xx reply); a Blob outage → handler throws → broker redelivery (receipts make it a no-op); a runner body read failure → the command is abandoned and retried.

### Task 8 build tasks

| Task | Repo | Scope | Done when |
|---|---|---|---|
| 8a | forge-conversations | Contracts 0.7.0 (reference type, chunk properties, deterministic body ids, body-field extractor, reference fields); Host: `IConversationBodyStore`, chunk handling on both consumers, reference-based grain and state, hydrator at every serving point, body query route, outbound commands with references. Package published. | Suite green, 0 warnings; tests: out-of-order and redelivered chunks, missing block → typed rejection, duplicate fact same/different hash, hydrated SSE/events equal the pre-change JSON shape, memory over bodies, body route member-scoped (other member → 404). Contracts 0.7.0 visible via `gh api`. |
| 8b | forge-runner | Publish chunks then fact; commands' references read via the Host body route; outbox keeps the small fact only; 4 MiB cap. | Suite green; tests for chunk-then-fact order, crash between chunks and fact → Interrupted, session state size. |
| 8c | forge-platform | Edge extracts, caps (413), chunks and awaits per-chunk replies, then sends the command. | Suite green; 4 MiB+1 → 413; chunk replies awaited before the command. |
| 8e | forge-client | Bob: after `ClaimMissionHandsWork` (status-only reply, B13), read the work item through `GetMissionHandsWork` and execute from it; Contracts 0.7.0. | Suite green; a test shows execution uses the queried work item. |
| 8d | forge-infra + deploy | 500-app: `ConversationHostBaseUrl` for the runner; image bumps; drain, reset, deploy all. | Default path: piped `forge chat` sends a ~400 KB single-line message ending with a codeword and asks for it; the turn completes and the answer contains the codeword (the runner read the whole body), reopening replays the message hydrated, a following small turn completes; Host/runner/edge logs clean. |

## Tasks 6+3 design (locked 2026-10-01)

Evidence so far (2026-10-01): the Orleans Container Apps tutorial runs one replica only (min = max = 1) and does
not cover silo-to-silo traffic. Our membership table shows every rollover's old silo marked Dead by its successor
(one suspecter each) — consistent with either a failed probe or a normal replacement, so not conclusive. The
Host uses default ports 11111/30000 and has one internal HTTP ingress.

**Type-2 test exception (2026-10-01):** to learn whether two Host replicas form one Orleans cluster, the Host
runs with min = max = 2 replicas via a forge-infra PR and `make 525-conversation-app`, then is reverted to 1 the
same way. Scope: dev Host only. Observation: both silos `Active` in `OrleansSiloInstances` with no suspicions for
5 minutes, and a grain call routed across silos. Reversal/removal: the revert PR, deployed immediately after.
Known effect while it runs: live SSE events can be missed (the in-process hub — the gap Task 6 fixes); no chat
use during the window.

**Test result (2026-10-01):** with min = max = 2 ([katasec/forge-infra#31](https://github.com/katasec/forge-infra/pull/31)),
both silos stayed `Active` with no suspicions for 6 minutes, and the Host log shows TCP connections on 11111
from the new silo to its sibling and to the previous revision's silo. Replicas and rollover revisions form one
Orleans cluster on Container Apps. Reverted to one replica ([katasec/forge-infra#32](https://github.com/katasec/forge-infra/pull/32), confirmed `maxReplicas: 1`).

| # | Decision | Why |
|---|---|---|
| F1 | **Fan-out = Orleans grain observers (per host).** Each Host process keeps its in-process `ConversationEventHub` for its own SSE readers. The first local reader of a conversation creates one `IConversationEventObserver` (`CreateObjectReference`, from non-grain code) and subscribes it on `ConversationGrain`; the last reader leaving unsubscribes and deletes the reference. The grain holds an Orleans `ObserverManager<IConversationEventObserver>` (in memory, not durable) and, after a confirmed commit, notifies with a `[OneWay]` call carrying the committed events. | Orleans' documented push pattern; no new infra, store or identity; correct on N silos and during rollover. Rejected: Orleans streams (memory = test-grade, Azure Queue = new infra and polling), broadcast channel (fans out to grains, not hosts), SignalR/Web PubSub or a Service Bus topic (new Tier-3 transport and roles). |
| F2 | **Lease and resubscribe:** observer entries expire after 2 minutes; each host resubscribes every 30 s. After every (re)subscribe, the host tells its local readers to catch up from the Table from their cursor (the E5 replay). | The observer list is lost when the grain reactivates on another silo; resubscribe restores it and the catch-up closes any window, including a missed final event. Fixed constants, no settings. |
| F3 | **Task 3: `PublishDeltaAsync` is `[AlwaysInterleave]`** (it stays synchronous, reads only the confirmed `State`, and notifies observers `[OneWay]`). | Deltas no longer wait behind commands on the grain queue; no contract change. A delta stamped at N while a commit to N+1 is in flight is dropped by the reader's existing `sequence == cursor` rule — harmless. |
| F4 | **Delta dedupe by `EventId` in the SSE writer** (a small per-connection set of recent delta ids). No runner offset, no contract change. | Service Bus redelivery reuses the delta's `EventId`; ordering and gaps inside a stream are cosmetic for a live-only draft (the durable message replaces it). |
| F5 | Replica count stays **1**. The design is correct for N; scaling out is a separate decision. | Fewest resources; nothing needs a second replica today. |

**Gates.** Security: no new entry point, store, identity or queue; observer calls use the existing internal Orleans cluster channel (proven above). Engineering Philosophy: one fan-out path (observers → local hub → SSE); two fixed constants (lease, resubscribe); the E5 gap rule is the single recovery path. Failure boundary: a dead or slow observer is dropped by `ObserverManager` and never blocks the grain (`[OneWay]`); a lost observer list is repaired by resubscribe + Table catch-up; an SSE host crash → client reconnects and replays from its cursor.

### Tasks 6+3 build tasks

| Task | Repo | Done when |
|---|---|---|
| 6+3 | forge-conversations (Host 0.8.0; Contracts unchanged) | Suite green, 0 warnings. Tests with **two silos in one test cluster**: an SSE reader on silo A receives every committed event of a grain living on silo B, in order, with no gaps; after the grain is deactivated and reactivated elsewhere, the reader receives the next events and a missed final event is caught up by resubscribe; a dead observer does not block commits; deltas interleave with a commit that is held in storage (the delta is published before the commit completes); a redelivered delta is written once. |
| Acceptance | forge-infra + deploy | Host 0.8.0 deployed with one replica. Default path: `forge chat` turn completes, reopening replays, no Host errors. Live multi-silo check (same Type-2 exception as above, reverted after): with two replicas, several `forge chat` turns all complete with full replies over repeated runs (SSE lands on either replica), and Host logs show no observer errors. Deltas: an events request with `deltas=true` through ForgeAPI shows delta frames before the final message (controlled check, stated as such). |

