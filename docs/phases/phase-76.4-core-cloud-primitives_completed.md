# Phase 76.4 — review evidence and stage timing

**Task remains open.** This file records finished investigations/reviews, not product completion.
[Active task](phase-76.4-core-cloud-primitives.md) · [Current plan](phase-76.4-core-cloud-primitives-plan.md).
Original plan and complete r7 correction plan approved after fresh full independent reviews.
Correction implementation/publication/default acceptance remain open.

Latest bounded r7 approval delivered through [MCL PR377](https://github.com/katasec/mission-control-language/pull/377),
merged2026-10-09T23:28:43Z atff88ce361217e3240cf82ab6a0924a2f09aef655. Documentation validation
passed11 docs/77 links/2 JSON examples/fences/global index and git diff --check; GitHub reported
mergeable/CLEAN with no required checks. Product tests/AOT/default acceptance N/A for this
documentation delivery; product gates remain mandatory. The same implementer is completing
current-source verification; final code-review/native/publication/default gates remain open.

Complete correction-plan approval delivered through [MCL PR376](https://github.com/katasec/mission-control-language/pull/376),
merged2026-10-09T23:15:01Z atb79252de6d9c412f3fa82f1c96a8e0a6bd35dbd0. Named documentation
checks passed:11 Phase76/hub files,77 local links/anchors,2 JSON examples, balanced fences,
global top-level phase index and git diff --check. No required GitHub checks were present;
mergeable/CLEAN and manual validation were observed. Product tests/AOT/default acceptance
are N/A for that documentation PR; the approved product plan retains them as mandatory gates.

Correction implementation assigned to the same implementer at2026-10-09 23:15:09 UTC, beginning
from clean770778d on the existing unfinished product branch/draftPR77. Stage ended at23:23:29 UTC
with an uncommitted intermediate diff, no merge/publication or acceptance approval.

## Correction implementation — intermediate handback

Evidence `/private/tmp/phase76-core-corrections-20261009T231509Z`: complete-intermediate.diff and
status-intermediate.txt freeze the handback. Full Debug solution build passed with zero warnings
and errors (managed-build-r2.log); parser/checkpoint/error-policy slice passed41/41 with no skips
(independent-focused.log); existing CLI scripts passed30/30 (script-tests.log). Later command
lookup/classifier test additions were not included in that full build. The exec-inclusive slice
had75 pass/11 fail and the managed native probe failed at the same visible macOS EPERM boundary.
No final full/package/native/default-path PASS is inferred from these intermediate results.

The implementer stopped the material deviation before adding a workaround. Supervisor inspected
Apple XNU kern_sig.c: group signalling filters zombie members, then POSIX nfound==0 returns EPERM.
proc_info.c's PROC_PGRP_ONLY list holds the process-list lock and includes zombies. Scratch
`/private/tmp/phase76-macos-held-group.py` observed an exited unreaped root with kill error1 and
a four-byte root-only group result; a root with an ordinary descendant had kill success and
root-only membership after two bounded observations. This is disposable mechanism evidence,
not corrected product or Native AOT acceptance. The complete round7 plan records the minimal
one-import, retained-root-only observation; all42 files/public contracts remain unchanged.
Supervisor clarification boundaries:2026-10-09 23:23:18–23:23:38 UTC. New full sequential plan
reviews are required before that clarification is implemented.

Current r7 implementation verification subsequently observed by supervisor: full Debug
build0warnings/errors (full-build-final.log); unfiltered Debug1062PASS/6 existing live skips/0fail,
2m19s with env -u MCL_API_KEY and blame-hang90s (full-debug-r3b.log); Release package406PASS/0skip
and metadata PASS (release-package-final.log). Managed lifecycle probe passed closed stdin,
literal argv/cwd/PATH, already-exited descendants, early-root timeout/caller cancel and unrelated
child ownership (lifecycle-managed-final.log). Source is still uncommitted at this observation;
final commit/package identity, native CI and acceptance remain open. full-debug-r3.log preserves
the stopped first attempt with the inherited key; the unchanged missing-key test failed there.
No test/product change or exclusion supplied the successful full rerun.

## Corrected frozen source — code review/native pending

Same implementer r3 ended2026-10-09 23:38:49 UTC, frozen/pushed product source
`dc03b7b9f295bf923436eaec5f645771142e061a`, [draftPR77](https://github.com/katasec/forge-mcl/pull/77).
All42 changed paths match the approved inventory; product tree clean. Complete diff SHA256
`585ddceaeb6ec5ced608966d89efa0fe88cf1156c5e6e5d34fc33c0a779519be`,2442 insertions/457 deletions.
Full handback/commands/per-file details: `/private/tmp/phase76-core-corrections-20261009T231509Z/EVIDENCE.md`.

| Corrected-source observation | Result / retained log |
|---|---|
| Debug solution build | 0warnings/errors; full-build-final.log |
| Unfiltered full Debug | 1062PASS/6 existing live skips/0fail,2m19s; full-debug-r3b.log |
| Full focused boundary slice | 190PASS/0skip/0fail; focused-final.log |
| Release Core/package | 0warnings/errors,406PASS/0skip, metadata PASS; core-release-final.log/release-package-final.log |
| Existing CLI scripts | 30PASS; script-tests.log |
| Managed lifecycle | macOS arm64 PASS; lifecycle-managed-final.log; not native/PID1 acceptance |
| Fresh normal-version branch consumer | PASS; consumer-run-final.log/consumer-restore-normal-final.log; lower-layer local package only |
| Actual published Client ABI | Client0.9.3 Create/Open/Reconnect without recompilation; Application DLL hashcf472b56aec324111750d6f03a7695e5e134a050bcb11c2a51076aac9cda23ca |
| Canonical native | [run38005239039](https://github.com/katasec/forge-mcl/actions/runs/38005239039), pending atdc03b7b |
| Four hosts/PID1 | [run38005239137](https://github.com/katasec/forge-mcl/actions/runs/38005239137), pending atdc03b7b |

Supervisor independently compared local normal0.1.8 nupkg with the fresh consumer-cache bytes:
both SHA256`e6f6a3e8b138a34db85b7a15d38b8bb53e547b8d7a59e7a6ef1db3eb9afc155b`.
Nuspec source isdc03b7b and Parser/Scout dependencies remain0.1.0. Extracted exact normal metadata
in normal-branch-package.nuspec. Preserved earlier branch-package.nuspec is the failed disposable
prerelease attempt, whose global Version override incorrectly relabeled those dependencies and
failed restore NU1102. No product changes fixed it; the approved normal-version isolated-cache
route passed. Neither local package is published/default acceptance.

Full current simplicity/style code review r2 assigned23:39:10 UTC; end not yet observed.
No prior plan/code PASS is inherited. Reviewer checks code while native CI runs, keeping its
zero-warning verdict open until final current-source logs exist. Ownership follows sequentially.

## Source investigation

Clean forge-mcl baseline `8d28dc1ff8f127facfd708b1c89369179e98cf18` and installed version
`0.10.1-dev.0+8d28dc1ff8f127facfd708b1c89369179e98cf18` independently observed. Core package
inventory contains0.1.7 through0.1.0; candidate0.1.8 unused at planning. Existing PR workflows
provide canonical native checks; no build-script/minimum-OS workaround required.

Confirmed pure-validation gap in ExpertLoader's filesystem diagnostic read and typed-parameter
MCL011 gap. Supervisor resolved immutable-source injection and parameter string seeding in the
active task before planning. Internally derived admitted names, inner format3/old-format refusal
and portable asset collision policy also recorded before plan reviews.

## Pre-change installed-default baseline

Supervisor observation 2026-10-09 22:09 UTC, before Core implementation publication:
installed CLI `0.10.1-dev.0+8d28dc1ff8f127facfd708b1c89369179e98cf18` successfully executed
`forge project create /private/tmp/phase76-core-baseline-20261009T2210` (exit0), then a piped
plain `forge chat --project .../forge.project.json` turn (exit0). Requested literal
`PHASE76_BASELINE_OK` was returned by Chat:Answerer. Produced declaration has Project ID
`9e8f5dad-1622-47c8-a3ca-352d7f7d10cd`, Chat@1 and empty folders. Normal saved login/API
route used; FORGE_API_ENDPOINT, FORGE_PLATFORM_ENDPOINT, RID, RELEASE_TAG and CLI_OUTPUT were
observed absent. Source and stdout/stderr evidence are retained in that disposable directory.
This proves the pre-change baseline only; the merged/published Core default regression is still
required. No new Core/cloud behavior is accepted by this result.

The same installed baseline also ran normal `forge init` then `forge run --steps` in the sibling
`local-run` fixture (both exit0), using normal MCL_PROVIDER/MCL_MODEL/MCL_API_KEY environment
resolution and no provider endpoint override. A role:agent expert requested Write then Read.
Supervisor independently read `result.txt` as exactly `PHASE76_HANDS_BASELINE`; final stdout
returned that text and the outside sentinel remained `outside-unchanged`. Source and separate
init/run stdout/stderr files are retained with the fixture. This is also baseline-only evidence.

Approved-plan documentation merged in [MCL PR374](https://github.com/katasec/mission-control-language/pull/374),
commit `6a0b4fc8584ee2062b9ba1809a1f73771e4d71ca`, 2026-10-09 22:08:34 UTC.

## Implementation evidence — review pending

Frozen source `770778d2d20a8c202552d7c3f3dd1e2cbdc7425c`, 29 approved files,
[draft product PR77](https://github.com/katasec/forge-mcl/pull/77). Evidence retained in
`/private/tmp/phase76-core-primitives-20261009T220732Z`.

| Observation | Result / evidence |
|---|---|
| Full Debug build | Zero warnings/errors, full-build-final.log |
| Focused boundary tests | 167 passed, zero skips, focused-current-final.log |
| Unfiltered full Debug tests | 1039 passed, six existing live skips, zero failures, 2m36s, full-debug-r2.log; normal repository test environment clears MCL_API_KEY only, no test exclusions |
| Normal make verify-core-package | Release slice 383 passed, zero skips, package metadata PASS, core-package-final.log |
| Explicit Release Core build | Zero warnings/errors, core-release-build-final.log |
| Exact committed-source package | final-packages, source provenance770778d; SHA256 920ddf3742295e581403ea86d3ffd823683b6f8726d481d8d2aa02d3849a9421 |
| Fresh isolated branch-package consumer | consumer-final-restore/run.log; generated JSON/assets/executable-bit/old hash/six-argument ctor, exec alias/cwd/runtime directories, exact StepKey, real numeric/in-flight cancelled ONNX all PASS |
| Canonical Native AOT | Pending existing PR workflows37999080680 (macOS package/CLI verification) and37999080639 (four-host release build) |

First full attempt, retained as full-debug-final.log, was stopped before completion after the
exported MCL_API_KEY defeated the unchanged MissingApiKey_ThrowsClearly assertion and a new
delta assertion omitted inherited child streaming. Supervisor independently compared the
missing-key test with origin/main and observed identical source. The delta assertion was corrected;
standard key-cleared unfiltered run completed normally. Stack sampling investigated its longer
duration; no established hang or terminal workaround was claimed and no exclusions were added.

Controlled branch-package evidence is not publication/default acceptance. Independent full code
reviews, supervisor readiness, merge, normal publication and post-merge default observations remain.
Tiny identity.onnx protobuf has no NUL and Git treats it as text, reporting two byte-level
whitespace lines; its reviewed SHA and real numeric result are retained. Text-source whitespace
checks pass. No extra gitattributes owner or fixture-byte mutation was introduced for this report.

## Plan review — simplicity round 1

Full current artifact **PASS**; product source unchanged.

| Check | Verdict / evidence |
|---|---|
| New apps or libraries | PASS — no service/dependency/maintained probe project |
| Reuse | PASS — TOML/ExpertLoader/common validator/adapters |
| Multiple code paths | PASS — shared parser and interpreter/adapter |
| Legacy paths | PASS — inner format2 refused; existing local callers required |
| Knobs | PASS — internally derived names and fixed bounds |
| Speculative abstractions | PASS — policy/fingerprint/workspace have concrete seams |
| Library choice | PASS — existing ONNX; real Linux numeric/cancellation required |
| Copy-paste | PASS — one name policy and shared output convention |
| Redundant definitions | PASS — DTO/trace extended, incomplete fingerprint replaced |
| Size versus requirement | PASS — required Core/test/publication inventory, no consumer migration |
| Test volume | PASS — admission/replay/correlation/process/native boundaries; default proof separate |

Remove/merge: nothing further. Ownership review and supervisor approval remain open.

## Plan review — ownership round 1

Technical **REVISE**, placement **PASS**. Actual restored Client0.9.3 metadata references
DurableMissionPackageInput's six-argument CLR constructor, signature
`200601080E0E0E0E15126D01128461`. Appending optional Assets alone replaces that member.
Current Client source confirms the same calls in StarterChatLaunch, ProjectService and
MissionVersionService. The CLI mixes a current Core project reference with that published DLL;
rebuilding the solution does not recompile it.

| Behaviour | Derived / proposed existing owner | Verdict |
|---|---|---|
| Distribution metadata | Core manifest reader | PASS |
| Immutable diagnostics | Core ExpertLoader | PASS |
| Parameter string typing | Core existing key walk | PASS |
| Pure bounded construction | Core package validator | PASS |
| Asset/model paths/hash/collisions | Core package validator | PASS |
| No-assets hash/actual JSON cap | Core package validator | PASS |
| Reachable/reserved input policy | Core semantics | PASS |
| Profile-name collection | Core package semantics | PASS |
| Complete fingerprint | Core replay | PASS |
| Checkpoint inputs/format | Core pipeline/checkpoint | PASS |
| Exact trace key | Core trace/invocation | PASS |
| Workspace/output convention | Core reusable primitives | PASS |
| Verified process input bindings | Core exec adapter | PASS |
| Duplex limits/joined lifecycle | Core exec adapter | PASS |
| Native inference cancellation/join | Core ONNX adapter | PASS |
| Retained published-client ABI | Core public DTO constructor | REVISE |
| Package/native/API publication checks | Existing forge-mcl publication/test owners | PASS |

No owner acquires a second job and no competing implementation was found. Actual CLI-selected
Client0.9.3, Client.Contracts0.2.1, Conversations.Contracts0.7.0 and Hands0.1.0 DLL inventory
found no ExpertLoader.Validate/ValidatedDurableMissionPackage member reference, so no extra
compatibility shim is evidenced. Supervisor combined correction: preserve exact six-argument
delegating constructor, exercise the actual retained Client Project/chat launch and add normal
installed Project/chat regression. Revised full plan and both new full reviews required.

## Plan serialization clarification

Round2 returned the complete corrected ABI/actual-published-Client plan. Before its independent
reviews, supervisor verified the same change's JSON constructor boundary with the installed
.NET10 source generator. Unannotated two-constructor record deserialization observed
`NotSupportedException`; `[method: JsonConstructor]` on the primary semantic constructor observed
successful asset deserialization. Probe source: `/private/tmp/phase76-json-ctor-probe`.
Round3 must include that annotation and no-assets/nonempty-assets round-trip evidence. No product
dependency, new public serializer API or compatibility reader is required. No code edits.

## Plan review — simplicity round 2

Complete current round 3 plan **PASS**, independently rechecked; no inherited verdict.

| Check | Verdict / current evidence |
|---|---|
| New apps or libraries | PASS — no dependency/service/maintained probe |
| Reuse | PASS — parser, diagnostics, validator, adapters, publication |
| Multiple code paths | PASS — shared parser/interpreter/adapter |
| Legacy paths | PASS — actual loaded Client constructor delegates; inner format2 refused |
| Knobs | PASS — internally derived policy and fixed bounds |
| Speculative abstractions | PASS — concrete policy/fingerprint/workspace seams |
| Library choice | PASS — existing ONNX1.27, real native/Linux checks |
| Copy-paste | PASS — shared traversal/process/output convention; existing CLI loader |
| Redundant definitions | PASS — one semantic DTO; annotation rather than custom reader |
| Size versus requirement | PASS — required Core/publication/retained-consumer inventory |
| Test volume | PASS — JSON/native/actual Client/default tests prove distinct boundaries |

Remove/merge: nothing further. Ownership verdict and supervisor approval still pending.

## Plan review — ownership round 2

Full current round3 technical plan **PASS**, ownership placement **PASS**. All 19 behaviours
independently checked against the atlas/Core README and actual retained published Client DLL.

| Behaviour | Derived/proposed existing owner | Verdict |
|---|---|---|
| Distribution metadata | Core manifest reader | PASS |
| Immutable diagnostics | Core expert loader | PASS |
| String mission parameters | Core semantic key walk | PASS |
| Bounded construction | Core package validator | PASS |
| Asset/model paths, collisions and hash | Core package validator | PASS |
| No-assets hash/serialized cap | Core package validator | PASS |
| Reachable/reserved inputs | Core execution semantics | PASS |
| Profile collection | Core package semantics | PASS |
| Semantic fingerprint | Core replay | PASS |
| Checkpoint admitted inputs/format | Core runner/checkpoint | PASS |
| Exact lifecycle StepKey | Core trace/invocation | PASS |
| Runtime workspace/output calculation | Core execution primitives | PASS |
| Verified file/runtime bindings | Core exec adapter | PASS |
| Concurrent bounded I/O/join | Core exec adapter | PASS |
| Native cancellation/join | Core ONNX adapter | PASS |
| Actual retained constructor | Core public API | PASS |
| JSON constructor/round trips | Core package DTO | PASS |
| Actual retained Client integration | Existing CLI tests | PASS |
| Immutable publication/API/native checks | Existing publication/test owners | PASS |

No duplicate implementation or owner with an unrelated second job found. Reviewer independently
repeated the source-generated constructor probe with the same refusal/success observations.
Move nothing. Runtime evidence remains required, not inferred from plan PASS.

Supervisor approval: complete round3 plan preserves actual binary/wire contracts, keeps pure
Core semantics separate from Host/Runner/Client ownership, retains generated JSON/AOT gates and
requires normal published-package and installed defaults. No public entry point, datastore or
credential boundary changes. UI visual gates N/A. **PLAN APPROVED** for this bounded Core task
only; later consumer/cloud tasks remain unapproved. Implementation uses a new adeen branch.

## Code review — simplicity/style round 1

Frozen product commit `770778d2d20a8c202552d7c3f3dd1e2cbdc7425c`, draft
[PR77](https://github.com/katasec/forge-mcl/pull/77), **REVISE**. Read-only independent review
ended 2026-10-09 22:31:52 UTC; no code changed during review.

| Simplicity check | Verdict / current evidence |
|---|---|
| New apps/libraries | PASS — existing dependencies |
| Reuse | PASS — existing parser, validator, interpreter and adapters |
| Multiple paths | PASS — shared parser and exec implementation |
| Legacy paths | PASS — required six-argument constructor delegates; old checkpoints refused |
| Knobs | PASS — fixed bounds and derived names |
| Speculative abstractions | PASS — policy, fingerprint and workspace serve current requirements |
| Library choice | PASS — actual ONNX numeric/cancellation fixtures |
| Copy-paste | PASS — shared traversal/output calculation |
| Redundant definitions | PASS — existing DTO/generated JSON extended |
| Size versus requirement | PASS — approved 29-file inventory |
| Test volume | PASS — distinct boundary coverage; early-parent-exit case missing |

| Style check | Verdict / current evidence |
|---|---|
| Progressive disclosure | REVISE — Parse hides several coherent stages |
| Small functions | REVISE — changed Parse spans 174 lines |
| Top-down order | REVISE — public StreamAsync follows private helpers in both adapters |
| Explicit errors | REVISE — timeout returns with ordinary descendant alive |
| Shallow nesting | REVISE — parser exceeds two nested levels |
| Separate side effects | PASS — pure admission; named process/native boundaries |
| Zero warnings | OPEN — managed zero warnings; canonical verification failed before AOT |
| Extraction for real reason | PASS — helpers represent semantic/I/O boundaries |
| Complexity | REVISE — classic McCabe 30 for Parse, no recorded exception |

Independent public-API probe: exec Python parent spawns an ordinary sleeping descendant inheriting
the redirected pipes, prints valid JSON, then exits. Adapter timeout 1s returned failure after
1.044s while child PID37841 remained alive; reviewer cleaned it. KillAndJoinAsync skips tree kill
when root.HasExited. Existing tests keep the parent alive and miss this case. Cleanup must retain
ordinary descendant lifetime ownership through pipe completion; this is unrelated to deferred
permissions or hostile-code isolation. A fix requiring new launch primitives returns to Plan.

Supervisor independently read raw logs for canonical macOS
[run37999080680](https://github.com/katasec/forge-mcl/actions/runs/37999080680):
RunAsync_ForwardsForgeEnvironmentVariables expected pass, received fail, line259; **1 failed,
1030 passed, 10 skipped**. Build had zero warnings, but **AOT was not reached**. Diagnose rather than
rerun without explanation. Four-host release run37999080639 was still running at 22:33 UTC.
Ownership code review is next; combine both verdicts before assigning corrections.

## Code review — ownership round 1

Same frozen commit as the preceding full code review. **Ownership placement PASS; technical
REVISE**. Reviewer independently derived ownership from the atlas and component READMEs, then
checked the full production diff, evidence and duplicate searches across the Forge repositories.

| Behaviour | Derived / actual owner | Verdict |
|---|---|---|
| Distribution before environment evaluation | Core manifest reader | PASS |
| Immutable diagnostics / parameter typing | Core expert loader | PASS |
| Pure semantic package construction | Core package validator | PASS |
| Kinds / bindings / root metadata | Core package validator | PASS |
| Assets / integrity / collisions / model references | Core package validator | PASS |
| Old hashes / actual serialization cap | Core package validator | PASS |
| Reachable / reserved input policy | Core execution semantics | PASS |
| Profile names without deployment authority | Core package semantics | PASS |
| Actual retained ABI / generated JSON | Core public DTO | PASS |
| Execution-semantic fingerprint | Core replay | PASS |
| Admitted replay / invalid shape refusal | Core checkpoint | REVISE correctness |
| Exact lifecycle StepKey | Core trace/execution | PASS |
| Runtime workspace / output calculation | Core execution primitives | PASS |
| Verified artifact / runtime bindings | Core exec adapter | PASS |
| Bounded joined process lifecycle | Core exec adapter | REVISE correctness |
| Native inference cancellation/join | Core ONNX adapter | PASS inspected implementation |
| Actual retained Client integration | Existing CLI integration tests | PASS |
| Immutable publication / provenance | Existing publication owners | PASS placement; acceptance pending |

No owner gains an unrelated job, no duplicate implementation found, no cloud state/credential
authority moved into Core. Reviewer independently confirmed the parent-first-exit cleanup defect
and read actual canonical macOS failure logs; these findings agree with the preceding review.

Additional source-confirmed defect: PipelineCheckpointCodec.TryRead accepts format3 with null or
missing `root_inputs`; ResumeAsync then dereferences RootInputs.Keys before the execution try
block. Reject malformed shape at the codec owner; mutate a valid current checkpoint in the
regression and observe InvalidContinuation with no provider invocation.

Supervisor combined correction assigned to the same implementer at 2026-10-09 22:36:12 UTC,
**read-only plan/investigation round 4**. Include parser cohesion/complexity, public method order,
checkpoint shape refusal, diagnosed canonical exec failure and reliable ordinary descendant
ownership through pipe completion. Existing APIs, package semantics, permissions policy and
default gates stay fixed. A native/library launch change outside the original inventory returns
to full Plan review before approval. No polling snapshot or post-launch ownership race proves the
required containment; no unsupported .NET11 API may be assumed available on .NET10.

## Read-only correction investigation

Frozen Core public-API scratch probe in `/private/tmp/phase76-core-r4-probes`: Python closes
stdin, writes valid JSON and exits0. Declared input sizes1 and16384 produced30/30 pass envelopes
each; size262144 produced30/30 failure envelopes with `Executable I/O failed: Broken pipe`.
Separate writer/close after observed child exit threw IOException HResult80131620 with inner
SocketException. Supervisor independently read the probe source and actual retained log. This
reproduces a defect but does **not** establish the failed CI test's exact reason; CI only logged
its pass/fail assertion. Preserve that assertion and include reason diagnostics in the correction.

Managed macOS P/Invoke scratch prototype `/private/tmp/phase76-posix-launch-probe/probe-r3.log`
observed POSIX spawn with a new group, root exit via waitid WEXITED|WNOWAIT, and pipes still open
through the ordinary descendant. An unrelated .NET Process child started/waited successfully;
group SIGKILL then closed pipes and waitpid reaped the exact root. Captured child JSON names root
and group44182, descendant44183; the delayed sentinel was absent after2.2s. Supervisor read source
and retained output independently. This is a managed macOS mechanism probe, **not** a product,
Native AOT, other-host or default-path acceptance result. The same investigation identified inner
SocketException native code32 / Shutdown for EPIPE. Complete reviewed plan and product changes
remain pending.

At2026-10-09 22:41:43 UTC, supervisor directly observed release run37999080639 jobs successful
on Windows ARM64, Linux ARM64 and Linux x64; macOS remained running. These help/version/AOT
checks at the old frozen source do not prove a future platform launcher or close this task.

## Correction plan round 4 — supervisor findings before reviews

Complete round4 returned a 42-file Core/test/build inventory with one private platform lifetime
seam, POSIX spawn/unreaped leader and preexecution Windows job ownership. Original package APIs,
ABI, generated JSON, parser, replay, workspace, ONNX and default gates were retained. No product
or repository-document edits occurred during the read-only stage. No independent PASS is claimed.

Before assigning reviews, supervisor identified required detail: the actual Runner entrypoint can
be PID1 and adopt ordinary grandchildren; owned-group reaping must cover that runtime. Closed
stdin mapping must name every platform's concrete errors. A blocking observer cannot be abandoned
or hang indefinitely if termination itself fails. Deliberately externally reaping the leader before
termination for a fault test would break the identity invariant being protected; a safe, honestly
controlled boundary test is acceptable. Complete round5 read-only refinement was assigned before
independent plan reviews; no implementation approval.

Supervisor independently read `/private/tmp/phase76-posix-r5-probe/Program.cs` and `probe.log`:
fixed-owned-root waitid WNOHANG observation cancelled and was awaited, then a second observation
retained root50184 unreaped while descendant50185 held the pipes. Checked group termination and
exact root reap completed; delayed sentinel remained absent. Managed macOS mechanism evidence
only, not Linux/PID1, Native AOT or default acceptance. [.NET10 PipeStream.Windows](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.IO.Pipes/src/System/IO/Pipes/PipeStream.Windows.cs) source also
confirms its synchronous-pipe ReadAsync/WriteAsync use [AsyncOverSyncWithIoCancellation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/Common/src/System/Threading/AsyncOverSyncWithIoCancellation.cs), which
attempts CancelSynchronousIo and joins the cancellation callback; reuse the BCL pipe boundary
rather than author another I/O thread-cancellation mechanism. Product behavior still needs the
reviewed current-source native checks.

## Correction-plan review — simplicity round 3

Complete round5 artifact reviewed, with a fresh full checklist. **REVISE** for one contract
wording mismatch; the thirteen added paths were independently judged necessary for the agreed
cross-platform joined lifetime and normal native proof, with no scope removal proposed.

| Check | Verdict / current evidence |
|---|---|
| New apps or libraries | PASS — probe is test tooling outside shipped payload; no added product app/dependency |
| Reuse | REVISE — plan calls ArtifactPaths registry immutable; locked contract requires Runner-owned live ConcurrentDictionary backing registry, retained by read-only Core API |
| Multiple code paths | PASS — one exec adapter with private platform ownership implementations |
| Legacy paths | PASS — actual published Client ABI overload; no old checkpoint reader |
| Knobs | PASS — fixed private cleanup deadline, no user setting |
| Speculative abstractions | PASS — present lifetime/failure seams, no public process framework |
| Library choice | PASS — inspected implementations do not supply required joined lifecycle unchanged; BCL pipes/ONNX reused |
| Copy-paste | PASS — shared adapter exchange/error boundary and parser |
| Redundant definitions | PASS — existing key/hash/parser/build authorities retained |
| Size versus requirement | PASS — six native/lifetime/argument files, three probe files, four existing build integration files; no equivalent native harness found |
| Test volume | PASS — observed defects, retained contracts and required native proof; controlled error test labeled accurately |

Supervisor independently confirms frozen PipelineExecutionWorkspace already retains the supplied
ArtifactPaths reference. Correct the plan's immutability statement; no corresponding product-code
change is required. Complete ownership review is next; no current plan approval.

## Correction-plan review — ownership round 3

Complete round5 plan, task, locked parent, atlas/READMEs and frozen clean770778d independently
reviewed. **Placement PASS; technical REVISE** for the same live-registry mismatch. Reviewer
observed working start23:07:39 UTC; assignment boundary was23:07:16 UTC, used in timing below.

| Behavior | Derived owner / proposed placement | Verdict |
|---|---|---|
| Distribution literals before environment evaluation | Existing Core manifest reader | PASS |
| Full local TOML evaluation/diagnostics | Same Core parser | PASS |
| Supplied markdown diagnostics without disk fallback | Core ExpertLoader | PASS |
| Declared mission parameters as strings | Core semantic validation | PASS |
| Pure package construction/validation | Core package validator | PASS |
| Portable assets/executable metadata/collisions/models | Core package validator | PASS |
| No-assets hash and actual serialized budget | Core package contract | PASS |
| Reachable inputs/exact reserved names | Core input semantics | PASS |
| Existing kinds/reachable profile names | Core validation; deployment retains availability | PASS |
| Actual Client ABI/generated JSON | Core DTO and existing CLI integration tests | PASS |
| Semantic fingerprint/incompatible replay | Core pipeline/checkpoint | PASS |
| Malformed current checkpoint shape | Existing Core codec | PASS |
| Relative replay/effects once/nested workspace | Core pipeline | PASS |
| Exact StepKey/attempt facts | Core trace contract | PASS |
| Workspace/pure output directory | Core shared primitive | PASS |
| Live verified artifact registry | Runner owns mutable backing map; Core retains read-only reference | REVISE plan wording |
| Process-local verified paths/runtime directories | Existing Core exec | PASS subject to registry correction |
| Command/args/cwd/inherited authority | Existing Core exec | PASS |
| Bounded concurrent I/O/precise declined stdin | Existing Core exec | PASS |
| Atomic retained POSIX group | Private Core exec lifetime | PASS |
| Atomic retained Windows job | Private Core exec lifetime | PASS |
| Descendant termination/joined I/O/observation | Private Core exec lifetime | PASS |
| Linux PID1 owned adopted-child reaping | Private Core exec lifetime; scoped Type2 | PASS |
| Cleanup failure over cancellation | Core exchange boundary | PASS |
| Numeric ONNX/joined native cancellation | Existing Core ONNX adapter | PASS |
| Native/PID1 proof | Core test tooling and existing build owner | PASS |
| Immutable publication/retained consumers/defaults | Existing publication and CLI acceptance owners | PASS |

No new runtime component, second job, duplicate lifetime owner, credential or persistent-data
authority. Desktop termination, Hands containment and LocalDiskWorkspace terminal execution
serve different owners/protocols. Atlas excludes test projects; the maintained probe is test
tooling outside the shipped payload. Combined correction: state the live retained backing map,
and add a focused existing exec-test regression showing a caller-registered path becomes usable
by a subsequent exec through the same workspace. Actual frozen product already retains the
reference; no new registry mechanism/file is required. Same implementer assigned complete r6
plan correction only, without further investigation or product edits.

## Correction-plan review — simplicity round 4

Complete current r6 plan rechecked independently against all eleven checks; **PASS**, no inherited
verdicts or remaining plan blocker. The live backing registry and same-workspace public behavior
regression match the locked contract; all42 files and original proof/default gates remain scoped.

| Check | Current verdict |
|---|---|
| New apps or libraries | PASS — probe outside shipped payload, no added dependency |
| Reuse | PASS — retain caller-owned live registry; no snapshot/new mechanism |
| Multiple code paths | PASS — one exec/parser/interpreter with private OS lifetime details |
| Legacy paths | PASS — actual published ABI overload, no checkpoint compatibility reader |
| Knobs | PASS — fixed private budget, no user setting |
| Speculative abstractions | PASS — present ownership/error boundaries |
| Library choice | PASS — inspected libraries miss joined lifecycle; BCL/ONNX reused |
| Copy-paste | PASS — shared exchange/error/parser implementation |
| Redundant definitions | PASS — existing key/hash/parser/build authority |
| Size versus requirement | PASS — thirteen added paths substantiate agreed lifetime/proof |
| Test volume | PASS — observed failures/contracts/native outcomes and same-workspace regression |

Nothing to remove or merge. Code review/native/PID1/publication/default acceptance remain required.

## Correction-plan review — ownership round 4 and approval

Complete current r6 artifact independently rechecked against task, locked contracts, owner
READMEs and clean770778d. **Technical PASS; placement PASS**, no inherited verdict. Reviewer
observed working start23:12:44 UTC; assignment boundary23:12:24 UTC is used below.

| Behavior | Derived owner / proposed placement | Current verdict |
|---|---|---|
| Literal distribution before environment evaluation | Core manifest reader | PASS |
| Full local parsing/evaluation/diagnostics | Same Core parser | PASS |
| Immutable source diagnostics without disk fallback | Core ExpertLoader | PASS |
| Mission parameter string typing | Core semantic validation | PASS |
| Pure resolved package construction/validation | Core package validator | PASS |
| Assets/executable metadata/collisions/models | Core package validator | PASS |
| No-assets hash/actual JSON budget | Core package contract | PASS |
| Reachable inputs/exact reserved policy | Core input semantics | PASS |
| Existing kinds/reachable profiles | Core validation; deployment owns availability | PASS |
| Published Client ABI/generated JSON | Core DTO and CLI integration tests | PASS |
| Full semantic fingerprint/incompatible replay | Core pipeline/checkpoint | PASS |
| Malformed current checkpoint refusal | Existing Core codec | PASS |
| Relative replay/completed effects once | Existing Core pipeline | PASS |
| Exact StepKey/attempt traces | Core trace contract | PASS |
| Workspace inheritance/pure output directory | Core shared primitive | PASS |
| Verified artifact registration | Caller/Runner; Core retains live read-only reference | PASS |
| Same-workspace newly registered path observation | Core exec reads, caller/Runner writes; existing test file | PASS |
| Absolute process-only JSON/environment mapping | Core exec; root/context/checkpoint stay relative | PASS |
| Command/args/cwd/inherited authority | Core exec | PASS |
| Bounded concurrent streams/precise declined stdin | Core exec | PASS |
| Atomic POSIX group/retained root identity | Private Core exec lifetime | PASS |
| Atomic Windows job | Private Core exec lifetime | PASS |
| Descendant termination/joined process and I/O | Private Core exec lifetime | PASS |
| Linux PID1 owned adopted-child reap | Private Core exec lifetime; scoped Type2 | PASS |
| Visible cleanup error over cancellation | Core exchange/error boundary | PASS |
| Numeric ONNX/joined native disposal | Existing Core ONNX adapter | PASS |
| Four native hosts/PID1 proof | Core test tooling and existing build scripts | PASS |
| Immutable Core provenance/public APIs | Existing publication owner | PASS |
| Retained Client/installed defaults | Existing CLI integration/acceptance owners | PASS |

No second job, duplicate lifetime owner or new runtime component. No public, credential,
persistent-data or permission authority expands. No placement change required. Native/PID1,
canonical macOS diagnosis, actual publication and installed defaults remain future required gates.

Supervisor independently checked the full r6 artifact and the exact r5→r6 diff, confirmed all42
files, retained ABI/public shapes, live caller-owned registry, .NET10 mechanisms, existing owner
and failure boundaries, package/default gates and N/A visual/security-data changes. **PLAN APPROVED
2026-10-09 23:13:40 UTC** for this bounded complete r6 plan only. No merge/publication/acceptance
approval. The same implementer receives correction implementation after this record is delivered.

## Complete round 7 clarification — full reviews and approval

The supervisor corrected the complete six-section plan after the stopped macOS deviation;
no new scope, files, public APIs, credentials, permissions or persistence owner. Fresh full
simplicity review r5 PASS (23:23:48–23:24:47 UTC); its inherited r6 approval-text comment was
corrected as metadata. Fresh full ownership review r5 technical/placement PASS
(assignment23:25:08, observed agent work23:25:40, end23:26:21 UTC).

| Simplicity check | Current r7 verdict / evidence |
|---|---|
| New apps/libraries | PASS; probe outside payload, no dependency |
| Reuse | PASS; parser/pipes/live registry/platform APIs retained |
| Multiple paths | PASS; one exec adapter, private macOS observation |
| Legacy paths | PASS; actual Client ABI, old checkpoints refused |
| Knobs | PASS; same private five-second budget |
| Speculative abstractions | PASS; observed lifetime/failure boundary only |
| Library choice | PASS; existing system libproc, independently checked XNU |
| Copy-paste | PASS; shared exchange/error policy and single parser |
| Redundant definitions | PASS; existing keys/hash/build owners; one native import |
| Size versus requirement | PASS; same42-file inventory and authority |
| Test volume | PASS; real group-state cases, no invented permission-fault proof |

| Behavior | Derived owner / current r7 verdict |
|---|---|
| Distribution before environment evaluation | Core manifest reader; PASS |
| Full local TOML behavior | Core manifest reader; PASS |
| Immutable expert-source diagnostics | Core ExpertLoader; PASS |
| Mission parameters typed as strings | Core semantic validation; PASS |
| Pure resolved-package construction | Core package validator; PASS |
| Asset/hash/executable/collision/model validation | Core package validator; PASS |
| No-assets identity/actual serialized size | Core package contract; PASS |
| Reachable admitted inputs/reserved names | Core input semantics; PASS |
| Existing kinds/reachable profile names | Core validation; deployment availability; PASS |
| Actual Client ABI/generated JSON | Core DTO/CLI integration tests; PASS |
| Semantic fingerprint/incompatible replay | Core checkpoint; PASS |
| Malformed checkpoint refusal before invocation | Core codec; PASS |
| Relative replay/completed effects once | Core pipeline; PASS |
| Internal StepKey/attempt lifecycle traces | Core trace contract; PASS |
| Workspace inheritance/pure output directories | Core execution primitive; PASS |
| Verified artifact registration | Caller/Runner; PASS |
| Same-workspace registration visibility | Core reads/caller writes; PASS |
| Process-only absolute mappings | Core exec; PASS |
| Command/argv/cwd/inherited authority | Core exec; PASS |
| Bounded streams/exact declined stdin | Core exec; PASS |
| Atomic POSIX group/retained root | Private Core exec lifetime; PASS |
| Atomic Windows job | Private Core exec lifetime; PASS |
| Descendant termination/joined process and I/O | Private Core exec lifetime; PASS |
| macOS root-only observation after pending EPERM/success | Private Core exec lifetime; PASS |
| Linux PID1 exact owned adopted reap | Private Core exec lifetime/scoped Type2; PASS |
| Cleanup failure over cancellation | Core exchange boundary; PASS |
| Numeric ONNX/joined native cancellation | Core ONNX adapter; PASS |
| All-host lifecycle/macOS states/PID1 proof | Test tooling/existing build scripts; PASS |
| Immutable publication/fresh restored APIs | Existing Core publication owner; PASS |
| Published Client/installed defaults | Existing CLI integration/acceptance owners; PASS |

Supervisor independently checked the full artifact, primary XNU signal/list code and the
retained-root-only proof: query does not establish authority or choose kill targets; only exact
root-only membership resolves pending EPERM; all other errors/deadlines stay visible. Fixed
budget, public contracts, same42-file inventory, AOT and normal default-path gates remain.
**PLAN APPROVED2026-10-09 23:27:01 UTC**. No merge/publication/acceptance approval.
Same implementer resumed at23:27:10 UTC under implement:r3; stage ended23:38:49 UTC.

## Full corrected-source code review — round 2

Simplicity/style reviewed the complete42-path diff atdc03b7b from23:39:10 to23:42:25 UTC.
All ten structural simplicity checks passed; the test-volume check requires two already-approved
observations: native duplex pressure/environment assertions in the existing self-spawning probe,
and an actual PipelineRunner parallel sibling launch while native ONNX inference remains unfinished,
followed by joined cancellation with no score write. No new product defect or style refactor was
found. Full managed/Release logs show zero warnings; current-source canonical/four-host AOT logs
remain pending, so the style zero-warning row is open. These findings require an implementation
correction within approvedr7, after the sequential full ownership review assigned23:42:55 UTC.

| Simplicity check | Current corrected-source verdict |
|---|---|
| New apps/libraries | PASS; probe is test tooling, no production dependency |
| Reuse | PASS; live caller registry, parser and managed pipes retained |
| Multiple paths | PASS; one adapter/private OS ownership implementations |
| Legacy paths | PASS; actual Client ABI retained, old checkpoints refused |
| Knobs | PASS; fixed private cleanup budget |
| Speculative abstractions | PASS; concrete lifetime/pipe/cleanup owner |
| Library choice | PASS; existing pipes/platform/ONNX packages |
| Copy-paste | PASS; shared exchange/error boundary |
| Redundant definitions | PASS; existing StepKey/output convention |
| Size versus requirement | PASS; approved42 paths,2442 insertions/457 deletions |
| Test volume | REVISE; native duplex/env and ONNX pipeline sibling observations missing |

| Code-style check | Current corrected-source verdict |
|---|---|
| Progressive disclosure | PASS; execution/parser entry flow |
| Small functions | PASS; coherent parser/native stages |
| Top-down order | PASS; public RunAsync/StreamAsync precede helpers |
| Explicit errors | PASS; cleanup failure precedence and macOS retained-root proof |
| Shallow nesting | PASS; bounded guards/stages |
| Separate side effects | PASS; named launch/observe/reap/argument boundaries |
| Zero warnings | OPEN; managed/Release zero, native CI pending |
| Extract for real reason | PASS; actual lifetime/parser/error boundaries |
| Complexity | PASS; manual classic AddRow9,ExchangeAsync8,POSIX JoinAsync8,probe RunAsync10 |

Full ownership review assigned23:42:55 UTC, observed work23:43:23–23:47:22 UTC:
placement PASS, technical REVISE for the same two verification gaps. Reviewer independently
derived owners from the Desktop atlas, Core and build READMEs before comparing the complete diff.

| Behavior | Derived owner / current code verdict |
|---|---|
| Package asset declarations | Core manifest; PASS |
| Distribution metadata before local/provider environment evaluation | Core manifest; PASS |
| Full local TOML behavior | Core manifest; PASS |
| Immutable markdown diagnostics/no filesystem fallback | Core ExpertLoader; PASS |
| Declared parameters as strings | Core semantic validation; PASS |
| Pure resolved package construction/validation | Core package; PASS |
| Legacy hash and asset/executable identity | Core package; PASS |
| Actual generated JSON size | Core package; PASS |
| Safe/collision-free assets/admitted ONNX model | Core package; PASS |
| Reachable admitted names/exact reserved names | Core input semantics; PASS |
| Reachable profile reporting | Core semantics/deployment availability; PASS |
| Six-argument ABI/generated JSON constructor | Core public contract; PASS |
| Actual published Client Create/Open/Reconnect | Core compatibility tests; PASS |
| Malformed checkpoint/unsupported format refusal | Core codec; PASS |
| Admitted root input retention/resume | Core replay; PASS |
| Execution-semantic fingerprint/no scratch identity | Core replay; PASS |
| Runtime workspace/deterministic output allocation | Core primitive; PASS |
| Live artifact registry/caller registration authority | Caller writes/Core reads; PASS |
| Child workspace/exact lifecycle StepKey | Core pipeline/trace; PASS |
| Process-only verified input/runtime alias mapping | Core exec; PASS |
| Literal command/argv/lookup/expert cwd | Core exec; PASS |
| Bounded concurrent joined streams | Core exec; PASS |
| Precise declined-input classification | Core exec; PASS |
| Output/status/reason/judge/streaming behavior | Core exec; PASS |
| Atomic POSIX group/retained root | Private Core lifetime; PASS |
| macOS exact held-root-only observation | Private Core lifetime; PASS |
| Linux exact owned adopted reap | Private Core lifetime/scoped Type2; PASS |
| Atomic Windows job and completion | Private Core lifetime; PASS |
| Cleanup failure precedence/resource release | Core lifetime/error boundary; PASS |
| Numeric ONNX/relative model/joined cancellation | Core ONNX; PASS |
| Actual parallel ONNX sibling observation | Core test owner; REVISE missing observation |
| All-host native lifecycle/PID1 proof | Existing probe/build owner; REVISE missing duplex/env cases |
| Immutable normal publication/provenance | Existing Core package owner; placement PASS, delivery pending |

No duplicate component, second job, new credential/datastore/permission authority or additional
established product defect. Move nothing. Supervisor independently confirmed both missing cases
are already required by approvedr7, combined both reviews and resumed the same implementer under
**implement:r4 at2026-10-09 23:47:54 UTC, PLAN APPROVED** for those existing-file verification
corrections only. No new scope or plan mechanism; full current-source reviews/native gates and
published/installed acceptance remain mandatory.

## Timing

### Verification correction r4 and Linux container failure

Same implementer23:47:54–23:52:26 UTC changed only existing probe Program/ExecProbeChild,
ONNX tests and scripts README. Held uncommitted at supervisor direction when current-source
Linux PID1 failed. No production workaround, commit or push. Evidence:
`/private/tmp/phase76-core-corrections-20261009T234754Z/EVIDENCE.md`,
correction-uncommitted.diff and complete-current.diff. Full Debug1063PASS/6 existing skips/0fail;
focused191PASS/0skip; Release/package407PASS; final Debug/CoreRelease zero warnings/errors.
Managed probe validates2MB stdin/1MB JSON stdout/48KiB stderr, actual PipelineRunner workspace
and environment bindings with unchanged relative caller source; native ONNX parallel sibling
case passes with bounded joined cancellation/no Numeric completion. Direct adapter no-score
assertion retained. These new observations are managed only; native CI has not run these edits.
Supervisor read the complete four-file correction independently. Its pressure child starts
stdout/stderr writes while concurrently reading stdin, so a sequential parent could still
drain stdin first and pass. The revised Core plan must make that existing test decisive:
finish the large stdout/stderr writes before reading stdin, retaining valid JSON, caps and
bounded cleanup. This is verification of the already-required duplex behavior, not a new
production mechanism or feature. No additional product defect was established by that read.

Atdc03, normal Linuxx64 native probe passed all old lifecycle cases. Actual PID1 container then
failed during VerifyArgumentsAsync with waitid ECHILD10 after standalone Process.Start initialized
the managed child reaper. Raw log native-linux-x64-dc03.log in the previous evidence directory;
[run38005239137](https://github.com/katasec/forge-mcl/actions/runs/38005239137), job114072387155.
SDK10.0.401/runtime10.0.12; base image digest222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4.
Supervisor checked exact .NET10.0.12 primary source: PID1 reapAll eventually uses waitpid(-1),
competing with Core's retained native root. Correct identity-loss handling refused further
numeric-group signalling and exposed IOException; it does not establish successful cleanup.
The [hosting design](phase-76.5-runner-process-hosting.md) locked00:00:19 UTC after full current
reviews supersedes the former Core PID1 adopted-child choice. No runtime-handler patch or native/default
waiver is approved. Hosting plan approval comes first; full revised Core plan follows its closure.

Additional frozen-dc03 native observations: LinuxARM64 job114072387316 and WindowsARM64
job114072387441 succeeded. Supervisor downloaded raw logs native-linux-arm64-dc03.log and
native-windows-arm64-dc03.log into the earlier evidence directory, observed lifecycle/cwd/PATH/
closed-stdin/early-root timeout and caller-cancel PASS and no raw compiler/linker warning.
Windows closed-input HRESULT800700E8 confirms the approved232 mapping; Linux reports native32.
These runs precede uncommitted verification additions and the pending hosting correction;
they do not close the final-source/native/default gates. Canonical macOS and macOS matrix
were still running at the last snapshot.

Supervisor later directly observed release run37999080639 completed successfully on all four
native hosts at source770778d (GitHub updatedAt2026-10-09T22:54:45Z). These frozen-source build/help/version results remain lower-layer evidence; the
that old source's canonical macOS managed suite failed; it did not test corrected-launch behavior.

| Stage / role / round | Start (UTC) | End (UTC) | Wall | Evidence |
|---|---|---|---|---|
| Scope/design / supervisor / Core producer | 2026-10-09 21:40:31 | 2026-10-09 21:41:50 | 1m19s | Bounded producer spoke from locked parent |
| Plan / implementer / r1 | 2026-10-09 21:41:50 | 2026-10-09 21:49:51 | 8m01s | Read-only full plan; source clarifications recorded during investigation |
| Review plan / simplicity / r1 | 2026-10-09 21:51:50 | 2026-10-09 21:52:47 | 57s | Full 11-check PASS |
| Review plan / ownership / r1 | 2026-10-09 21:53:05 | 2026-10-09 21:55:16 | 2m11s | Full 17-behaviour REVISE; actual binary constructor reference |
| Plan / implementer / r2 | 2026-10-09 21:56:00 | 2026-10-09 21:57:28 | 1m28s | Full ABI-corrected plan; no reviews before JSON clarification |
| Plan / implementer / r3 | 2026-10-09 22:00:20 | 2026-10-09 22:00:54 | 34s | Complete plan with retained ABI, actual Client launch and generated-JSON constructor/round trips |
| Review plan / simplicity / r2 | 2026-10-09 22:05:02 | 2026-10-09 22:05:42 | 40s | Full current round3 plan, all 11 checks PASS |
| Review plan / ownership / r2 | 2026-10-09 22:06:02 | 2026-10-09 22:07:02 | 1m00s | Full current round3 plan, all 19 behaviours PASS |
| Plan approval / supervisor | 2026-10-09 22:07:32 | 2026-10-09 22:07:32 | Instant boundary | Explicit bounded PLAN APPROVED; implementation assigned same agent |
| Implement / same implementer / r1 | 2026-10-09 22:07:32 | 2026-10-09 22:27:04 | 19m32s | Frozen770778d, attached draft product PR77; managed/package facts pass; canonical native pending |
| Review code / simplicity and style / r1 | 2026-10-09 22:27:56 | 2026-10-09 22:31:52 | 3m56s | Full current tables REVISE; early-parent-exit cleanup reproduction, parser complexity and canonical macOS test failure |
| Review code / ownership / r1 | 2026-10-09 22:33:18 | 2026-10-09 22:35:47 | 2m29s | Full18-behaviour placement PASS; technical REVISE for lifecycle/checkpoint shape; canonical failure independently confirmed |
| Plan / implementer / r4 | 2026-10-09 22:36:12 | 2026-10-09 22:49:09 | 12m57s | Read-only library/native/closed-stdin probes and complete42-file correction plan; supervisor refinement required before reviews |
| Plan / implementer / r5 | 2026-10-09 22:51:07 | 2026-10-09 23:03:22 | 12m15s | Complete42-file correction plan returned and transcribed; product frozen; full reviews/approval pending |
| Review plan / simplicity / r3 | 2026-10-09 23:04:07 | 2026-10-09 23:07:02 | 2m55s | Complete current r5 artifact; full11-check REVISE for live registry wording, bounded expansion otherwise justified |
| Review plan / ownership / r3 | 2026-10-09 23:07:16 | 2026-10-09 23:09:56 | 2m40s | Full27-behavior placement PASS; technical REVISE for live registry wording and missing behavior regression |
| Plan / same implementer / r6 | 2026-10-09 23:10:31 | 2026-10-09 23:11:13 | 42s | Complete current plan corrects live registry ownership and adds existing-file behavior regression; no extra investigation or product change |
| Review plan / simplicity / r4 | 2026-10-09 23:11:34 | 2026-10-09 23:12:08 | 34s | Full current r6 plan, all11 checks PASS; ownership next |
| Review plan / ownership / r4 | 2026-10-09 23:12:24 | 2026-10-09 23:13:04 | 40s | Full current r6 plan, all29 behaviors technical/placement PASS |
| Correction-plan approval / supervisor | 2026-10-09 23:13:40 | 2026-10-09 23:13:40 | Instant boundary | Complete r6, fresh full reviews and independent supervisor check; bounded PLAN APPROVED |

| Implement / same implementer / r2 | 2026-10-09 23:15:09 | 2026-10-09 23:23:29 | 8m20s | Intermediate code; managed build0warnings, focused41PASS, scripts30PASS; macOS EPERM deviation stopped |
| Plan clarification / supervisor / r7 | 2026-10-09 23:23:18 | 2026-10-09 23:23:38 | 20s | Same42 files; exact retained-root-only libproc observation |
| Review plan / simplicity / r5 | 2026-10-09 23:23:48 | 2026-10-09 23:24:47 | 59s | Full current r7, all11PASS; historical approval metadata corrected |
| Review plan / ownership / r5 | 2026-10-09 23:25:08 | 2026-10-09 23:26:21 | 1m13s | Full current r7, all30 technical/placement PASS |
| Plan approval / supervisor / r7 | 2026-10-09 23:27:01 | 2026-10-09 23:27:01 | Instant boundary | Full artifact/primary source/authority/failure/default gates checked; bounded PLAN APPROVED |
| Implement / same implementer / r3 | 2026-10-09 23:27:10 | 2026-10-09 23:38:49 | 11m39s | Frozendc03b7b; full1062/focused190/Release406/scripts30 PASS; current-source native CI pending |
| Review code / simplicity and style / r2 | 2026-10-09 23:39:10 | 2026-10-09 23:42:25 | 3m15s | Full separate tables; required native duplex/env and ONNX pipeline sibling observations missing; native warnings pending |
| Review code / ownership / r2 | 2026-10-09 23:42:55 | 2026-10-09 23:47:22 | 4m27s | Full33-behavior table; placement PASS, same2 verification gaps; observed work began23:43:23 |
| In-plan correction approval / supervisor / r4 | 2026-10-09 23:47:54 | 2026-10-09 23:47:54 | Instant boundary | Combined reviews, same approvedr7 scope, same implementer; native/default gates retained |
| Implement / same implementer / r4 | 2026-10-09 23:47:54 | 2026-10-09 23:52:26 | 4m32s | Four existing test/doc files, full1063/focused191/Release407 PASS; held uncommitted for actual PID1 failure |
| Hosting correction design / supervisor | 2026-10-09 23:52:24 | 2026-10-09 23:53:00 | 36s | Standard init ownership proposal; documentation12files/81links/2JSON/fences/global/diff PASS; no product approval |

Tokens N/A; not independently measured. Product PR and acceptance timing remain future.
