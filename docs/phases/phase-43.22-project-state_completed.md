# Phase 43.22 task 1 — Project state completion record

Verified 2026-09-05. The active design and contracts remain in
[Project state](phase-43.22-project-state.md).

## Delivered boundary

`ProjectStore` now uses `ProjectManifestFile` as its sole lease/read/publish adapter. The adapter
uses the stable per-home lock, unique same-directory temporary files, bounded reads, byte-change
detection and atomic replacement. Manifest v3 adds the immutable submission journal and preserves
v1/v2 data in memory until a successful mutation publishes v3. The legacy Project Control
conversation ID remains isolated in its migrated field until task 5 retires that route.

`ProjectMissionNames` is the shared, read-only Janus/Naive catalog. The added
`ForgeMission.ProjectStoreProbe` exists only to prove the operating-system process boundary; it
does not add a production service, queue, capability or HTTP dependency.

## Failure and complexity review

| Failure | Owner and observed result | Recovery / negative evidence |
|---|---|---|
| Another process holds the lease | `ProjectManifestFile` returns `ProjectBusy`; cancellation remains cancellation. | Held-lease and cancellation tests leave the manifest bytes unchanged. |
| Read or publication I/O fails | Adapter returns `ManifestReadFailed` or `ManifestWriteFailed`; receipt publication becomes `SubmissionUncertain`. | Original manifest stays readable; injected write and typed read-failure tests pass. |
| Nonparticipating edit is observed | Adapter returns `ProjectChanged`. | The changed bytes are never replaced. |
| Process ends during publication | The on-disk manifest is the old or new complete file. | Child `Environment.FailFast` probes at pre-flush, pre-rename and post-rename prove the state; stale unique temporary files remain ignored. |
| Journal identity or payload conflicts | `ProjectStore` returns the named submission/mission conflict. | Retry and changed-payload tests prove no mutable retry data is substituted. |

The parent review found the initial test set lacked real crash and receipt-race evidence, returned
it for correction, then accepted the completed version. Added production methods were reviewed
against the complexity gate: lease acquisition, publication, journal transitions and validation
are named, cohesive operations, each below the normal threshold of 15 with no artificial wrapper
layers. No exception was recorded.

## Verification

| Check | Observation |
|---|---|
| Focused state and Project Control compatibility tests | `dotnet test src/ForgeMission.Tests/ForgeMission.Tests.csproj --no-restore --filter "FullyQualifiedName~ProjectStoreTests|FullyQualifiedName~ProjectMissionControlSessionTests"` — **72 passed, 0 failed**. This includes two independent OS processes racing receipt/container updates across 60 projects. |
| Repository build | `dotnet build src/ForgeMission.slnx --no-restore` — **0 warnings, 0 errors**. |
| Diff hygiene | `git diff --check` passed. |

Default-path acceptance remains aggregated in task 5 because this task exposes no user-facing
route by itself.
