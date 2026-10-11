# Phase 76.10.1 — Contracts/Host output continuation producer

**Status:** PLAN APPROVED by supervisor on2026-10-11 after full current simplicity r2 and
ownership r2 PASS; same implementer authorized for the exact producer scope. Parent:
[output continuation design](phase-76.10-output-continuations.md).

## Scope

Implement only the Contracts/Host producer and normal Contracts0.13.0 release plumbing in
`/Users/ameerdeen/progs/forge-conversations`. No Runner, Platform, infra, dependency or UI change.
The parent's `Done when`, security/data ownership, failure boundaries and default-path gates apply.
This producer is a delivery dependency; its managed/package evidence cannot close cloud acceptance.

## Plan r2

All paths below are relative to the owning repo. Every change reuses the parent's locked flow.

| File | Purpose |
|---|---|
| `src/ForgeMission.Conversations.Contracts/MissionProducedFiles.cs` (new) | Exact `MissionProducedFile` record, bounded inert JSON parser/validator; move the existing output-path grammar here, adding the documented128-byte filename bound |
| `src/ForgeMission.Conversations.Contracts/ConversationContracts.cs` | Trailing optional `ProducedFiles` on command/progress |
| `src/ForgeMission.Conversations.Contracts/ConversationBodies.cs` | Fixed body field `mission-produced-files` |
| `src/ForgeMission.Conversations.Contracts/ConversationMessages.cs` | Fixed `ConversationQueryNames.GetMissionContent` plus `All` entry for the parent's already-defined later API projection |
| `src/ForgeMission.Conversations.Contracts/ConversationContractsJsonContext.cs` | Generated JSON for record/array |
| `src/ForgeMission.Conversations.Contracts/ForgeMission.Conversations.Contracts.csproj` | Contracts0.13.0 |
| `src/ForgeMission.Conversations.Contracts/README.md` | Server-only vocabulary and fixed invariants |
| `src/ForgeMission.ConversationHost/Grains/MissionProducedFilesAdmission.cs` (new) | Pure exact-set comparison against canonical binary Artifact facts, retaining event Attempt as well as descriptor/path |
| `src/ForgeMission.ConversationHost/Grains/ConversationGrain.cs` | Correlation/duplicate guards before reads; canonical admission inside existing conditional pause decision; forward reference through existing result command |
| `src/ForgeMission.ConversationHost/Grains/ConversationGrainResults.cs` | Host-only `IntakeFailureReason` ID2, request manifest-reference JSON ID2, trailing `InvalidMissionPause` outcome |
| `src/ForgeMission.ConversationHost/Grains/ConversationTransition.cs` | `HandsRequested` manifest-reference JSON ID2 |
| `src/ForgeMission.ConversationHost/Grains/ConversationState.cs` | Manifest-reference JSON ID38; set/clear alongside opaque continuation |
| `src/ForgeMission.ConversationHost/Messaging/ConversationProgressHandler.cs` | Existing intake with32KiB bound; correlation-aware pause failure; typed failure recording; use shared output grammar |
| `src/ForgeMission.ConversationHost/README.md` | Canonical admission/reference lifetime/failure semantics |
| `tests/ForgeMission.ConversationHost.Tests/MissionProducedFilesContractsTests.cs` (new) | JSON defaults, bounds, shared grammar |
| `tests/ForgeMission.ConversationHost.Tests/MissionProducedFilesAdmissionTests.cs` (new) | Exact canonical membership/mismatches |
| `tests/ForgeMission.ConversationHost.Tests/MissionProducedFilesStateTests.cs` (new) | State fold/codec/reference lifetime/clear transitions |
| `tests/ForgeMission.ConversationHost.Tests/MissionProducedFilesProgressHandlerTests.cs` (new) | Controlled adapter failure routing using existing DispatchProxy test pattern |
| `tests/ForgeMission.ConversationHost.Tests/MissionProducedFilesIntegrationTests.cs` (new) | Existing real Azurite fixture: chunks, pause/result/recovery/retries/negative paths |
| `tests/ForgeMission.ConversationHost.Tests/ConversationProgressHandlerTests.cs` | Filename/path bounds and dropped binary chunks |
| `tests/ForgeMission.ConversationHost.Tests/ConversationContractsRoundTripTests.cs` | Existing unique query-name inventory grows9→10 with the fixed binary query name |
| `.github/workflows/publish-conversations-packages.yml` | Normal0.13.0 release references and focused integration filter |
| `eng/verify-conversations-packages.sh` | Contracts0.13.0 audit; Presentation unchanged0.8.0 |

### Reuse

| Need | Existing equivalent / decision |
|---|---|
| Manifest tuple/parser | No equivalent shape; use exact parent record and generated JSON, BCL formatting and existing SHA/deterministic-ID helpers |
| Bounds | Reuse `MissionContentLimits.MaxContentBytes`; add only manifest-specific fixed bounds |
| Output grammar | Move current handler `OutputPathSlot`; no second validator |
| Canonical membership | Existing Artifact references and `eventStore.ReadAfterAsync`; no new persistent registry/list |
| Manifest read | Existing `IConversationBodyStore.ReadAsync`; keep side effects in named grain admission step |
| Pause/result lifetime | Existing `DecideMissionHandsRequest`, `HandsRequested`, conditional transition/state fold/outbox |
| Failure | Existing `ConversationRunFailureRecorder`; only the typed invalid-current-pause outcome invokes it |
| Tests | Existing DispatchProxy, AzuriteFixture, TestBodies, launch helpers and fake dispatcher; no production test abstraction |
| Release | Existing package workflow, immutable/private guards and audit script |

Current dependencies stay unchanged. Host already consumes published Core0.1.8; cached package
nuspec identifies forge-mcl commit `03dd4dbf114a4bca4d25597c858008e393d716b8`. Authenticated
package metadata lists Contracts0.1.0–0.12.0;0.13.0 is unused. Recheck before publication.

### Sequence

1. After supervisor approval, create `adeen/phase-76-output-continuations-producer` from clean
   current owning-repo main.
2. Add inert Contracts vocabulary/generated JSON/shared grammar, including the fixed query name
   needed by the later normal API MapQuery projection. Validate JSON32KiB,32 unique
   path/content identities,100MiB aggregate, descriptor/output-kind/hash/type, UTF-8 bounds,
   step hash, canonical positive attempt and run-derived content identity.
3. Compare exact canonical set in Host, including canonical event Attempt; ignore non-binary artifacts. Null/empty manifest passes
   only with no binary output facts.
4. In the existing conditional pause decision, reject wrong run/conversation/command/launch/request
   and outstanding/late/duplicate requests before reading manifest/artifact data. Strengthen the
   existing outstanding-request guard from ID-only matching to exact stored-request and both
   reference equality. Exact accepted retries preserve references; changed/missing references
   reject without failing the active run. No second receipt mechanism.
5. Current correlated new-pause intake failure or invalid canonical set returns `InvalidMissionPause`
   without adopting the pause. Valid pause stores both references atomically. Result copies both
   into the existing deterministic continuation before clearing both. Preserve all Orleans IDs.
6. Handler body conflicts remain ordinary rejection. Other Hands pause intake errors carry the
   original pause and Host-only reason through grain correlation; only typed invalid outcome fails
   the run. Non-pause behaviour unchanged; infrastructure exceptions propagate.
7. Prove focused managed behaviour locally before product PR/full CI. Then real-storage focused
   and full managed suites in normal CI, sequential code reviews and supervisor readiness.
8. Merge/publish normally; independently inspect actual remote package before consumer work.

### Verification

Observed baseline: .NET10.0.401; existing `MissionContentContractsTests`16/16 passed,0 skipped,
using Release `dotnet test ... --no-restore --filter FullyQualifiedName~MissionContentContractsTests`.

| Layer | Required observation |
|---|---|
| Local Contracts | Record/array/pause/command JSON roundtrip, null defaults, no public reference leakage; exact bounds including zero bytes; malformed IDs/hash/path/attempt/UTF-8/size/count rejected |
| Local pure admission | Reordered exact set accepted; omitted/invented/duplicate/foreign/changed descriptors rejected |
| Local state | Codec retains references only; old state null; request/result/cancel/interruption transitions maintain both-reference lifetime |
| Local controlled handler | Typed invalid fails; ordinary rejection does not; original malformed/incomplete pause + reason preserved; conflict rejects; storage/grain outages propagate |
| CI real Azurite | Stage chunks/artifacts; adopt full manifest; restart Host; claim/result dispatch identical reference with deterministic ID; exact retries no duplicates; missing/bad/oversized/dropped/mismatched content cannot pause; current bad intake records one Error+Failed, stale/duplicate/wrong-correlated bad pause leaves run unchanged; durable bytes/count limits remain; outages redeliver |
| Package | Normal pack/audit Contracts0.13.0 plus unchanged Presentation0.8.0; repo commit/dependency-free nuspec/DLL/README/license/symbols; remote unique private associated version and actual DLL generated APIs |
| Default cloud | Pending Runner/API/infra delivery; ordinary saved login/default endpoint/disposable Project produce→pause→fresh-worker read plus post-completion binary query |

Local commands: restore solution with `nuget.config`, Release build with `--no-restore`, then
Host test project with `--no-build --no-restore` and filter covering the four new non-Azurite
classes plus existing MissionContentContracts/ConversationContractsBoundary/ConversationBodyContracts/ConversationContractsRoundTrip
classes. Full unfiltered `dotnet test ForgeMission.Conversations.slnx -c Release --no-build
--no-restore` runs through normal CI's existing Azurite environment. Zero warnings required.
Native AOT remains the final whole-phase gate by operator instruction; UI gates N/A.

### Principles that changed decisions

| Implementer rule | Choice |
|---|---|
|1 No NIH;2 No duplicate paths|Shared grammar and existing pause/claim-check/failure/outbox lifecycle|
|3 Minimum needed;4 No speculative abstraction|Producer-only scope and one pure canonical-admission seam|
|5 Stay in scope|Raised and returned intake-correlation gap to supervisor|
|6 Verified means done|Separate local controlled, real storage, package and still-open cloud evidence|
|10 Explicit errors;12 Separate side effects|Typed failure versus propagated infrastructure outage; named grain reads, pure comparison|
|14 Extract for real reason|Shared grammar owner and testable membership boundary|

Open questions: none after supervisor's recorded correlation correction. No executable edit
approval is implied by this plan.

Supervisor source check: the published DTO/generated JSON already exist, but
`ConversationQueryNames` currently lacks `GetMissionContent`. Add its inert name here so later
API consumes the published vocabulary without a duplicated literal or a second release solely
for that name. Implementer r2 explicitly adopts this complete artifact and the exact replay
guard correction; full current reviews passed and supervisor approval is recorded above.

## Timing

| Stage | Start UTC | End UTC | Evidence |
|---|---|---|---|
| Plan / implementer / r1 |2026-10-11 12:19:12|2026-10-11 12:22:55|Read-only source/package investigation and complete producer plan|
| Review plan / simplicity / r1 |2026-10-11 12:27:54|2026-10-11 12:28:19|Full11-check PASS before query-name correction|
| Review plan / ownership / r1 |2026-10-11 12:29:04|2026-10-11 12:29:37|Full11-behaviour PASS including persisted query name; r2 acknowledgment then pending|
| Plan / implementer / r2 |2026-10-11 12:30:10|2026-10-11 12:30:15|Complete persisted artifact adopted, exact replay/query tests explicit|
| Review plan / simplicity / r2 |2026-10-11 12:30:48|2026-10-11 12:30:57|Full11-check PASS on complete current plan|
| Review plan / ownership / r2 |2026-10-11 12:31:29|2026-10-11 12:31:44|Full12-behaviour PASS including canonical event Attempt|
