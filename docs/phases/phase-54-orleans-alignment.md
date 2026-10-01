# Phase 54 — Orleans alignment (forge-conversations Host)

> **Status: complete 2026-10-01.** Every candidate task is done and live in dev (Host 0.8.0, runner 0.18.0 — now 0.19.0 after [Phase 55](phase-55-forge-chat-hands.md),
> ForgeAPI 0.7.0). Evidence: [completed record](phase-54-orleans-alignment_completed.md).
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
| 3 ✅ | Deltas off the non-reentrant queue (publish without a grain turn, or `[AlwaysInterleave]` with no state change) plus delta sequence dedupe | Streaming no longer queues behind commands |
| 4 ✅ | `[ReadOnly]` on read methods (after confirming they don't mutate) | Reads stop waiting on each other |
| 5 ✅ | Activation limited to local repair; explicit decision on the fail-closed corruption check | Predictable activation |
| 6 ✅ | Before any second silo: SSE hosts subscribe as grain observers with resubscribe and log catch-up | Correct fan-out on multiple silos and during rollover |
| 7 ✅ | `[ResponseTimeout]` on long methods | Explicit timeouts |

| 8 ✅ | Large bodies via claim-check: body in Host-owned Blob, event carries a preview + reference | Removes the per-event ceiling (30 KiB today, 256 KB transport) up to a 4 MiB guardrail |

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
| D10 | **Outbox:** a transition that owes a mission command records it in state (`DispatchOwed`). Register the reminder before raising; after confirm, send (`MessageId = CommandId`), then raise `DispatchSent` (state-row-only commit) and unregister when nothing is owed. What remains is drained by the next reminder tick and after the next call to the grain (`CommitTransitionAsync`); activation does not drain (the grain has no `OnActivateAsync`; corrected 2026-10-01). `DispatchState.BrokerAccepted` is deleted. | Service Bus cannot join the Table transaction; the owed command is durable before the send, and the send is idempotent within the 10-min duplicate-detection window (existing exposure beyond it is unchanged). |
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

## Tasks 4+7 design (locked 2026-09-30) — done, Host 0.6.1, see [completed record](phase-54-orleans-alignment_completed.md#tasks-47--e5--readonly-reads-progress-timeout-sse-gap-rule-done-2026-09-30)

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

## Task 8 design (locked 2026-09-30) — done 2026-10-01, see [completed record](phase-54-orleans-alignment_completed.md#task-8--every-body-in-blob-via-claim-check-done-2026-10-01)

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

## Tasks 6+3 design (locked 2026-10-01) — done, see [completed record](phase-54-orleans-alignment_completed.md#tasks-63--grain-observers-for-n-silos-interleaved-deltas-done-2026-10-01)

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

## Next

Phase complete. Follow-ups are in the [backlog](../backlog.md): the conversation Host explainer doc, the Desktop client upgrade, and `forge chat` with hands.
