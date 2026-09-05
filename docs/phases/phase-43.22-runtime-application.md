# Phase 43.22 task 3 — Mission application and read session

> **Design locked; Codex implements after tasks 1–2.** Parent: [reconstruction hub](phase-43.22-project-mission-reconstruction.md).
> Product operations below Presentation; no native/visual change in this task.

## File and responsibility boundary

Replace `Services/ProjectMissionRuntimeSession.cs` with `ProjectMissionApplication.cs` and
`ProjectMissionReadSession.cs` under `src/ForgeMission.ClientRuntime/`. Keep them in `Services/`.
The first coordinates manifest transactions and Host commands; the second owns a session's
subscription and bounded read windows. Do not move the candidate's combined class into a renamed
file. Register concrete owners in ClientRuntime `Program.cs` and its session store. Preserve the
existing admission/disposal guard; it does not substitute for the project file lease.

Keep `ConversationHostClient.cs` as the only Host HTTP/SSE adapter. Reuse
`ConversationTailReader.cs`, `ProjectMissionToolRefusal.cs`, existing event transport and typed
contracts. Revise their small seams as specified below. Preserve `ConversationRuntimeSession`
and `ConversationToolHandOff` for established durable compatibility clients. Remove their stale
comments claiming removed Project Control callers.

Update `src/ForgeMission.ClientRuntime.Transport/{ClientRuntimeContracts.cs,ClientRuntimeJsonContext.cs,
HttpClientRuntimeChannel.cs,ClientRuntimeEvent.cs}` and the existing relay
JSON context. New Project endpoint handlers in `Transport/ClientRuntimeEndpoints.cs` validate the
session, invoke one named operation and translate its result. No English-error parsing, lease,
container creation, tool policy, retry or UI policy lives in those handlers.

## Public surface-neutral contracts

Keep existing Project launcher, catalog-read and selection requests. Catalog has `[Janus, Naive]`,
canonical selected name or null, and truthful legacy-history flag. On invalid selection it still
returns the list plus `UnknownMission` so the user can repair it. Move shared built-in name constants
to Contracts (introduced with task 1's validator; Host/Worker adopt them in task 2/5).

Replace the candidate's start response with the explicit journal/result shape below. This is an
unreleased Project feature contract change; update all in-repo consumers together. Existing
`/transport/prompt`, `/v1` and non-Project durable DTOs are unaffected.

```csharp
public enum ProjectSubmissionState { Prepared, Accepted, Rejected }
public sealed record ProjectSubmissionView(
    Guid CommandId, string Mission, string Input, ProjectSubmissionState State,
    Guid? RunId, long? AcceptedSequence, ProjectOperationError? Rejection);
public sealed record StartProjectMissionRunRequest(
    string SessionId, Guid CommandId, Guid? PreviousCommandId, string Input);
public sealed record RetryProjectMissionSubmissionRequest(string SessionId, Guid CommandId);
public sealed record ProjectSubmissionResponse(
    ProjectSubmissionView? Submission, ProjectOperationError? Error);
public sealed record GetProjectMissionStateRequest(string SessionId);
public sealed record ProjectMissionState(
    ProjectMissionsView Missions, ProjectSubmissionView? Submission,
    ProjectRunPage? Runs, ProjectOperationError? HistoryError);
public sealed record GetProjectMissionStateResponse(
    ProjectMissionState? State, ProjectOperationError? Error);
public sealed record GetProjectRunsRequest(string SessionId, ProjectRunCursor? Cursor);
public sealed record GetProjectRunsResponse(ProjectRunPage? Page, ProjectOperationError? Error);
public sealed record GetProjectRunRequest(string SessionId, Guid RunId);
public sealed record GetProjectRunResponse(ProjectRunDetail? Run, ProjectOperationError? Error);
public sealed record GetProjectRunEventsRequest(
    string SessionId, Guid RunId, long AfterSequence, long? ThroughSequence);
public sealed record GetProjectRunEventsResponse(
    ProjectRunEventPage? Page, ProjectOperationError? Error);
```

`ProjectMissionsView`, `ProjectOperationError` are existing transport types. RunPage/Cursor/Detail/
EventPage/Summary are task 2's shared Contracts types; use them directly, not copied equivalents.
SubmissionView's RunId/AcceptedSequence exist only for Accepted; Rejection only for Rejected.
Prepared carries neither. Start/Retry may return **both** the committed Prepared view and an
operational Error (`SubmissionUncertain`); that explicitly distinguishes unresolved acceptance
from definitive rejection. Accepted returns its view and no Error. Rejected returns its view and
no outer Error; its Rejection explains the result. Pre-prepare validation has Error only.

State read returns no State for invalid/expired session or unreadable manifest. A valid local
Project can still return State with HistoryError if Host is down; this is availability metadata,
not an assertion that history is empty. Corrupt selection is represented by Missions.Selected=null
and outer UnknownMission, consistent with the existing repairable catalog contract.

| HTTP POST route | Named application operation |
|---|---|
| `/transport/project/mission/run` | `StartProjectMissionRunAsync(StartProjectMissionRunRequest, ct)` |
| `/transport/project/mission/retry` | `RetryProjectMissionSubmissionAsync(RetryProjectMissionSubmissionRequest, ct)` |
| `/transport/project/mission/state` | `GetProjectMissionStateAsync(GetProjectMissionStateRequest, ct)` |
| `/transport/project/runs` | `GetProjectRunsAsync(GetProjectRunsRequest, ct)` |
| `/transport/project/run` | `GetProjectRunAsync(GetProjectRunRequest, ct)` |
| `/transport/project/run/events` | `GetProjectRunEventsAsync(GetProjectRunEventsRequest, ct)` |

The existing `IClientRuntimeChannel` remains `SendAsync<TRequest,TResponse>(request, ct)` plus
`Subscribe(ct)`. The table names application operations, **not new channel-interface methods**.
Update HttpClientRuntimeChannel's existing request-type routing/STJ registrations for each pair;
do not add a second transport interface. `ProjectMissionApplication` owns Start/Retry; the read
session owns the four read operations. Existing catalog/selection keep their shared facade.

Each request binds to the existing live session's Project home. It cannot open arbitrary paths,
choose another container, call a provider or grant a capability. Unknown session uses the current
typed SessionNotFound behaviour. Host container reads must verify returned Project ID and
ProjectMission purpose against the manifest first; a mismatch is MissionRunConflict.

## Submission flow — no UI-owned retry policy

`ProjectMissionApplication.StartAsync` validates input then calls task 1's Prepare transaction.
The surface generates a GUID once for a deliberate new submission; it supplies the last journal
command ID it observed as PreviousCommandId. This is a normal request identity/concurrency token,
not a UI retry journal. After any uncertainty, surfaces use **Retry** on the Runtime's stored
command ID and supply no new mission/input. A refreshed or second surface can perform the same action.

1. For a new command only, require a usable, synchronized Host read state; if an active run is
   known, return RunAlreadyActive. A null-container new Project is empty/ready. This is an
   application precondition, not solely a disabled UI button; Host remains the final concurrency
   authority if another client starts afterward. Existing-ID Retry bypasses this read guard.
   Prepare immutable content under the project lease; release it before HTTP. If an equal
   Accepted/Rejected journal already exists, return it. A different Prepared intent is refused.
2. Ensure container using the existing deterministic creation command and the journal's ProjectGoal.
   Call idempotent create even when the manifest already has an ID on first dispatch/recovery, so
   Host verifies Project identity and pinned goal. Persist only an equal/null ID under the lease.
3. Submit the exact journal command/mission/input through the existing Host start API, with zero
   capabilities. Host checks deduplication **before** active-run rejection.
4. On valid acceptance, persist the acceptance receipt against that command ID. Return Accepted
   and invalidate the read session. A failed local receipt write returns SubmissionUncertain;
   the known Host run ID can be logged but is not fabricated into a committed local receipt.
5. A well-formed definitive 400/404/409 Project error becomes a persisted Rejected receipt.
   Timeout, request cancellation, connection fault, 5xx, bad JSON or unexpected success shape
   keeps Prepared and returns SubmissionUncertain. Do not clear or replace uncertain intent.

`RetryAsync` requires the current stored command ID, sends no caller payload and follows the same
dispatch steps. First use the Host command-receipt point query: an equal stored mission/input/
goal confirms acceptance; differing content is conflict. NotFound means **unknown**, not proof
of rejection: repeat the exact idempotent start. No automatic provider run is started by Open,
read, reconnect or elapsed time. State read does not mutate the journal. A reopened Prepared receipt remains visible for explicit
Retry, even if its run can already be inspected in Host history. Never auto-resubmit
an old instruction when somebody merely opens the Project.

Closing a window cancels session I/O, not an accepted durable run. Submitted network cancellation
is uncertainty; no rollback, Stop, abandon-pending or forget-intent action is introduced. If storage
is unavailable, preserve the last disk journal and report the error. A Project can recover after
Client Runtime restart without retaining any Presentation object.

| Local error code | Mapping / action |
|---|---|
| `ProjectBusy`, `ProjectChanged` | Task 1 contention/external edit; refresh or retry same action |
| `SubmissionPending`, `SubmissionChanged` | Refresh state and surface existing journal; do not mint an automatic replacement |
| `SubmissionUncertain` | Prepared remains; expose Retry on stored ID |
| `InvalidMissionInput`, `UnknownMission` | Existing submission validation/Host invalidRequest or unknownMission |
| `InvalidRunQuery` | invalidRequest on a history/cursor read, not an instruction error |
| `MissionRunNotFound`, `MissionRunConflict`, `RunAlreadyActive` | Host notFound; commandConflict/wrongPurpose; runAlreadyActive respectively |
| `HistoryUnavailable`, `HistoryInvalid`, `HistorySynchronizing` | Host outage/protocol fault; historyInvalid; historySynchronizing respectively |

Append new enum values without changing serialized historical ordinals. Keep existing manifest/
session errors for those failures. User-visible messages are safe and specific; logs include
operation/command/run IDs and full exception at the owning boundary, never request secrets.

## Read session and event delivery

`ProjectMissionReadSession` owns one Project container subscription per live session; no second
queue or durable event hub. Read-only Open does not create a container: null container means an
empty run page with zero watermarks. A stored ID is checked/read through Host. Start notifies the
read owner only after the deterministic container exists; notification contains an ID, not a
shared mutable journal. The session is disposed with the existing session slot and application
shutdown; task lifetime is awaited, not fire-and-forget.

Reuse `ConversationTailReader` with a supplied starting sequence (default zero for old callers)
and a lightweight publish adapter. For this feature, tail from the last observed sequence; on a
new session use zero so an outstanding tool refusal can be recovered. The read owner discards
raw historical bodies after notification; it does not accumulate every prior trace.

Revise tail cursor correctness in its one shared implementation: require next sequence to be
last+1, ignore <=last replay duplicates, treat gaps as protocol error and reconnect from last,
and advance last **only after** the on-event hook succeeds and notification is published.
Remove the ever-growing seen-event-ID set; contiguous sequence is the deduplication key. A failed
refusal hook must not advance past a tool request. Existing hook delivery must be idempotent;
verify compatibility-session tool tests after the change. Fixed reconnect delay stays 250 ms,
cancellable. Report connection/error state instead of silently swallowing permanent faults.

`ProjectMissionToolRefusal` retains the stable `ClientToolResult(requestId)` identity and zero
authority. It uses Host result submission only. Report failure and retry the same result before
advancing; if Host reports the run has moved past that request, verify its current expected-tool
state and treat it as resolved. Never execute a tool to get an apparently successful test.

Append one existing-envelope event kind `ProjectMissionChanged`, with new optional envelope property
`ProjectMissionChange? ProjectMission`, whose record is
`ProjectMissionChange(Guid ContainerId, long LastSequence)`. No raw expert messages or local paths
in that payload. It invalidates reads; it is not an authoritative list update. Subscribe before
initial state read. Coalesce invalidations (one refresh in flight plus one dirty bit), and refresh
on transport reconnection even if no notification arrived. The existing SSE hub can drop a hint
without losing durable facts because reads recover from Host. Maintain a fixed 1-second state
refresh while active, Prepared, or Synchronizing, and a 5-second refresh otherwise while a Project
is open; cancel on dispose. This bounded fallback also observes changes from another process.
No new global poller, session registry or configuration knob.

Keep only the current 20-run page (plus a transient replacement response), current Detail and a
selected 200-event trace page in the read session. Paging replaces the visible window; it does not
append unbounded histories in memory. A pure `ProjectRunReadState` reducer merges by RunId and
LastSequence within that window, rejects inconsistent equal sequences, and uses a generation ID
to discard responses from a previous Project or selected run. No Home-owned acceptance buffer.
For the same command ID, journal view transitions are monotonic Prepared→Accepted/Rejected;
a late Prepared HTTP response cannot overwrite a terminal view. A response for an older command
cannot replace the current journal. PreviousCommandId links committed replacements; otherwise
refresh the journal from Runtime instead of guessing an ordering between random GUIDs.
A long trace provides page navigation with absolute sequence cursors; latest/live is a distinct
view state. The existing transcript projection resets/replays only the selected page.

Explorer's asset/context projection stays local. Remove its independently computed Runs array:
change `ProjectWorkbenchProjection` to Project/Assets/Context only; both surfaces consume the
same `ProjectRunReadState` page. `ProjectWorkbenchProjector` never writes or trusts manifest.Runs
for current status. No fabricated run document via OpenProjectDocument; trace uses GetProjectRun.

## Done when / stateful tests

| Precondition | Positive / negative observation |
|---|---|
| Valid session, selected mission, no unresolved intent | One accepted run; blank/oversized/corrupt selection/expired session makes no container or run. |
| Lost acceptance response | Disconnect after Host acceptance, restart Runtime, change selected mission, Retry: same run and original mission/input; no second provider invocation. |
| Competing windows | Two new IDs with the same previous ID produce one journal winner; loser sees SubmissionPending/Changed, not overwritten content. |
| Host accepted, manifest receipt failed | Prepared survives and reconciliation finds original run; no success claim before receipt commits. |
| Typed outcomes | Changed Message prose/localization leaves error classification unchanged; missing/malformed envelope remains uncertain. |
| Subscription before acceptance | Early events, duplicate/reordered responses, gaps and reconnect never lose a run or double counts; old session responses cannot populate a new Project. |
| Zero authority | Injected ToolRequested yields idempotent refused result; failed delivery retries without cursor advance; no filesystem/terminal dispatch is reachable. |
| Lifecycle/bounds | Close/dispose cancels and awaits loops; no full-history list/trace accumulation; compatibility session/tool tests still pass. |
| Surface parity | All actions exercised through HTTP/channel contracts without Presentation, native Host, provider or datastore types in the caller. |

Run existing transport and session tests in their actual projects. Do not bypass broken project
references with audit-only rehosting and label that the full suite.
