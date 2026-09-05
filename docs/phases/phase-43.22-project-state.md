# Phase 43.22 task 1 — Project state and submission journal

> **Verified 2026-09-05.** Evidence: [task completion record](phase-43.22-project-state_completed.md). Parent: [reconstruction hub](phase-43.22-project-mission-reconstruction.md).
> Non-visual component work; default-path proof is aggregated in task 5, not waived.

## Task and files

Replace the persistence coordination in `src/ForgeMission.ClientRuntime/Services/ProjectStore.cs`.
Add `ProjectManifestFile.cs` in that directory as the only lease/read/publish adapter; keep pure
manifest validation/migration in `ProjectStore`. Update `ProjectManifest.cs` and
`ProjectManifestJsonContext.cs`. Add shared `ProjectMissionNames` constants/ordered names in
Conversations.Contracts and the `ProjectMissions` validation helper here; add the task-1 local
error enum values in ClientRuntime.Transport. Task 2 adopts those names for Host/Worker.
Keep proven create, naming, path validation and launcher behaviour.
Add meaningful tests in `src/ForgeMission.Tests/ClientRuntime/ProjectStoreTests.cs` and a small
process test fixture for cross-process contention/crash injection. Do not import candidate Core,
CLI, OCI/source-resolution changes or mass-regenerate mission locks.

## Transaction protocol

`ProjectManifestFile.Read(home)` performs a bounded validated read; `UpdateAsync(home, transform,
ct)` acquires the project lease, reads the latest file, invokes one pure synchronous transform,
validates/serializes the result and publishes it before releasing the lease. `transform` is
`Func<ProjectManifest, ProjectManifest>`; its exception leaves the original untouched. It cannot
perform HTTP/async work. All mutations, including create-if-absent, migration, selection, container
ID and submission receipt, use this one owner. Initial home-directory creation remains the
existing collision-safe launcher operation; manifest existence is rechecked under the lease.

| Rule | Exact decision |
|---|---|
| Lease | Stable `<home>/.forge-project.lock`, opened `OpenOrCreate`, read/write, `FileShare.None`; retain the file permanently, dispose the handle to release. Retry only sharing/lock contention every 50 ms, cancellably, for at most 5 seconds; return `ProjectBusy`. Permissions/disk faults are not contention. |
| Identity | Lease is a file within the actual home, so aliases reaching the same directory share it; existing root/symlink containment rules still apply. Never lock by session ID or just an in-process dictionary. |
| Publication | Serialize to a unique `.forge-project.<guid>.tmp` in the same directory using create-new; flush to disk; atomically replace/move into `forge.project.json`. Existing reader handles must permit replacement. No delete-then-write gap. Clean up only this operation's temp path in `finally`. |
| Read limits | 2 MiB UTF-8 manifest maximum, checked before allocation and serialization. Existing larger files fail `InvalidManifest` unchanged; do not truncate. Unknown future version fails unchanged. |
| External edits | Compare the original file bytes/existence immediately before replace; a detected change returns `ProjectChanged` without overwriting it. This detects observed edits; it cannot make nonparticipating external writers transactional. Supported concurrent writers use the lease. |
| Crash scope | Process crash leaves either old or new valid manifest; stale unique temps are ignored, never promoted. Test kills before flush, before rename and after rename. No claim of power-loss durability of parent-directory metadata. |
| Cancellation | Before publish leaves old state. After publish is committed even if caller disconnects; reread by identity resolves uncertainty. Never report rollback of a committed write. |
| Support | Current macOS local-filesystem target; exercise Linux/Windows file semantics before claiming those targets. Network filesystems are not supported by this transaction contract. Do not claim automatic detection of every remote mount. |

Expose `ReadForHome` and the existing launcher facade. Mutation helpers delegate to `UpdateAsync`;
remove any other `ReadForWrite`/`ReplaceManifest` sequence. Keep the lease adapter narrow; no
general transaction framework, lock service, public callback API or background cleanup process.

## Manifest v3 — complete extension

Retain candidate v2's typed assets, selection, attached context, runs, Project ID/title/goal,
`projectMissionContainerId`, and `legacyProjectControlConversationId`. Add exactly the nullable
`submission` below; `CurrentSchemaVersion = 3`. Preserve JSON naming/null conventions and use STJ
source generation for every new root/nested type. Historical enums retain serialized values.

```csharp
internal enum ProjectSubmissionPhase { Prepared, Accepted, Rejected }
internal sealed record ProjectSubmission(
    Guid CommandId,
    Guid? PreviousCommandId,
    string Mission,
    string Input,
    string ProjectGoal,
    ProjectSubmissionPhase Phase,
    ProjectSubmissionAcceptance? Acceptance,
    ProjectSubmissionRejection? Rejection);
internal sealed record ProjectSubmissionAcceptance(
    Guid ContainerId, Guid RunId, long AcceptedSequence,
    ConversationRunStatus Status);
internal sealed record ProjectSubmissionRejection(string Code, string Message);
```

`ConversationRunStatus` is `ForgeMission.Conversations.Contracts.ConversationRunStatus`. Journal rejection `Code` is one of the
stable Host/local codes defined in task 3; this is not a second error taxonomy. The public journal
view omits `ProjectGoal` and local paths. `Status` is the **acceptance-time** status, never a mutable
run-status cache. Authoritative current status comes from the Host read model.

| Invariant | Rule |
|---|---|
| Cardinality | Zero or one journal record. It holds the last attempt/receipt, not a transcript or history database. A completed receipt is replaced only by the next deliberate Start. |
| Content | Nonempty command ID; mission exactly `Janus` or `Naive`; nonblank input, at most 32,000 UTF-16 code units **and 16,384 UTF-8 bytes**; no normalization after preparation. Goal is the manifest goal captured on first prepare. |
| Size | Entire serialized journal must be ≤ 96 KiB UTF-8; otherwise `InvalidMissionInput` before publication/network. Full manifest limit also applies. |
| Phase | Prepared has neither result; Accepted has only a nonempty acceptance with positive sequence; Rejected has only a code/message. Invalid combinations fail `InvalidManifest`; never guess an outcome. |
| Identity | All fields before `Phase` are immutable. An attempted changed input under an existing ID returns `MissionRunConflict`. Receipt writes compare the current command ID and immutable fields under the lease. |
| Selection | Selection may change while Prepared/Accepted; it never mutates the journal. Selection validation/repair has one implementation, `ProjectMissions`; a corrupt selection is not silently defaulted. |
| Container | Persist a returned container ID only if null or equal. An unequal stored value is `MissionRunConflict`, never overwritten. Host confirms Project ID/purpose before reads. |
| Goal | Creation and any recovery use the captured goal. A changed manifest goal conflicting with an existing Host container is an explicit conflict; do not repin server state or rewrite a pending request. No goal-edit action is introduced. |

### State transitions and operation methods

The task-3 application calls these methods on `ProjectStore`; methods return the committed
`ProjectRecord` (existing type), or throw existing `ProjectOperationException` carrying the named
code. `PrepareSubmissionAsync(home, commandId, previousCommandId, input, ct)` derives mission/goal
itself. `RecordSubmissionAcceptedAsync(home, commandId, acceptance, ct)` and
`RecordSubmissionRejectedAsync(home, commandId, rejection, ct)` never replace an unrelated record.
`SelectMissionAsync(home, mission, ct)` and `SetProjectMissionContainerIdAsync(home, id, ct)` share
the transaction owner. A nullable previous ID is an optimistic concurrency token, not a retry ID.

| Existing journal / request | Result |
|---|---|
| None, previous ID null, new ID | Prepare immutable record; then network may begin. |
| Same command ID and identical input/previous ID | Return existing record without rereading selection into it or resetting phase. |
| Same ID, changed input/previous ID | `MissionRunConflict`; no mutation/network. |
| Different command ID while Prepared | `SubmissionPending`; show/retry the existing intent. Never replace it, even if a run appears terminal elsewhere. |
| Different ID after Accepted/Rejected, previous ID equals stored command ID | Replace with newly prepared selection/input/goal. Host still enforces one active run. |
| Mismatched previous ID | `SubmissionChanged`; caller refreshes; no new request or overwrite. |
| Definitive equal acceptance/rejection receipt | Set terminal journal phase atomically; equal repeats are no-ops. Conflicting receipts are `MissionRunConflict`. |
| Timeout, disconnect, cancellation after prepare, unreadable Host response, HTTP 5xx | Keep Prepared. It means acceptance is unresolved, not that no run exists. |
| Receipt publication fails after Host accepted | Keep last valid disk state and return `SubmissionUncertain`; later Retry resolves by the same command ID. Never mint another. |

### Migration and legacy data

Read v1/v2 as v3 **in memory**; do not rewrite on simple Open. Next successful mutation writes
v3 under the lease. v1 moves `missionControlConversationId` to the legacy pointer, sets new
container null and submission null. v2 preserves both IDs, selection and arrays, adds submission
null. An unknown selection remains repairable through the picker; unrelated invalid fields fail.
Conflicting v1/v2 legacy IDs fail explicitly. Never translate old control messages into runs.

`runs[]` remains readable/preserved historical metadata only; it is never updated or used as
current lifecycle truth. The new UI does not pretend those entries are Host-confirmed mission
runs. Removing this compatibility field requires a separate data migration. Reject newer schemas
in older clients; do not launch an old binary against a v3 manifest during acceptance.

## Done when / preconditions

| Precondition | Positive / negative observation |
|---|---|
| Valid home and readable known schema | Existing create/open/path cases pass; traversal, permission failure, oversized/newer/malformed manifest fail unchanged. |
| Exclusive whole transaction | Two separate processes race selection and receipt/container changes for 60 projects: both values retained; no malformed files. Held lease times out as ProjectBusy; cancellation terminates promptly. |
| Atomic publication | Injected kill/fault boundaries preserve old-or-new valid file; no other operation's temp is removed; disk-full and permission errors do not erase original. |
| Immutable intent | Selection change between prepare and retry preserves original mission/input; same ID changed payload fails; stale previous ID and a second pending intent fail with no writes. |
| Migration | v1/v2 preserve IDs/assets/context/history; Open is nonmutating; next successful write is v3; failed writes leave original bytes. |
| No extra authority | Persistence has no HTTP/provider/capability dependency; no app launch creates a Project. |

Record focused tests and actual process results in a task completion companion. Code review must
show every manifest writer using the same lease. Passing serial setter tests alone is insufficient.
