# Phase 43.22 task 2 — Host outcomes and bounded history

> **Design locked; implement after task 1.** Parent: [reconstruction hub](phase-43.22-project-mission-reconstruction.md).
> No visual change. Default-path acceptance remains task 5's gate.

## Existing code to reuse and change

Selectively port candidate Project Mission create/start acceptance in
`src/ForgeMission.ConversationHost/Grains/ConversationGrain.cs`, its `IConversationGrain.cs` and
`ConversationGrainResults.cs`, API handlers in `Api/ConversationApiEndpoints.cs`, shared DTOs in
`src/ForgeMission.Conversations.Contracts/ConversationContracts.cs`, STJ context and deterministic
IDs. Exact preserved create/start shapes are listed below; no provider fields are added. Preserve `BeginRunAsync`, pending-transition repair, command equality, sequencing, outbox,
Worker checkpoints and existing compatibility semantics. Do not copy legacy writer additions.

Add Host-local `Grains/ProjectRunIndex.cs` (pure fold plus bounded orchestration) and
`Persistence/AzureTableProjectRunIndexStore.cs` / `IProjectRunIndexStore.cs` (real persistence seam).
Extend `IConversationEventStore` / `AzureTableConversationEventStore` with bounded range reads;
leave `AppendAsync`'s canonical transaction and existing callers unchanged. New contracts use
STJ generation; Orleans method arguments/results use concrete serializable wrappers with stable
field IDs, following current grain conventions. No JSON reflection or generic outcome framework.

## Typed Host errors

Project create/start and new reads return `ConversationApiError(string Code, string Message)` on
failure. `Code` is a stable lower-camel string. `Message` is safe display text, not exception detail.
Existing non-Project routes keep their compatibility shapes. Do not derive behaviour from English
messages or HTTP 409 alone.

| Code | HTTP | Meaning |
|---|---|---|
| `invalidRequest` | 400 | Invalid ID, blank input, invalid cursor/range, payload limit, unsupported fields |
| `unknownMission` | 400 | Mission outside the shared built-in allow-list |
| `notFound` | 404 | Missing container/run/command |
| `wrongPurpose` | 409 | Existing conversation is not this Project Mission container |
| `commandConflict` | 409 | Same command ID, changed content; mismatched Project/container |
| `runAlreadyActive` | 409 | A different run is active; no append |
| `historySynchronizing` | 409 | Requested run is not indexed yet; retry a bounded read; never interpret as missing history |
| `historyInvalid` | 409 | Stored history cannot form a valid index/trace; preserve it and report |
| `legacyReadOnly` | 410 | Retired write during the admission-freeze stage |
| `serviceUnavailable` | 503 | Storage/transport temporarily unavailable; acceptance may be uncertain |

`ConversationHostClient` is the only decoder. Unexpected status, missing/malformed code, or invalid
success body becomes transport/protocol uncertainty, not a fabricated definitive rejection.
Use the same `ProjectMissionNames` static constants/ordered list in the Contracts assembly for
Client Runtime, Host and Worker. Worker still resolves the named asset; the UI gets the catalog
from Client Runtime. A shared name list is not a provider/catalog framework.

Input limits apply at both trusted application admission points: nonblank, ≤32,000 UTF-16 units
and ≤16,384 UTF-8 bytes. Additionally serialize the planned UserMessage with the existing STJ
context before beginning a transition; reject if its JSON exceeds `MaxInlineEventJsonBytes`
(currently 48 KiB). This catches JSON escaping overhead before an outbox/append side effect.
Do not change the limit for unrelated compatibility routes. Project starts accept **zero** capability
declarations and never select provider/model/expert from a caller field.

## Preserved Project command contracts

The following Contracts types and routes retain the candidate's meaning and deterministic IDs:

```csharp
public sealed record CreateProjectMissionContainerRequest(
    Guid ProjectId, Guid CommandId, string ProjectGoal);
public sealed record CreateProjectMissionContainerResponse(Guid ContainerId, long AcceptedSequence);
public sealed record StartProjectMissionRunRequest(
    Guid ContainerId, Guid CommandId, string Mission, string Input);
public sealed record StartProjectMissionRunResponse(
    Guid ContainerId, Guid RunId, long AcceptedSequence, ConversationRunStatus Status);
```

Host routes: `POST /conversations/project-mission` and
`POST /conversations/{containerId}/mission-runs`. Validate route/body ID equality. Container ID
is `ConversationDeterministicIds.Conversation(ProjectMissionContainerCreate(projectId))`, with
create command ID `ProjectMissionContainerCreate(projectId)`; run ID is `ProjectMissionRun(commandId)`.
Preserve the candidate UUID namespace/name strings. Create pins Project ID and goal, not a mission
or capabilities; equal create retries return the same container, changed goal/identity conflicts.
Successful create is 201, start 202. An absent container must return notFound rather than wrongPurpose.

## Public read contracts

All IDs are GUIDs, sequences signed 64-bit positive values (zero means before the first event),
timestamps UTC. Wire names follow existing camel-case STJ conventions. No local paths, raw
accepted-command JSON, provider secrets or capability declarations leave these read DTOs.

```csharp
public sealed record ProjectRunSummary(
    Guid RunId, Guid CommandId, string Mission, string Title,
    long AcceptedSequence, long LastSequence, ConversationRunStatus Status,
    int ExpertTurns, int ToolCalls, DateTimeOffset AcceptedAtUtc);
public sealed record ProjectRunCursor(long AnchorSequence, long BeforeAcceptedSequence);
public sealed record ProjectRunPage(
    Guid ContainerId, long IndexedSequence, long TargetSequence,
    bool Synchronizing, ProjectRunSummary[] Runs, ProjectRunCursor? Next);
public sealed record ProjectRunDetail(
    ProjectRunSummary Run, string Input, long IndexedSequence, long TargetSequence);
public sealed record ProjectRunEventPage(
    Guid ContainerId, Guid RunId, long ThroughSequence,
    long ScannedThroughSequence, ConversationEvent[] Events, bool HasMore);
public sealed record ProjectCommandReceipt(
    Guid ContainerId, Guid RunId, string Mission, string Input, string ProjectGoal,
    long AcceptedSequence, ConversationRunStatus Status);
```

`Title` is the first 120 Unicode scalar values of the submitted instruction with whitespace runs
collapsed to one space, plus `…` only if truncated. It is a display label, never the canonical
input. Full instruction is available in Detail; exact expert messages are only in event pages.
`Status` in CommandReceipt is the original acceptance status. Status in Summary is current at
the returned index watermark. Never equate Completed with verified output.

| Host route | Request/response and validation |
|---|---|
| `GET /conversations/{containerId}/runs?anchor=&before=` | Optional cursor fields must be both absent or both present. Returns ProjectRunPage, 20 runs maximum, newest accepted first. Invalid nonpositive/reversed/future cursor is 400. |
| `GET /conversations/{containerId}/runs/{runId}` | ProjectRunDetail, or 404. It must belong to this Project Mission container. Source full input from its canonical accepted UserMessage, not a UI cache. If indexing is behind and the run is not indexed, return historySynchronizing; only caught-up absence is 404. |
| `GET /conversations/{containerId}/runs/{runId}/events?after=0&through=...` | ProjectRunEventPage. `through` omitted pins the current durable LastSequence; later pages preserve it. Require `0 ≤ after ≤ through ≤ current LastSequence`. |
| `GET /conversations/{containerId}/project-commands/{commandId}` | Point lookup by accepted event ID; returns ProjectCommandReceipt or 404. Validates purpose and stored StartMission command. No scans, no execution, no new event. Input/ProjectGoal permit exact journal-content verification. |

Add these methods to `IConversationGrain` (all return
`Task<ConversationProjectReadResult>`): `ReadProjectRunsAsync(long? anchor, long? before)`,
`ReadProjectRunAsync(Guid runId)`,
`ReadProjectRunEventsAsync(Guid runId, long after, long? through)`, and
`ReadProjectCommandAsync(Guid commandId)`.

Use one Host-local Orleans wrapper in `ConversationGrainResults.cs`, matching the existing
JSON-across-grain-boundary convention: `ConversationProjectReadResult(string? PayloadJson,
string? ErrorCode, string? ErrorMessage)`, `[GenerateSerializer]` with IDs 0, 1, 2 respectively.
Success has only PayloadJson, serialized with the matching task-2 shared DTO's STJ type info;
error has only code/message. Each API adapter deserializes its known response DTO with generated
metadata. This keeps Orleans annotations/packages out of the shared Contracts project.
Do not invent undefined wrappers or a generic response framework.
API handlers validate route syntax and translate outcomes only. Grain derives its own address
using existing identity policy; requests cannot supply a Table partition or tenant.

## Derived run index — why, storage and algorithm

Source inspection established that public events omit per-run mission identity while the
idempotency row retains `AcceptedCommandJson`. Repeated full-history scans per list or run would
make cost grow with all prior output. Use a **rebuildable query index in the existing event table**,
owned solely by Host. This adds no canonical store and changes no execution/append protocol.

| Row key within existing ConversationAddress.PartitionKey | Stored shape |
|---|---|
| `0-{sequence:D19}`, `1-{eventId:N}` | Existing authoritative Event / Idempotency rows, unchanged |
| `2-{long.MaxValue - acceptedSequence:D19}` | `SummaryJson` containing ProjectRunSummary; descending-acceptance listing index |
| `3-{runId:N}` | Same `SummaryJson`, point lookup index |
| `4-project-run-index` | `Version=1`, `IndexedSequence` (Int64); absent means version 1, sequence 0 |

The two summary rows are atomic indexes of one derived fact, not independently writable models.
No public writer/set-status route exists. `IProjectRunIndexStore` exposes `ReadCheckpointAsync`,
`FindRunAsync`, `CommitBatchAsync(expectedCheckpoint, summaries, nextSequence)`, and
`ReadPageAsync(anchorSequence, beforeAcceptedSequence, count)` with explicit
`ConversationAddress` and `CancellationToken`. Define Host-local
`ProjectRunIndexCheckpoint(int Version, long IndexedSequence, string? ETag)`; absent ETag means
insert-only checkpoint. Commit uses its ETag to reject competing batches. Query count is fixed
by the caller to 21 (20 plus existence probe), not unlimited enumeration.

Exact Host-local persistence signatures (use existing ConversationAddress and shared summary):

```csharp
Task<ProjectRunIndexCheckpoint> ReadCheckpointAsync(ConversationAddress address, CancellationToken ct);
Task<ProjectRunSummary?> FindRunAsync(ConversationAddress address, Guid runId, CancellationToken ct);
Task<ProjectRunIndexCheckpoint> CommitBatchAsync(ConversationAddress address,
    ProjectRunIndexCheckpoint expectedCheckpoint, ProjectRunSummary[] summaries,
    long nextSequence, CancellationToken ct);
Task<ProjectRunSummary[]> ReadPageAsync(ConversationAddress address,
    long anchorSequence, long? beforeAcceptedSequence, int count, CancellationToken ct);
// On IConversationEventStore:
Task<ConversationEvent[]> ReadRangeAsync(ConversationAddress address,
    long after, long through, int count, CancellationToken ct);
```

`IConversationEventStore.ReadRangeAsync(address, after, through, count, ct)` returns at most
`count` **contiguous conversation events**, ascending, with sequence predicate in the Table row-key
range. It reads no idempotency/index rows. New callers use fixed `count=25` for index advance,
`count=200` for trace. Existing replay APIs need not change their contracts.

1. In the serialized grain call, repair existing pending durable transitions as current reads do;
   capture `TargetSequence = checkpoint.LastSequence`. Read index checkpoint.
2. Read at most 25 canonical events after IndexedSequence through target. For a UserMessage,
   point-read `FindByEventIdAsync` and deserialize its accepted StartMission command. Check
   command ID/event ID, derived run ID, container, known mission and zero capabilities.
3. Fold events in order, loading the existing summary with FindRunAsync once per distinct run
   into a batch-local dictionary (at most 25 point reads). First UserMessage initializes a summary (Queued, zero counts).
   ParticipantMessage increments ExpertTurns for a non-user participant; ToolRequested increments
   ToolCalls; RunStatus assigns the durable enum; every run event advances LastSequence.
   Other events do not increment these counts. A status/participant event before its accepted
   UserMessage, missing command, gap, wrong run/container, unknown index version or inconsistent
   mission is `historyInvalid`, never invented data. Container-level RunId-null events only
   advance the index cursor. Legacy ProjectControl is excluded by purpose, not reinterpreted.
4. Collapse changes to one final summary per affected run. In **one Table transaction**, upsert
   both index rows per affected run and insert/update the checkpoint with its expected ETag.
   At most 25 distinct runs means at most 51 operations. Serialized summary ≤ 2 KiB; reject an
   invalid larger summary. No expert/input body is copied into indexes. Commit failure leaves
   cursor unchanged; retry folds the same range. ETag conflict rereads on the next request.
5. Query the page/point index after that single batch; return Synchronizing when index < target.
   Never loop through all old events inside one request. Runtime schedules the next bounded call.

Index maintenance is lazy and resumable on read; it is **not** on the run-acceptance/progress critical
path. A bad index cannot stop an already accepted durable execution. The Runtime admission guard returns
HistoryUnavailable/HistoryInvalid for a new Start while its current read state cannot establish
active state; Retry of an existing immutable journal remains permitted. UI mirrors that guard. A failed read never clears rows.
Automatic deletion/rebuild is not necessary for normal operation: old stores begin at cursor zero,
crashes resume from checkpoint. A future index-version change needs an explicit migration, not
silent fallback. Existing event queries use prefixes below `1-`, so index rows never become events.

### Pagination and live consistency

First page sets AnchorSequence to the index watermark; next pages carry the same anchor and an
exclusive BeforeAcceptedSequence equal to the last displayed accepted sequence. Use row-key
ranges, not a scan with a RunId predicate. Newer runs appear on **Refresh/latest page**; loading older
pages cannot shift/duplicate the existing acceptance ordering. A later page can have a newer
IndexedSequence and fresher statuses: this is a live list, not a frozen historical snapshot.
Merge summaries only when incoming LastSequence is higher (equal must agree). Never downgrade a
terminal status from an older response. Empty page during initial indexing is **Loading runs…**,
not proof that the Project has no runs.

Trace pages scan at most 200 **conversation** events and then filter by validated run ID.
ScannedThroughSequence advances across other runs too; an empty Events array with HasMore is valid.
`HasMore = ScannedThroughSequence < ThroughSequence`. A missing sequence inside the range fails
`historyInvalid`; do not skip. To follow live activity, complete the pinned range, then request a
new range after the last scanned sequence. Exactness means all returned events are unmodified;
no summary replaces long output. The UI may page it and label unloaded content.

## Done when / preconditions

| Precondition | Required positive and negative evidence |
|---|---|
| Existing container and matching purpose | Janus/Naive alternation and equal retries pass; foreign run, missing container, legacy purpose, unknown mission and changed-ID-content return typed errors without append. |
| Complete indexed source | Old candidate history reindexes from zero; >25-event history advances across calls; crash before/after batch commit gives identical final summaries/counts. Missing/malformed accepted command fails visibly. |
| Bounded work | Instrumented store tests show ≤25 events plus per-event point lookups per index advance, ≤21 summary reads, ≤200 scanned events per trace call; query count does not grow with all historical runs. |
| Pagination | ≥45 runs, new runs between pages, changing statuses, duplicated responses and page refresh yield stable IDs/order and no stale status overwrite. |
| Exact trace | Paged original events match stored events field-for-field, across empty filtered pages, large output, reconnect and concurrent later runs. |
| Error/serialization | Each code round-trips AOT/STJ/Orleans; localized Message changes do not change classification; unexpected success/error bodies are not accepted as valid. |
| Retained engine | Existing grain/Host outbox/persistence tests still pass; derived indexing creates no queue commands and does not modify canonical event/idempotency rows. |

Use real Azurite for transactional/index tests plus bounded fake-store tests. Record these as
component evidence, not a substitute for the full suite or default route.
