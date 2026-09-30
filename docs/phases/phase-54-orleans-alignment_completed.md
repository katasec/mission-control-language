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
