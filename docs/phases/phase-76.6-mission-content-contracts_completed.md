# Phase 76.6 — Content contract review and delivery evidence

Contracts0.9.0 product delivery accepted through the actual observations below. Closure record:
[docs PR381](https://github.com/katasec/mission-control-language/pull/381). Full Phase76 remains open;
native verification is reserved for final delivery and the publisher metadata discrepancy is explicit.

## Design reviews

Both reviewers independently inspected the complete current design and actual existing owner,
JSON and publication files. Reviewed pre-lock document SHA256
`c0f1c284422458d38bb12afe7a04475b87f7dc97a70986b68bc7e0e2f43f44b2`.
Simplicity observed04:56:25–04:57:40UTC; ownership04:58:18–04:59:41UTC.
Root independently checked all concrete types, unchanged active routes/current public ABI,
normal version availability, publisher coupling and named security/default boundaries.
No finding dismissed. DESIGN LOCKED05:00:09UTC; no executable edit authorized.

| Simplicity check | Current verdict/evidence |
|---|---|
| New apps/libraries | PASS: existing dependency-free Contracts |
| Reuse | PASS: generated JSON, body chunk size/Body IDs, normal publisher |
| Multiple paths | PASS: inert values introduce no execution/route/admission alternative |
| Legacy | PASS: existing shapes remain intact; no reader/fallback |
| Knobs | PASS: four fixed parent bounds |
| Abstractions | PASS: concrete values; validation/storage left to Host |
| Library choice | PASS: existing STJ/source generation; native probe required |
| Copy-paste | PASS: existing workflow and verifier |
| Redundant definitions | PASS: binary reference differs from UTF-8 body; chunk/ID helpers reused |
| Size | PASS: content values/JSON/Contracts0.9.0 only; unchanged Presentation audited |
| Test volume | PASS: actual wire/negative/ABI/native observations only |

Owner derivation preceded proposed placement, using Contracts README15–31 and Host README19.
Actual Contracts JSON context9, body limits19/25, Body identity123, publisher45 and verifier13
support the reuse decisions. Named Forge repository searches found no proposed content types.

| Behavior | Independently derived owner / proposed placement | Verdict |
|---|---|---|
| Input/output kind | Contracts / same | PASS |
| Binary content identity/length/hash/media/kind | Contracts / same | PASS |
| Named literal/artifact inputs | Contracts / same | PASS |
| Staged chunk request | Contracts / same | PASS |
| Conversation-scoped query request | Contracts / same | PASS |
| Shared fixed limits | Contracts / same | PASS |
| Existing chunk size | Contracts / existing body limits | PASS |
| Reflection-free generated JSON | Contracts / existing context | PASS |
| Preserve current APIs/ABI/ordinals/active registries | Contracts / unchanged | PASS |
| Authorize/validate/stage/adopt/persist | Host / explicitly deferred | PASS |
| Verify/publish immutable package | Existing repo engineering workflow / same | PASS |
| Fresh normal-feed/native producer acceptance | Contracts producer verification / same | PASS |

Technical design PASS: security/authority unchanged, no role/credential/store/entry point;
engineering owner/reuse/dependency PASS; additive ABI design PASS; default producer acceptance
legitimate. Actual Native AOT/publication evidence remains outstanding. UI N/A. Move nothing.

## Complete plan reviews and approval

Simplicity observed05:05:03–05:05:21UTC; ownership05:05:54–05:06:24UTC. Both rechecked the
complete current seven-path artifact, not only a change. Reviewed plan SHA256
`b7908913167f5ba06b6f8850421f699d036ba3b63ec16dd5fa8aa66945cd0434`.
Root ordinary-SDK-target clarification was present in both reviewed artifacts. No finding waived.

| Simplicity check | Current complete-plan verdict |
|---|---|
| New apps/libraries | PASS: seven existing-owner paths, no dependency/runtime |
| Reuse | PASS: existing context/enum annotations/chunk/IDs/release owners |
| Multiple paths | PASS: one format; controlled branch proof then real normal-feed acceptance |
| Legacy | PASS: existing DTO/ctor/ordinals/registries/consumer pins unchanged |
| Knobs | PASS: four fixed bounds; no preemptive native target override |
| Abstractions | PASS: plain values; one needed private verifier expected-version arg |
| Library choice | PASS: dependency-free existing STJ; ordinary-target native proof |
| Copy-paste | PASS: extend existing verifier/publisher |
| Redundancy | PASS: distinct binary semantics; text/chunk primitives preserved |
| Size | PASS: seven paths; audit unchanged Presentation without republishing |
| Tests | PASS: wire/compatibility/modified release-negative boundaries only |

Ownership derived from Contracts README15/Host README19 before placement. Current full table:

| Behavior | Derived owner / planned placement | Verdict |
|---|---|---|
| Input/output kinds | Contracts / same enum | PASS |
| Binary reference values | Contracts / same record | PASS |
| Named strings/artifact maps | Contracts / same record | PASS |
| Chunk request values | Contracts / same record | PASS |
| Scoped query values | Contracts / same record | PASS |
| Fixed bounds/reused chunk+IDs | Contracts / same existing conventions | PASS |
| Generated wire metadata | Contracts / same existing context | PASS |
| Byte/map/long/malformed JSON tests | Contracts verification / dedicated tests | PASS |
| Existing ABI/wire/registry/consumer pins | Existing owners / untouched | PASS |
| Authority/integrity/adoption/storage | Host / deferred, no implementation | PASS |
| Both package audits | Existing engineering verifier / private expected version | PASS |
| Immutable merged Contracts-only publication | Existing publisher / narrowed target | PASS |
| Public Native AOT proof | Producer verification / supervisor normal-feed acceptance | PASS |

Technical plan PASS: security/data/credentials unchanged; exact additive API/ABI/JSON;
seven paths/one owner/explicit JSON+audit+immutable+visibility errors; both dependency audits;
full Ubuntu/Azurite integration and ordinary-SDK native remain mandatory; normal-feed fresh
acceptance defined. UI/deployment N/A. No competing types found across named Forge repos.

Root PLAN APPROVED05:06:50UTC after independently inspecting complete artifact and those gates.
Original local Docker absence was recoverable: installed `/Applications/Docker.app` started by
normal `open -a Docker`, exit0 available05:06:23UTC; actual `docker info`05:06:34UTC reports
29.1.2/Docker Desktop on existing desktop-linux context. No settings/source change. Both local
full suites and normal CI remain required; no fixture skip or waiver. Same implementer assigned
05:07:08UTC; only approved seven paths, no merge/publication/acceptance authority.

## Implementation and current code reviews

Frozen product head`fa7acb0f6d06abec97b45302031eda440eca91fb`, tree
`b673926d6ffb1310f3e76e14d9a52f25a31aa573`; seven approved paths201+/21−.
Complete binary diff SHA256`3bbc1d03f9cb9760a6452c241ade36940983fa4b915b949888fdb79e93a7ac54`.
[Product PR20](https://github.com/katasec/forge-conversations/pull/20) opened05:14:43UTC.
Implementer own observed05:07:41–05:20:05UTC; assignment05:07:08UTC.

| Verification | Actual observation |
|---|---|
| Managed build | Debug and Release0warnings/0errors |
| Focused |103PASS/0skip/0fail |
| Full local, real Azurite | Debug274PASS/0skip/0fail2m27s; Release274PASS/0skip/0fail2m30s |
| Normal PR CI | [38026846346](https://github.com/katasec/forge-conversations/actions/runs/38026846346), job114139388424SUCCESS;274PASS/0skip/0fail2m56s;0compiler warnings/errors |
| CI source/artifact | Synthetic merge5aeeebae0d0e01c245a7c35491660cd53a5e4562, parents baseline+reviewedhead; artifact11660826717,430845bytes, zipSHA2ed2188c39bf4b175a165b76731564d1efa90d25e64b2c0faa43563749563169 |
| Controlled package | Contracts0.9.0 SHA256b0e2ca263c506cb9750b9055c09b7be3ddc1156f0eda4e56476b38612c25f138; restored bytes identical; exact reviewed-source nuspec/no dependencies |
| Controlled audit negatives | Missing package/symbol, wrong version/commit, extra Contracts dependency rejected |
| Controlled release negatives | Existing version, non404 API error, public/foreign/missing/duplicate package version, old tag, unmerged ref rejected; correct guards pass |
| Controlled managed consumer | All generated kinds/references/maps/chunks/query IDs, exact bytes/long/Unicode, malformed JSON and retained shapes PASS |

Root independently read all seven diffs, raw build/test/audit/consumer output and raw CI log.
Only five new JSON registrations and the locked inert vocabulary are added; existing contracts,
active17commands/9queries, pins and runtime files are byte-identical. Both packages are audited;
only Contracts0.9.0 is eligible for immutable private publication. Root rechecked0.9.0 absent05:22:42UTC.
Raw evidence directory`/private/tmp/phase76-content-contracts-20261010T050708Z` and root CI log
`/private/tmp/phase76-content-contracts-pr-ci.log`; GH observations above are the durable CI pointers.
Ruby PATH and GitHub Action Node deprecation notices are separate from compiler/trim/linker warnings.

Simplicity/style reviewer read the complete current frozen diff and existing counterparts,
observed05:21:39–05:22:44UTC. Full simplicity table:

| Check | Verdict/evidence |
|---|---|
| New apps/libraries | PASS: plain existing Contracts values; no dependency; values7–32 |
| Reuse | PASS: existing context/enum annotations/verifier; context111–115/verifier13 |
| Multiple paths | PASS: one serialization path; no routes/active registrations; values5–6/tests113–124 |
| Legacy | PASS: no fallback/reader; old request/body shapes retained; tests97–109 |
| Knobs | PASS: fixed parent bounds/no duplicated chunk setting; values27–32/tests123 |
| Abstractions | PASS: no validator/service framework; values13–25 |
| Library choice | PASS: existing generated STJ; context111–115 |
| Copy-paste | PASS: shared verify_package/two audits/one push; verifier52–53/workflow115 |
| Redundancy | PASS: binary length/media/kind semantics distinct; values13–15/tests101–109 |
| Size | PASS: seven exact approved paths; workflow97/115/122 |
| Test volume | PASS:133lines prove actual wire/malformed/retained boundaries; tests13–124 |

Full separate code-style table:

| Check | Verdict/evidence |
|---|---|
| Outline first | PASS: values5–6/tests6–7/release namedsteps72/92/117 |
| Small functions | PASS: new tests≤17lines; existing verifier38lines/one audit |
| Top-down | PASS: tests before helpers127–132; verifier calls52–53 |
| Explicit errors | PASS: JSON errors/audit/immutable/visibility exit nonzero; tests79–93/verifier22–48/workflow100–107/133–146 |
| Shallow nesting | Observation: baseline visibility step retains four control levels; workflow122–140; new C# shallow |
| Separate effects | PASS: inert values; release effects in existing owner; values13–32/workflow89–148 |
| Zero warnings | PASS managed raw builds/CI; operator explicitly reserves native for final phase delivery |
| Real extraction | PASS: needed private version arg/fixture reuse; verifier15/tests127–132 |
| Complexity | PASS manual classic McCabe: verifier9/visibility11 including short circuits, unchanged/new C#≤3; all<15 |

Supervisor disposition of nesting observation: independently compared current and baseline full
visibility step. Control structure is identical; only the package selection/version changes.
Its retry, private/repository/exact-version checks and final diagnostic failure remain explicit,
with all modified predicates exercised. No new nesting or weakened failure boundary. Dismiss
cleanup for this bounded vocabulary/release change because it would expand into unrelated workflow
restructuring without correcting a regression. No warning, integrity or functional failure waived.
Reviewer-reported stale current native-before-acceptance sentences were corrected throughout the
active spoke/plan. Historical reviewed commands/tables remain labelled historical.

Ownership reviewer independently derived owners from the atlas and baseline component READMEs
before inspecting the full diff. Own observed05:23:48–05:24:14UTC; supervisor dispatch05:23:10UTC.
Independently recomputed the exact frozen diff hash above. Full current table:

| Behavior | Derived owner / actual placement | Verdict |
|---|---|---|
| Stable kind ordinals/wire names | Contracts / MissionContentKind:7 | PASS |
| Binary identity/long/hash/media/kind | Contracts / MissionContentReference:13 | PASS |
| Named strings/artifacts | Contracts / MissionNamedInputs:17 | PASS |
| Chunk owner/slot/sequence/ref/bytes | Contracts / StageMissionContentChunkRequest:21 | PASS |
| Conversation-scoped query value | Contracts / GetMissionContentRequest:25 | PASS |
| Fixed bounds | Contracts / MissionContentLimits:27 | PASS |
| Generated wire metadata | Contracts / existing context:111 | PASS |
| Exact wire and malformed JSON | Contracts verification / tests:10/78 | PASS |
| Existing ABI/shapes/registries | Existing Contracts / unchanged plus tests:96 | PASS |
| Authority/integrity/adoption/storage | Host / excluded; Contracts README:27 | PASS |
| Version without new dependencies | Contracts / csproj:9 | PASS |
| Both package/provenance/symbol audits | Existing eng verifier / :13/52 | PASS |
| Verified merged-source publication | Existing publisher / workflow:59 | PASS |
| Immutable sole Contracts0.9.0 push | Existing publisher / workflow:92/115 | PASS |
| Private/repository/exact-version visibility | Existing publisher / workflow:117 | PASS |

| Technical gate | Current verdict |
|---|---|
| API/ABI/JSON | PASS exact additive shape; old constructors/ordinals/registries untouched; no replacement serializer/chunk helper |
| Security/data/credentials | PASS inert values; Host retains semantic validation; no route/storage/permission grant |
| Engineering/scope | PASS seven approved paths/one owner; consumer/Core/Presentation behavior untouched |
| Managed verification | PASS actual103/274/274/CI274; zero skips/failures/compiler warnings |
| Package/failure paths | PASS controlled exact-source audit/JSON/release negatives/reflection-disabled consumer |
| Normal publication/default | OPEN at review; supervisor must merge/publish/fresh normal-feed managed accept |
| Native | Operator defers to final full-phase delivery; historical runs not default acceptance |
| UI/deployment | N/A |

Fresh search across named Forge repos found no competing vocabulary/owner; UTF-8 body references
have different semantics. No owner acquires a second job. Move nothing. Supervisor independently
checked this result against scope/Done when and raw evidence. Ready-to-merge05:24:58UTC after
current required CI SUCCESS and verified synthetic-merge tree equals reviewed tree. No code correction.

## Operator correction: native verification timing

Original Mac ordinary-target native attempt failed because SSL was not on the linker path.
The supervisor's process-local existing Homebrew library path rerun linked but emitted five
deployment-target warnings: macOS12 target versus OpenSSL27/Brotli26 on actual macOS27.0.1.
Neither is a zero-warning PASS. The supervisor then chose an extra controlled native consumer
in official .NET10 AOT ARM64 image407a2711f25619956ffc5febc9245a1bf9105d73a2bb42876b512ffa87094637.
It executed with zero warnings and exact package bytes, but is controlled evidence only.

The operator corrected this: **"There wesa no erquest to use docker - what are you dong ?"**,
then **"AOT is at the end after everything works..."**, **"not for testing"**. Supervisor stopped
the extra Docker route and moved AOT to final phase delivery. No more Docker/native consumer
runs for this increment. The sole scratch container already exited0 with `--rm`; no daemon stop,
prune or unrelated-container removal. Product architecture/configuration/source unchanged.
Normal managed builds/tests, publication and fresh normal-feed managed consumer close this
increment; the final native delivery gate stays open for the full phase. This is an explicit
operator instruction about timing, not a linker-warning waiver or default acceptance claim.

## Normal publication and managed default observation

Product PR20 merged05:25:03UTC as`d4d013e577e4e57f3eaf3310527094b5d2dfa851`; actual merged tree
equals the reviewed/CI tree above. Supervisor dispatched the existing workflow from main05:25:23UTC;
[run38027450839](https://github.com/katasec/forge-conversations/actions/runs/38027450839) created05:25:25UTC.
Private-package/API observation05:30UTC: repository`katasec/forge-conversations`, visibility`private`,
Contracts0.9.0 version id1364807069 created05:29:56UTC. Verify job114141213613SUCCESS:
274PASS/0skip/0fail3m11s,0compiler warnings/errors, both source/package/symbol audits PASS,
artifact11661330787/431001bytes. The actual package push succeeded.

Publication job114141937179 failed05:35:11UTC **only at its final metadata check**:
`visibility=private repository=missing expected_repository=katasec/forge-conversations`,
`version_0.9.0_count=1`. Overall run is FAILURE, never recorded as green. Prior run36655211551
on2026-09-30 has the identical missing-repository observation for0.6.0; this is a retained
reporting discrepancy, not a content-contract regression established by the evidence.

Supervisor independently exercised the exact private/repository/exactly-one-version predicates
with normal authenticated GH credentials05:33:08UTC: private, repo1381261480/
katasec/forge-conversations, one0.9.0/id1364807069. The normal local NuGet credential also returned
the same repository/visibility/version05:35:53UTC. This establishes the actual association and
shows a credential-view difference; why the workflow view omits the repository is unresolved.
No claim of a particular missing permission. Raw publication log
`/private/tmp/phase76-content-contracts-publication.log` and prior failure log
`/private/tmp/phase76-conversations-prior-publish-failure.log` retain both observations.

Supervisor acceptance05:35:53UTC uses those actual identical criteria plus the normal published
consumer below. The active spoke records a Type2 operational supplement for0.9.0 only, with
reversal/removal condition. No product/security predicate, token permission or immutable version
rule is waived/changed. Do not republish or rerun the immutable publisher against0.9.0.
The next Contracts release must investigate the CI metadata view; there is no standing exception.

| Default fact | Named observation |
|---|---|
| Published artifact | Contracts0.9.0,184292bytes,SHA256`811a42aca121a3d8a3b335309ad037315d9d898061a2c556c2e0d6b74b5be54f` |
| Source/dependencies | Nuspec commit`d4d013e577e4e57f3eaf3310527094b5d2dfa851`, correct repo URL, no dependencies; README/license/net10 DLL present |
| Normal route | Actual restored `.nupkg.metadata` source`https://nuget.pkg.github.com/katasec/index.json`; unchanged repo nuget.config/normal PowerShell credentials |
| Safe starting state | Dedicated scratch consumer and NEW cache`/private/tmp/phase76-content-contracts-default/published-packages-20261010T0527`; no existing project/account/data mutation |
| Absent replacements | One PackageReferenceContracts0.9.0; no local feed/sibling/project/DLL reference/custom JSON context/native target override; reflection serialization disabled |
| Actual action | Normal restore, Release managed build with warnaserror, actual consumer run using all five generated types, exact enum/long/byte/map/query fields/limits/malformedJSON and retained shapes/registries |
| Result |05:30:17UTC all actions PASS;0warnings/0errors; consumer assembly9728bytesSHA256`57e39498f478d198a81c6e2f580693096878dfa8de324134f8111b7a3221cc2e` |
| Limits | Pure-library producer acceptance; no hosted route/storage behavior claimed. Native AOT remains final-phase-only under operator instruction. |

Root inspected actual restore/build/run/provenance output. Raw files under
`/private/tmp/phase76-content-contracts-default`: published-restore.log,published-build.log,
published-run.log,published-observation.json; consumer ProgramSHA256
`316ab45e66098cac0494bc70f588a998be4705bc3d06e29d4ac903ce8874416a`.

## Stage boundaries

All2026-10-10UTC. Agent activity times above are distinct from result-available boundaries.

| Stage / role / round | StartUTC | EndUTC | Wall | Evidence |
|---|---|---|---|---|
| scope/design:supervisor |04:54:53 |04:55:55 |1m02s | Full bounded design and checks |
| review-design:simplicity |04:55:55 |04:57:57 |2m02s | Current full11-check PASS |
| review-design:ownership |04:57:57 |05:00:09 |2m12s | Current full12-behavior/technical PASS |
| Supervisor DESIGN LOCKED |05:00:09 |05:00:09 |0s | Full design accepted, no product edits |
| plan:implementer |05:00:36 |05:03:39 |3m03s | Seven-path complete returned plan; observed05:01:11–05:02:31 |
| Plan transcription/default-target clarification:supervisor |05:03:39 |05:04:44 |1m05s | Complete artifact, ordinary SDK native target before any observed failure |
| review-plan:simplicity |05:04:44 |05:05:33 |49s | Full current11-check PASS |
| review-plan:ownership |05:05:33 |05:06:50 |1m17s | Full current13-behavior/technical PASS |
| Supervisor PLAN APPROVED |05:06:50 |05:06:50 |0s | Exact seven-path current plan |
| implement:implementer |05:07:08 |05:20:31 |13m23s | Same implementer; own observed05:07:41–05:20:05; result known by supervisor clock05:20:31 |
| review-code:simplicity/style | Dispatch timestamp unavailable |05:23:10 | Unavailable | Own observed05:21:39–05:22:44; full11/9 tables; result known before next dispatch |
| review-code:ownership |05:23:10 |05:24:46 |1m36s | Own observed05:23:48–05:24:14; full15behavior/technical PASS |
| Supervisor ready-to-merge |05:24:58 |05:24:58 |0s | Scope/diff/rawCI/tree evidence accepted; baseline nesting finding disposition above |
| Product merge |05:24:58 |05:25:03 |5s | PR20 reviewed head merged as d4d013e577e4e57f3eaf3310527094b5d2dfa851 |
| Normal publication/default acceptance |05:25:23 |05:35:53 |10m30s | Normal push/private source/fresh managed consumer PASS; workflow metadata-report failure and exact independent supplement above |
| Closure documentation validation | Unavailable; updates interleaved with acceptance |05:37:00 | Unavailable |17docs/122local links+anchors/2JSON/fences/globalhub/diff PASS; docs-only product gates N/A |

Product end-to-end scope04:54:53→last product merge05:25:03 =30m10s.
Product PR20 open05:14:43→merge05:25:03 =10m20s. Acceptance/docs closure are later, not part
of that product span. Dispatch time not sampled for simplicity code review is explicitly unavailable;
agent activity times are preserved rather than invented as assignment times. Tokens N/A.
