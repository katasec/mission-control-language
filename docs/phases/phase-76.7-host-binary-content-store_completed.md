# Phase 76.7 — Host binary content storage evidence

The bounded storage primitive is merged and verified; [product PR21](https://github.com/katasec/forge-conversations/pull/21),
[closure docs PR382](https://github.com/katasec/mission-control-language/pull/382).
Current design: [Host binary storage](phase-76.7-host-binary-content-store.md). This closes only
unused internal binary operations; admission, canonical references, expiry and user file support
remain future integration work. AOT stays at final whole-phase delivery.

## Design review

Simplicity r1 independently inspected actual Host adapter, Orleans registration, Contracts and
tests; own observation05:51:20–05:52:45UTC. Reviewed artifact SHA256
`f06a9c1c0787a5b4e5fc13015777397c5c8fe73d63a293c0519f402f9de57ab6`.
REVISE: remove duplicate outcome enum; make matching repeated commit succeed; require the empty
chunk receipt and test the missing-receipt case. All three corrected by supervisor05:53:25–05:53:27UTC.
No finding dismissed. Existing adapter/SDK use and bounded no-runtime default classification
were supported by actual source checks. Complete current simplicity r2 PASS (own05:53:55–05:54:13);
complete ownership r1 PASS (own05:54:58–05:55:36). Reviewed pre-lock SHA256
`afb1d65e27ef41460c5a4be042c8fa9dea71a2deed495099c0584682436adefd`.
Supervisor independently checked existing registrations/adapter/Contracts/normal PR workflow,
accepted the current design and locked it05:56:22UTC; plan approval still required.

| Simplicity check | Current r2 verdict/evidence |
|---|---|
| New apps/libraries | PASS: existing adapter/container/SDK only |
| Reuse | PASS: existing outcome, exception, JSON, chunk/hash and block helpers |
| Multiple paths | PASS: one Blob seam, distinct binary/text semantics |
| Legacy | PASS: no fallback/migration reader |
| Knobs | PASS: existing fixed limits/SDK conditions |
| Abstractions | PASS: small immutable receipts address actual overwritable block conflict |
| Libraries | PASS: SDK reused; Orleans Blob grain-state provider is a different responsibility |
| Copy-paste | PASS: shared block mechanics required |
| Redundant definitions | PASS: duplicate enum removed |
| Size | PASS: unused persistence only; no public routes/sweep/deployment |
| Tests | PASS: proportionate integrity boundaries/retries/empty receipt; text tests retained |

Ownership derived blind from atlas and READMEs before proposal. All16 behavior rows PASS:
stage, assembly, verified read, existing outcome, derived keys, conflicting retries, reused
chunk/hash, empty/repeated commit, owner/slot identity, pre-I/O validation, conditional candidate
cleanup, bounded reads, propagated failures, future authority/adoption, future expiry and existing
verification owner. Actual Host Program.cs44/82 and README19 establish one persistence owner.
Security/engineering/failure/API/default gates PASS; no competing implementation or second job.
No finding dismissed; managed evidence remains outstanding. No deployed file-support claim.

## Plan reviews

Reviewed full pre-approval plan SHA256
`c2d877364ef9ba6d4b359dc4b44eb8453e19de8c27ac54020df3a1add53e0f8c`.
Simplicity own06:01:11–06:02:35UTC: all11 current checks PASS, including justified nested SDK
test-only policy and malformed-receipt/bounded-metadata clarification. Ownership derived owners
blind; own06:03:10–06:03:23: all17 behavior rows plus security/engineering/failure/compatibility/
default gates PASS. Existing one adapter/SDK path, no new production type/provider/dependency,
no authority/adoption claim. Supervisor independently checked five-file fit, actual existing
block/commit helpers, SDK12.29.1 APIs, test-filter no-container scope, normal full PR workflow and
operator AOT timing. No finding dismissed or approval inherited. PLAN APPROVED06:04:00UTC.

## Timing

All times UTC2026-10-10. Missing boundaries are not reconstructed. Tokens N/A.

| Stage / role / round | Start | End | Evidence |
|---|---|---|---|
| `[scope:supervisor] Host binary storage` | 05:38:26 | 05:38:26 | Bounded dependency selected after prior task closure |
| `[design:supervisor:r1] Host binary storage` | 05:38:26 | Unavailable; result existed by05:49:05 | Actual existing adapter, grain provider and fixture inspected; design artifact |
| `[review-design:simplicity:r1] Host binary storage` | Unavailable; exact dispatch not recorded | Result received by05:53:25 | Own observations05:51:20–05:52:45; full11-check verdict |
| `[design:supervisor:r2] Host binary storage` | 05:53:25 | 05:53:27 | Three corrections above, Markdown validationPASS |
| `[review-design:simplicity:r2] Host binary storage` | 05:53:27 | Result received by05:54:27 | Complete current design PASS; own05:53:55–05:54:13 |
| `[review-design:ownership:r1] Host binary storage` | 05:54:27 | Result received by05:56:22 | Complete current design PASS; own05:54:58–05:55:36 |
| `[approve-design:supervisor] Host binary storage` | 05:56:22 | 05:56:22 | DESIGN LOCKED; no product edit approval |
| `[plan:implementer:r1] Host binary storage` | 05:56:27 | Result received by06:00:11 | Full five-file plan; own05:56:54–05:59:08 |
| `[review-plan:simplicity:r1] Host binary storage` | 06:00:49 | Result received by06:02:48 | Full current plan/design clarification PASS; own06:01:11–06:02:35 |
| `[review-plan:ownership:r1] Host binary storage` | 06:02:48 | Result received by06:04:00 | Full current plan/design clarification PASS; own06:03:10–06:03:23 |
| `[approve-plan:supervisor] Host binary storage` | 06:04:00 | 06:04:00 | Explicit bounded PLAN APPROVED |
| `[implement:implementer:r1] Host binary storage` | 06:04:12 | 06:10:22 | Frozen five-file handback; own06:04:42–06:10:02 |
| `[review-code:simplicity:r1] Host binary storage` | 06:10:22 | Received by06:13:00 | Full simplicity/style PASS; own06:10:58–06:11:48 |
| `[test:supervisor] Local binary functional checks` | 06:12:54.929 | 06:12:59.926 | Actual TRX25PASS/0fail/0skip |
| `[test:supervisor] Local text regression` | 06:13:11.868 | 06:13:15.589 | Actual TRX7PASS/0fail/0skip |
| `[review-code:ownership:r1] Host binary storage` | 06:13:11 | Received by06:14:58 | Full current source PASS; own06:13:39–06:14:22 |
| `[ready:supervisor] Source/raw CI/review gates` | 06:15:00 | 06:15:00 | All current checks and exact source tree PASS |
| `[merge:supervisor] Product PR21` | 06:15:00 | 06:15:13 | Normal reviewed merge |
| `[accept:supervisor] Primitive source identity/clean main` | 06:15:16 | 06:15:23 | Merged tree equals tested/reviewed tree; no runtime path exists |
| `[validate:supervisor] Closure documentation` | 06:17:07 | 06:17:16 |20docs/130local links/2JSON/fences/globalhub/diffPASS; final minor heading checked before commit |

End-to-end scope→last product merge **36m47s**. Product PR opened06:09:20, merged06:15:13
(**5m53s**). Documentation closure is outside product timing. Missing boundaries above remain
honestly labelled; tokens N/A.

## Current code reviews

Frozen/reviewed source `c2b69624e7c28b4f70a1286087771c31f24a8f82`, baseline
`d4d013e577e4e57f3eaf3310527094b5d2dfa851`; five paths627+/29−, no new production type/file or
dependency. Full diff SHA256 `b4156c47f760c970c1d3595a84832a31c37f4994f9dd6e9475b0017cb6b4991c`.
Simplicity/style personally read final source/raw stable logs; ownership independently derived
owners and recomputed full diff hash. No finding dismissed; all current source verdicts PASS.

| Simplicity check | Verdict / observation |
|---|---|
| New apps/libraries | PASS: existing adapter/SDK, no added production type/dependency |
| Reuse | PASS: same block staging/IDs/sizes/commit, outcome/exception |
| Multiple paths | PASS: bounded binary byte semantics, shared assembly |
| Legacy | PASS: active text behavior retained, no fallback |
| Knobs | PASS: existing fixed limits/derived keys |
| Abstraction | PASS: private tuple/coherent SDK boundaries only |
| Library | PASS: Azure streaming/conditionals/ETags; BCL media parser |
| Copy-paste | PASS: shared block mechanics; test policy has required SDK sync/async entries |
| Redundant definitions | PASS: existing JSON/outcome/chunks/hash |
| Size | PASS: five required paths; about180net adapter lines |
| Tests | PASS:409lines cover meaningful integrity/concurrency/cancel/cleanup, not mirrored DTOs |

| Style check | Verdict / observation |
|---|---|
| Outline | PASS: adapter summary/public flows reveal intent |
| Functions | PASS: new production functions<30lines, coherent tests<40 |
| Order | PASS: public entries before helpers |
| Errors | PASS: expected catches only; storage/cancel/staleETag failures propagate |
| Nesting | PASS: maximum2control levels |
| Sideeffects | PASS: named upload/download/commit/cleanup steps |
| Warnings | PASS: final actual Debug/Release0warning/error; AOT deferred by operator |
| Extraction | PASS: shared block mechanics/bounded SDK failure steps |
| Complexity | PASS: manual McCabe validation12, commit8, stage6, receipts6, blocks6, bounded read5, remaining≤7; no artificial extraction |

Ownership full17behavior rows PASS: internal methods/outcome, validation, scoped keys, immutable
metadata, conflicting first writers, shared mechanics, exact empty receipt, owner/slot, racing
commit, own-ETag cleanup, bounded reads, verified bytes, propagated failures, unchanged text,
future authority/lifecycle and test-only SDK policy. Host README19, Program44 and82 establish
existing ownership. Security/engineering/compatibility/failure/default classification PASS.
The review left full CI as a separate supervisor gate; root checked actual PASS below.

## Functional verification and operator correction

Operator: **"before CI full CI - you need to check if the code works"**, **"Also leave AOT last"**.
Original plan excluded local storage tests and used normal PR CI for Azurite. That order was wrong:
compilation and unrelated focused checks did not prove the new storage operations. PR CI had
already started06:09:27 when operator clarified. Root personally ran existing local managed
binary/text fixture tests, without source changes, before accepting full CI. Future increments
run applicable focused functional checks **before** triggering full CI. No AOT/native consumer
or ad-hoc Docker command was run; existing Testcontainers fixture owns temporary Azurite startup
and teardown. Source scope remains the approved five files.

| Check | Actual observation |
|---|---|
| Debug build | Final `dotnet build ... -c Debug --no-restore -warnaserror` exit0/0warnings/errors |
| Release build | Same Release exit0/0warnings/errors |
| Pure focused | Planned contract/SSE filter26PASS/0fail/0skip |
| Binary functional | Root Release `--filter FullyQualifiedName~MissionContentStoreTests`:25PASS/0fail/0skip,4.9901s; actual TRX06:12:54.929–06:12:59.926 |
| Text regression | Root Release `--filter FullyQualifiedName~ConversationBodyStoreTests`:7PASS/0fail/0skip,3.7118s; actual TRX06:13:11.868–06:13:15.589 |
| Full normal CI | Run38029952694/job114148603817 SUCCESS06:13:45; unfiltered299PASS/0fail/0skip,3m4s;0compilerwarnings/errors; existing two-package/source audit stepPASS |
| No publisher | Normal PR skips publisher by its existing event condition; no package version/publish/deploy change |

Raw logs/evidence `/private/tmp/phase76-host-storage-20261010T060442Z/`:
`EVIDENCE.md`, `full.diff`, `build-debug-stable.log`, `build-release-stable.log`,
`focused-debug-stable.log`, `local-storage-focused.log`, `local-text-storage-focused.log`,
`local-storage-results/storage-focused.trx`, `local-text-storage-results/text-storage-focused.trx`.
Root raw CI `/private/tmp/phase76-host-storage-pr-ci.log`. Early development compiler failures
were missing required existing Azure listing arguments, retained in raw evidence and fixed;
stable source checks above supersede them. No dependency/design change or warning waiver.

Verified boundaries include empty/nonUTF8/>4MiB exact roundtrip/reverse/repeat/fresh adapter;
tenant+conversation isolation; conflicting descriptor/chunk and concurrent first writers;
durable receipt surviving failed PUT; missing/wrong-size blocks; commit winner consuming blocks;
wrong whole hash and conditional cleanup preserving a replacement; missing/malformed/oversized
stored bytes; invalid cap/hash/media/owner/chunk before mutation; cancellation and exact retry.
Old text suite unchanged. Root independently read all five changed files and checked scope.

## Merge and bounded acceptance

PR21 merged06:15:13 as `ae1135a7a8e45e4d7eb1c0ebabc77c432a57022d`.
CI synthetic merge `f6177d7a4ed12b0c3222804d6f55065582727954` has parents baseline+reviewed head.
Reviewed head, synthetic merge and actual merged main all have tree
`8ae41758499fc303374db6440e82c90f793d8dec`. Root observed clean current product main0/0,
sole main worktree06:15:23; merged task branch removed06:15:52. No source corrections followed
the passing managed checks, so no repeated tests were needed.

Default-path applicability: unused internal primitive has no active user/runtime route; governing
evidence-layer exception permits contract-boundary closure. Real Azurite proves storage only.
No deployed file feature, canonical adoption, expiry, Runner consumption or whole-phase completion
is claimed. No package/image publication/deployment/native gate belongs to this increment.
Next: Host admission, canonical references and lifecycle from the locked parent contracts.

## Approved implementation plan

Historical approved plan and verification-order correction; implementation is delivered above.

### Phase 76.7 — Implementer plan

**Status:** PLAN APPROVED2026-10-10 06:04:00UTC after complete current simplicity/ownership plan PASS
and supervisor source/scope/security/failure/default checks. Implement only this five-file plan.
Verification-order correction06:12UTC: operator requires local functional storage checks before
full CI and repeats AOT-last. This supersedes the original plan's local-storage exclusion;
source scope/design remain unchanged. PR CI had already started, so root ran existing local
binary/text storage tests before accepting its result. Future increments run them before PR CI.
Implementer observations2026-10-10 05:56:54–05:59:08UTC; supervisor received by06:00:11UTC.
Design: [Host binary storage](phase-76.7-host-binary-content-store.md).
Baseline forge-conversations clean main`d4d013e577e4e57f3eaf3310527094b5d2dfa851`.
After approval create `adeen/phase-76-host-binary-content-store` before editing.

#### Files

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

#### Reuse

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

#### Sequence

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
11. Local managed builds and focused functional binary/text checks through the existing Azurite
    fixture before full CI (latest operator correction). Commit/push stable
    approved scope; create and attach draft PR so normal existing Ubuntu verification runs full suite.
12. Hand back frozen source/diff/raw observations; supervisor owns full sequential code reviews,
    normal CI check, merge/acceptance/docs closure. Implementer never marks task complete.

#### Verification

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

Local functional commands (existing managed fixture, no custom Docker/native route):

```powershell
dotnet test tests/ForgeMission.ConversationHost.Tests/ForgeMission.ConversationHost.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~MissionContentStoreTests"
dotnet test tests/ForgeMission.ConversationHost.Tests/ForgeMission.ConversationHost.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~ConversationBodyStoreTests"
```

Normal existing PR workflow
`.github/workflows/publish-conversations-packages.yml` restores, builds Release, and runs
**unfiltered** solution tests with real existing Azurite fixture, then existing package audit.
Require zero compiler warnings/skips/failures, inspect raw logs/counts/source SHA. No workflow
changes or publisher invocation (Contracts0.9.0 already published).
No active runtime/user path: storage-layer evidence only, no deployed file/adoption claim.
UI/deploy N/A; operator reserves AOT for final whole-phase delivery after functional flow works.
All three design Donewhen conditions still require independent reviews/root CI/normal merges/cleanmain.

#### Principles that changed decisions

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

#### Open questions

None. Supervisor confirmed malformed commit receipt→Incomplete; differing descriptor→Conflict.
Future callers own authority, admission, adoption and expiry serialization; primitive does not.
