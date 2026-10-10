# Phase 76.7 — Implementer plan

**Status:** Plan r1 returned; not approved. No product edit authorized.
Implementer observations2026-10-10 05:56:54–05:59:08UTC; supervisor received by06:00:11UTC.
Design: [Host binary storage](phase-76.7-host-binary-content-store.md).
Baseline forge-conversations clean main`d4d013e577e4e57f3eaf3310527094b5d2dfa851`.
After approval create `adeen/phase-76-host-binary-content-store` before editing.

## Files

All paths under `/Users/ameerdeen/progs/forge-conversations`:

| File | Change |
|---|---|
| `src/ForgeMission.ConversationHost/Persistence/IConversationBodyStore.cs` | Three exact binary methods; clarify existing Committed semantics without changing ordinals/members |
| `src/ForgeMission.ConversationHost/Persistence/AzureBlobConversationBodyStore.cs` | Validation, immutable metadata, shared assembly, bounded reads and conditional candidate cleanup |
| `tests/ForgeMission.ConversationHost.Tests/MissionContentStoreTests.cs` (new) | Required meaningful binary observations through existing Azurite collection/adapter |
| `tests/ForgeMission.ConversationHost.Tests/ConversationSseWriterTests.cs` | Three explicit throwing NoBodyStore methods |
| `src/ForgeMission.ConversationHost/README.md` | Internal binary operation/keys/retry scope; no active file-feature claim |

No Contracts, project, dependency, workflow, publisher, version, configuration or other repo
edits. No new production class/interface/file. Exact signatures are the design's three methods;
reuse `BodyCommitOutcome` and `ConversationBodyUnavailableException`. Stage Committed means
durable chunk; commit Committed means complete verified payload; neither adopts or grants authority.

## Reuse

| Need / method | Actual existing equivalent / choice |
|---|---|
| Persistence | Extend existing seam/adapter; Orleans Table-backed CustomStorage journal is a different responsibility |
| Descriptor/limits | Existing generated MissionContentReference metadata/MissionContentLimits; no private JSON/context/knob |
| Binary validation | Text ConversationBodies.IsValid has UTF-8/4MiB semantics; one private ValidateContentReference validates binary fields and returns generated descriptor bytes; BCL MediaTypeHeaderValue.TryParse |
| Chunk/hash | Existing ChunkCount/ChunkLength/ChunkBytes/Sha256Hex after bounded long→int conversion |
| Block upload | Extract existing StageAsync upload body into shared private StageBlockAsync(BlockBlobClient,int,ReadOnlyMemory<byte>,CancellationToken); preserve empty behavior; binary stable snapshot hashes/uploads same bytes |
| Block IDs | Generalize existing ExpectedBlockIds to validated int byteLength; retain encoding and empty list |
| Block sizes | Generalize existing AllBlocksStagedAsync to int byteLength; retain SDK list lookup/exact chunk lengths |
| Create-only commit | Existing TryCommitCreateOnlyAsync returns Task<ETag?>: response ETag or null on existing409/412; text keeps same success/failure distinction |
| Immutable descriptor/receipts | Private EnsureImmutableAsync uses SDK UploadAsync IfNoneMatch=*; existing409/412 bounded-read exact compare. Existing block commit cannot store standalone small metadata |
| Bounded reads | Existing TryDownloadAsync text path remains unchanged. Private DownloadBoundedAsync uses SDK DownloadStreamingAsync/Details.ContentLength/exact token-aware reads/disposal; private named tuple(bool Exists,byte[]? Bytes) distinguishes missing/corrupt; no result framework |
| Receipts | Private HasContentReceiptsAsync bounded-reads each required receipt and checks64lowercase ASCIIhex; missing/malformed→Incomplete |
| Verify/cleanup | Existing hash helper; coherent private candidate verification/delete with own returned ETag IfMatch |
| Tests | Existing AzuriteFixture.BodyStore/connection/unique addresses; small nested test-only Azure SDK pipeline policy for fault/race interception, no product hook/framework |
| Implementations | Actual search: only production adapter and NoBodyStore implement interface |

Existing SDK12.29.1 has streaming content length, conditional upload and commit ETags; no added package.

## Sequence

1. Reconfirm design/baseline/five-file scope, create branch and unique scratch evidence directory;
   record actual command boundaries.
2. Add exact methods/enum documentation and explicit NoBodyStore methods.
3. Share block staging/IDs/size checks/commit ETag while preserving text decoding/read/failures.
4. Validate before I/O: nonempty ID; Input/Output; length0..100MiB before int cast;64lowercase
   hex SHA; nonempty parseable media type/no CRLF; reject media string too long to fit descriptor
   before serialization, then actual generated UTF-8≤32KiB; exact stage index/chunk length; commit
   nonempty owner/slot and deterministic Body(owner,"mission-content/"+slot).
5. Derive only `{escaped-tenant}/{conversationId:N}/contents/{contentId:N}/descriptor`,
   `receipts/{index:D6}`, `payload`; slot never enters paths.
6. Bounded reader inspects streaming ContentLength before allocating exactly expected bytes;
   read exact and check EOF;404→missing, wrong length/truncated/excess→corrupt; dispose all streams;
   propagate storage/network/cancellation. Descriptor expected size=canonical bounded JSON,
   receipt64bytes, payload validated reference length.
7. Stage: validate/snapshot→create-only descriptor→create-only receipt→shared upload. Exact existing
   metadata reused, differing→Conflict before block write. Empty persists descriptor/receipt0 and
   skips PutBlock.
8. Commit: validate owner/reference; missing descriptor→Incomplete, differing/corrupt→Conflict;
   missing/malformed receipts→Incomplete; exact existing payload size/hash→Committed, differing
   existing payload→Conflict without modification; missing/wrong-size blocks→Incomplete; create-only
   commit expected list; racing winner reverified. Newly wrong candidate conditionally deleted by
   own commit ETag→Incomplete; descriptor/receipts retained. Failed conditional delete propagates,
   never removes replacement or hides storage error.
9. Read: validate, compare immutable descriptor, bounded-read/hash payload; missing/corrupt→existing
   unavailable exception. Real verified empty returns empty array.
10. Add named tests/README; public flows above helpers, coherent side-effect boundaries/early returns;
    measure materially changed functions≤15 without artificial extraction.
11. Local managed builds/nonstorage focused tests only; no local containers. Commit/push stable
    approved scope; create and attach draft PR so normal existing Ubuntu verification runs full suite.
12. Hand back frozen source/diff/raw observations; supervisor owns full sequential code reviews,
    normal CI check, merge/acceptance/docs closure. Implementer never marks task complete.

## Verification

| Boundary | Named observation |
|---|---|
| Binary bytes | Empty, nonUTF8, >4MiB multi-chunk roundtrip/hash |
| Delivery | Reverse/exact repeats/repeated commit/fresh adapter |
| Empty staging | Missing descriptor and descriptor-without-receipt→Incomplete; real receipt permits repeat commit/read |
| Isolation | Same content ID in other tenant/conversation unavailable and original unchanged |
| Immutability | Changed descriptor/same-index bytes→Conflict before winning block/payload change |
| Concurrency | Conflicting first descriptors and same-descriptor different chunks: one winner/one Conflict, exact winning assembly |
| Receipt before block | Test-only SDK policy fails block PUT after metadata persists; failure propagates; fresh adapter rejects changed retry, accepts exact retry |
| Missing staging | Missing/wrong-size block→Incomplete and no false payload success |
| Whole hash | Wrong whole hash→Incomplete; own candidate removed, metadata retained, changed retry conflicts |
| Conditional cleanup | Test-only SDK policy replaces corrupt candidate immediately before DELETE; stale IfMatch fails visibly and replacement survives actual Azurite condition |
| Stored corruption | Missing/corrupt descriptor, missing/wrong-length/wrong-hash/empty-missing payload→unavailable; existing bad payload commit Conflict and untouched |
| Bounded reads | Oversized descriptor,65byte/malformed receipt, oversized payload rejected with defined outcomes, no binary DownloadContent allocation |
| Validation | Empty ID, undefined kind, negative/overcap/overflow length, bad hash/media/descriptor/owner/slot/ID/index/chunk throw before mutation; prefix listing proves no blobs |
| Cancellation | Precancel and controlled block cancellation propagate; prior metadata exact-retryable via fresh adapter |
| Text | Unchanged ConversationBodyStoreTests pass in full existing suite |

Local normal PowerShell:

```powershell
dotnet restore ForgeMission.Conversations.slnx --configfile nuget.config
dotnet build ForgeMission.Conversations.slnx -c Debug --no-restore -warnaserror
dotnet build ForgeMission.Conversations.slnx -c Release --no-restore -warnaserror
dotnet test tests/ForgeMission.ConversationHost.Tests/ForgeMission.ConversationHost.Tests.csproj -c Debug --no-build --no-restore --filter "FullyQualifiedName~MissionContentContractsTests|FullyQualifiedName~ConversationBodyContractsTests|FullyQualifiedName~ConversationSseWriterTests"
git diff --check
```

No local storage tests that start Testcontainers. Normal existing PR workflow
`.github/workflows/publish-conversations-packages.yml` restores, builds Release, and runs
**unfiltered** solution tests with real existing Azurite fixture, then existing package audit.
Require zero compiler warnings/skips/failures, inspect raw logs/counts/source SHA. No workflow
changes or publisher invocation (Contracts0.9.0 already published).
No active runtime/user path: storage-layer evidence only, no deployed file/adoption claim.
UI/deploy N/A; operator reserves AOT for final whole-phase delivery after functional flow works.
All three design Donewhen conditions still require independent reviews/root CI/normal merges/cleanmain.

## Principles that changed decisions

| Implementer rule | Concrete choice |
|---|---|
| 1 No NIH | Existing store/generated JSON/chunk/hash/SDK |
| 2 No duplicate paths | Shared block upload/IDs/sizes/create-only commit |
| 3 Minimum | Fivefiles, no routes/adoption/sweep/package publication |
| 4 No abstraction | Existing adapter/private tuple, no new result framework |
| 5 Scope | Supervisor clarified malformed-receipt outcome before implementation |
| 6 Verified | Real normal Azurite CI, separate deployed-feature evidence |
| 7 Outline | Public validate→stage→assemble/verify flow |
| 8 Small functions | Coherent validation/boundedI/O/cleanup steps |
| 9 Order | Public entries before helpers |
| 10 Errors | Storage/cancel/staleETag failure propagates |
| 11 Nesting | Early missing/conflict/invalid returns |
| 12 Sideeffects | Named SDK upload/download/commit/delete boundaries |
| 13 Warnings | Local warning-as-error builds/raw CI0warnings; AOT deferred |
| 14 Extraction | Shared actual block mechanics/real boundedI/O only |
| 15 Complexity | Measure≤15; no score-driven fragmentation |

## Open questions

None. Supervisor confirmed malformed commit receipt→Incomplete; differing descriptor→Conflict.
Future callers own authority, admission, adoption and expiry serialization; primitive does not.
