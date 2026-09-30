# Phase 54 — Orleans alignment: completed work

## Tasks 1+2 — delete `MissionRunGrain`; `JournaledGrain` single atomic write (done 2026-09-30)

Design: [spoke D1–D14](phase-54-orleans-alignment.md#tasks-12-design-locked-2026-09-30).
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
[D1–D14](phase-54-orleans-alignment.md#tasks-12-design-locked-2026-09-30). Kept for its measurements
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

Design: [spoke E1–E5](phase-54-orleans-alignment.md#tasks-47-design-locked-2026-09-30-build-ready).
Code: [katasec/forge-conversations#14](https://github.com/katasec/forge-conversations/pull/14). Deploy:
[katasec/forge-infra#29](https://github.com/katasec/forge-infra/pull/29), Host `0.6.1`
(`sha256:3fbe6e8c…f320`), revision `--0000006`.

| Check | Observation |
|---|---|
| Tests (supervisor rerun) | `-warnaserror` 0 warnings; 217/217. The four new tests fail on the old code (gap test got ids `1,2,4`; overlap test timed out at 35 s; reflection found no ReadOnly). CI "Verify Conversations packages" passed. |
| Deploy | what-if: image 0.6.0 → 0.6.1 only; `make 525-conversation-app` Succeeded 14:11:31Z; revision Healthy, silo started 14:11:53Z. |
| Default path | Piped `forge chat` (forge 1.0.0+c041dee, no overrides): a new turn answered `BLUE-HERON-54` from earlier turns, prior turns replayed; Host logs: no errors, no SSE gap warnings. **PASS.** Timeout proven by reflection only (slow runtime test skipped by decision). |

## Task 8 — every body in Blob via claim-check (done 2026-10-01)

Design: [spoke B1–B14](phase-54-orleans-alignment.md#task-8-design-locked-2026-09-30-build-ready).

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
| Not proven live | Bob's claim-then-read (B13): no default client attaches hands (`forge chat` is NoHands; Desktop deferred — [backlog](../backlog.md)). Controlled tests only. |

**Gotcha:** the Host logged `TableBeingDeleted` for the reminders table at startup because the reset had just
deleted it; Orleans retried and started the reminder service a second later. In a reset, delete Orleans
tables well before the deploy, or leave the reminders table in place.

## Tasks 6+3 — grain observers for N silos; interleaved deltas (done 2026-10-01)

Design: [spoke F1–F5](phase-54-orleans-alignment.md#tasks-63-design-locked-2026-10-01-build-ready).
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
