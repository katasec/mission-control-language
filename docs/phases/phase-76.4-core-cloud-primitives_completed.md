# Phase 76.4 — review evidence and stage timing

**Task remains open.** This file records finished investigations/reviews, not product completion.
[Active task](phase-76.4-core-cloud-primitives.md) · [Current plan](phase-76.4-core-cloud-primitives-plan.md).
Plan approved after full current independent reviews; implementation/publication/default acceptance remain open.

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

## Timing

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

Tokens N/A; not independently measured. Product PR and acceptance timing remain future.
