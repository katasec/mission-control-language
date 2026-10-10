# Phase 76.4 — review evidence and stage timing

**Task remains open.** This file records finished investigations/reviews, not product completion.
[Active task](phase-76.4-core-cloud-primitives.md) · [Current plan](phase-76.4-core-cloud-primitives-plan.md).
Complete r10 plan and its approved corrections are implemented; current handback is below.
Both fresh full current code reviews pass; native verification/publication/default acceptance remain open.

## Retained local overrides — investigation r10

Read-only stage **2026-10-10 02:00:23–02:02:52 UTC**; no repository change.
Supervisor suspected that pausable execution dropped global-let `--var` overrides. Actual
production CLI regressions both PASS (2/2,874ms), including successive Hands calls. A controlled
public-API first execution and fresh-runner resume both retain overrides through the CLI's
existing `Vars + ContextObjects` composition. `Vars` alone filters lets on original main
`8d28dc1` as well as current source; this behavior predates this phase. Checkpoint inputs exclude
process-local values, and cloud admission still rejects undeclared, global-let and reserved names.
Supervisor independently read raw test/probe output. No demonstrated regression and no correction
authorized. This controlled evidence does not replace installed-default acceptance. Reproduction,
original source excerpts and raw logs: `/private/tmp/phase76-vars-probe-20261010T020023Z/EVIDENCE.md`.

## Diagnostic correction — implementation r10

Approval02:19:30 UTC; same implementer observed **02:20:11–02:27:00 UTC**. Clean/pushed source
`0d0a338f672469356b3c412e7a4ccf2dc11d74e7`, draftPR77. Only build script/README delta81+/1-;
full42paths2698+/457-. Complete `git diff --binary 8d28dc1 HEAD` SHA256 independently matched
by supervisor: `194334957aebd581ddfd90ccf035ae1ff9d2e67d9ea5d98e946c84d17c291909`.
Plain text Git diff has a different hash because it omits binary fixture patches; the recorded
artifact and review identity is explicitly the complete binary diff. Production tree remains
`6c7cfd841a81fd60ad0fd3d7213ae75e4bd202f9`, identical to8f0fa851/5a80c56e.

Supervisor independently read actual final diff/parser/script/controlled logs: parserPASS,
30existing script checksPASS,23controlled checksPASS. Exact-new process launch admitted;
old despite newmtime/wrongidentity/malformedtime/unsupported rejected. Eligible user/system
copies occur once; actual10s poll, absent roots/no cwd fallback and directory/read/copy errors
observed. Actual status-file path occupied by a directory makes Set-Content fail; terminating
write behavior preserves the original probe failure. Successful/non-macOS probes do not collect.
All failed development logs remain retained; no native production test was retried into acceptance.

Existing two helpers and narrow collector nesting clarification implemented; no new workflow,
API, dependency, fixture, payload, deadline or source change. Version inventory0.1.7latest/
0.1.8absent and tag404 observed. Full commands/inventory/raw evidence:
`/private/tmp/phase76-core-crash-evidence-20261010T022011Z/EVIDENCE.md`.

Current [canonical38016928681](https://github.com/katasec/forge-mcl/actions/runs/38016928681)
passes; [four-host38016928684](https://github.com/katasec/forge-mcl/actions/runs/38016928684)
fails only macOS matrix. Report retention gap recorded below;
fresh full sequential code reviews r6 PASS; verdicts below.
No current native PASS, causal diagnosis, merge/publication or default acceptance claimed.

Supervisor fetched actual current PR merge `9cca9cca2ae2a18eb300e033889e3699641978bb`, parents
`8d28dc1`/`0d0a338f`; empty tree diff against reviewed HEAD. Current canonical job114109157175;
matrix Windows114109157543/macOS114109157651/LinuxARM114109157682/Linuxx64114109157756.

## Full code review r6 — scoped crash evidence

Simplicity/style assignment02:27:13 UTC; observed **02:27:56–02:30:55 UTC**. Full42current paths,
binary diff/source identity independently matched, no previous PASS inherited. Source PASS,
no correction. Ownership assigned02:31:30 UTC after simplicity finished; observed
**02:32:05–02:33:27 UTC**, source/ownership PASS with no correction. Both reviewed the full
current42-path artifact independently. Neither verdict closes native or default acceptance.

| Simplicity check | Current verdict / evidence |
|---|---|
| New apps/libraries | PASS: maintained native verification probe, no dependency/host |
| Reuse | PASS: shared input policy/parser, existing build owner |
| Multiple paths | PASS: one interpreter/adapter, required OS lifetime implementations |
| Legacy paths | PASS: actual published Client ctor retained, old checkpoints refused |
| Knobs | PASS: fixed cleanup/report limits, no setting |
| Speculative abstractions | PASS: private native owner/live caller authority |
| Library choice | PASS: existing ONNX/BCL/built-in PowerShell |
| Copy-paste | PASS: shared exchange/fingerprint/admission; distinct selector/collector |
| Redundant definitions | PASS: existing StepKey/output convention |
| Size/requirement | PASS: exact approved42paths,2diagnostic edits |
| Test volume | PASS: real admission/replay/ABI/lifecycle/cancellation obligations |

| Code-style check | Current verdict / evidence |
|---|---|
| Progressive disclosure | PASS: public flow before mappings/exchange/native detail |
| Small functions | PASS: cohesive stages;46line collector narrow recorded exception |
| Top-down | PASS: build/adapter callers before helpers |
| Explicit errors | PASS: termination→pipejoins→reap, cleanup precedence; diagnostic failure cannot replace probe error |
| Shallow nesting | PASS: early guards, recorded per-report catch exception |
| Separate side effects | PASS: named launch/observation/collection/selection boundaries |
| Zero warnings | Managed Debug/Release PASS; current canonical/four-host native OPEN |
| Extract for reason | PASS: real ownership/parsing/failure duties, no metric-only third helper |
| Complexity | PASS: manual classic collector12/selector5/exchange8/POSIXjoin7/parserAddRow9/pipecancel10/childdispatcher12/retainedwalker13 |

Actual191focused/1063full+6existing live skips/407Release-package/consumer evidence read for
unchanged product tree; current parser/30script/23controlled logs read. Current native runs
confirmed in progress, not accepted. Both prior macOS failures remain unresolved; publication
and installed-default acceptance stay open.

Ownership derived owners before implementation inspection from the Desktop atlas and Core,
build and Runner READMEs. Complete current behavior verdicts:

| Behavior | Derived owner / actual placement | Verdict |
|---|---|---|
| Distribution-only metadata | Core manifest / ForgeTomlReader | PASS |
| One parser and retained local configuration | Core manifest / ForgeTomlReader | PASS |
| Explicit package assets | Core contracts / DurableMissionPackageInput | PASS |
| Published six-argument CLR constructor | Core contracts / retained overload | PASS |
| Generated JSON constructor selection | Core contracts / JsonConstructor | PASS |
| First-root and parameterless package construction | Core validator / TryCreate | PASS |
| Immutable validation without filesystem/config reads | Core validator / TryValidate | PASS |
| Immutable diagnostic source without disk fallback | Core expert loader / Validate | PASS |
| Declared root parameter string typing | Core expert loader / availability map | PASS |
| Reachable admitted input names | Core shared input policy | PASS |
| Exact reserved names with token_count retained | Core shared input policy | PASS |
| Required root versus optional expert inputs | Core validator | PASS |
| Actual reachable provider profiles | Core semantic validation | PASS |
| Environment and reserved-binding refusal | Core admission semantics | PASS |
| Existing generic expert kinds | Core admission semantics | PASS |
| Asset hashes/collisions/reserved directories | Core package validator | PASS |
| ONNX model-to-asset resolution | Core package validator | PASS |
| Extended hash with no-assets compatibility | Core package validator | PASS |
| Actual generated JSON size | Core package validator | PASS |
| Semantic fingerprint without scratch paths | Core interpreter / PipelineDefinitionFingerprint | PASS |
| Current checkpoint shape and old-format refusal | Core checkpoint codec | PASS |
| Admitted inputs and completed-effect replay | Core interpreter | PASS |
| Caller-owned live artifact registry | Caller/Runner registers; Core workspace consumes | PASS |
| Deterministic output-directory paths | Core workspace primitive | PASS |
| Nested runtime workspace inheritance | Core interpreter | PASS |
| Exact lifecycle/stream/tool StepKey | Core interpreter / trace contract | PASS |
| Process-local registered-path mapping | Core exec adapter | PASS |
| Runtime directories and current artifact aliases | Core exec adapter | PASS |
| Expert cwd and literal arguments | Core exec adapter / argument helper | PASS |
| Concurrent bounded three-stream I/O | Core exec adapter | PASS |
| Concrete declined-stdin errors | Core exec adapter | PASS |
| Output/status/reason and explicit JSON failures | Core exec adapter | PASS |
| Bare Linux PID1 refusal before allocation | Core exec start boundary | PASS |
| Atomic POSIX group and retained root | Core private exec lifetime | PASS |
| macOS retained-root-only group observation | Core private POSIX lifetime | PASS |
| Atomic Windows job/handle association | Core private exec lifetime | PASS |
| Terminate, join I/O, exactly reap/dispose | Core exec lifetime | PASS |
| Cleanup IOException precedence/context | Core exec failure boundary | PASS |
| Adopted container orphan reaping | Existing Runner init; no Core global reaper | PASS |
| Numeric ONNX and joined native cancellation | Core ONNX adapter | PASS |
| Unfinished native work and actual sibling launch | Core adapter tests | PASS |
| Native pipe cancellation and duplex pressure | Core native probe | PASS |
| Actual image topology and separate PID1 refusal | Build verification / native probe | PASS |
| Failed macOS invocation diagnostic collection | Existing build verification owner | PASS |
| Exact identities/current launch report selection | Build diagnostic selector | PASS |
| Bounded polling/no cwd fallback/copy once | Build diagnostic collector | PASS |
| Collection errors preserve original probe error | Build invocation/collector boundary | PASS |
| Immutable publication/source verification | Existing package/release owners | PASS |
| Actual published Client ABI verification | Existing ForgeProjectTests | PASS |

| Technical gate | Current observation/verdict |
|---|---|
| Security/data/credentials | PASS: no endpoint/store/identity/credential authority added; inherited process authority is explicit; only validated probe reports collected |
| API/ABI/generated JSON | PASS: retained ctor/generated ctor and actual published Client/package round trips |
| Failure containment | PASS: checked terminate→joined I/O→exact reap; collection failure cannot replace probe failure |
| Diagnostic verification | PASS: actual parser/30script/23controlled observations, including write failure/empty roots/original error |
| Production verification | Supporting191focused/1063full+6existing live skips/407Release-package/consumer evidence inspected against identical production tree |
| Current Native AOT | OPEN: canonical/Linux/Windows pass, matrix macOS SIGSEGV repeats; cause unobserved |
| Normal publication/fresh restore | OPEN; branch package cannot close it |
| Installed defaults | OPEN: normal Project Chat and Hands still required |
| UI | N/A: no visual/layout change |

Named-repository searches found no competing validator, adapter, native lifetime or report collector.
Consumer calls are reuse. No owner gains a second job. Reviewer recommendation: move nothing.

Supervisor independently rechecked final source/tree/diff identity and the actual collector,
selector, invocation boundary and controlled raw logs after both reviews. Clean source is pushed
0/0; exact full binary diff hash matches both reviewers. Both current source reviews accepted;
no finding requires a correction. This is source acceptance only: native recurrence remains
unexplained, so merge/publication/default readiness is explicitly withheld. Full text whitespace
check excludes only the already recorded ONNX binary heuristic; fixture identities remain checked.

### Current r10 native observations

All use synthetic checkout `9cca9cca2ae2a18eb300e033889e3699641978bb`, with the reviewed tree.
Supervisor fetched each completed job's actual raw log while the macOS jobs remained running.

| Current job | Result / actual observations |
|---|---|
| Linux x64,114109157756 | SUCCESS02:39:59 UTC; zero compiler warnings,30scripts/native identity/blocked read-write0ms/pressure/workspace/argv/cwd/PATH/exited descendant/timeout/cancel lifecycle PASS |
| Linux ARM64,114109157682 | SUCCESS02:41:38 UTC; zero compiler warnings,30scripts/native identity/blocked read-write0ms/pressure/workspace/argv/cwd/PATH/exited descendant/timeout/cancel lifecycle PASS |
| Windows ARM64,114109157543 | SUCCESS02:42:16 UTC; zero compiler warnings,30scripts/native identity/blocked read3ms-write0ms/pressure/workspace/argv/cwd/PATH/exited descendant/timeout/cancel lifecycle PASS |
| macOS matrix,114109157651 | FAILED; pressure child SIGSEGV13902:49:54 UTC, outer probe abort134. Collector reports two matching reports copied02:50:00. Matrix failure artifact unavailable by existing design; canonical retained artifact pending |
| Canonical macOS,114109157175 | SUCCESS:1055managed PASS/10existing skips/0failed, zero compiler warnings; all native probe assertions PASS02:50:40–02:50:47; terminal27tests/package verification PASS. Failure-only collector correctly has no reports |

Current x64 additionally observed Runner index `c0031d451d046d4f28a75ca7f7c7f26169b26e92555de4b601128a005670331c`,
amd64 `cf2a7e14f639519af223dd1efa0a2adf4d40cf1cac5f1b7d2017c5860ca1078b`,
image source `17080b73a84ad0b0e42a891024090e8f997194ed`, unchanged tini entrypoint.
Actual topology02:39:36 UTC: tiniPID1/dotnetRunnerPID7/nativeprobePID14. Both under-init
completion/cancellation pressure and lifecycle checks PASS, with separate bounded orphan process
entry disappearance02:39:37/02:39:40. Bare PID1 refusal before child/root/sentinel PASS02:39:42.
This component proof does not close later unified cloud acceptance.
Raw logs: `/private/tmp/phase76-core-r10-linux-x64.log`,
`/private/tmp/phase76-core-r10-linux-arm64.log`, `/private/tmp/phase76-core-r10-windows-arm64.log`,
`/private/tmp/phase76-core-r10-macos-arm64.log`.

Canonical always-upload artifact11657526336 is151,674,861bytes; supervisor inspected ZIP inventory
and extracted raw source/native-publish/probe-publish/probe-run logs via bounded ranges at
`/private/tmp/phase76-core-r10-canonical-range-logs`. No crash reports/status file exist, as expected
for success. Executable/dSYM/native sidecar paths are confirmed in the actual archive. Earlier
failed matrix reports cannot be recovered through its successful-ZIP-only artifact route.

Read-only investigation r12 observed02:51:56–02:52:37 UTC. Same-tree canonical success and matrix
failure establish recurrence, not a causal diagnosis. Child139 and parent134 are distinct failures;
actual child faulting thread/PC/image UUID/symbolication remain missing. No production correction
or failed-check waiver is justified. Evidence:
`/private/tmp/phase76-core-native-r12-20261010T025156Z/EVIDENCE.md`.
Supervisor acknowledges that failure retention should have covered both macOS routes initially.
The next bounded plan retains the matrix's existing scoped diagnostic outputs; no native fix is
guessed from successful local repetitions or another successful sibling.

### Current diagnostic revision stage boundaries

Assignment start/end are used where recorded; observed agent activity is explicitly distinguished.
Earlier Core stage tables remain below. ProductPR77 remains open, so end-to-end merge span and
post-publication acceptance are open. Tokens N/A; no agent context size is treated as usage.

| Stage / role / round | Start UTC,2026-10-10 | End UTC | Wall | Evidence |
|---|---|---|---|---|
| `[plan:implementer:r10]` | Assignment unavailable; observed02:09:58 | 02:11:28 | Assignment duration unavailable | Complete current plan; observed activity1m30s |
| `[review-plan:simplicity:r10]` | 02:14:20 | 02:15:37 | 1m17s | Full11 checks PASS |
| `[review-plan:ownership:r10]` | 02:15:54 | 02:18:21 | 2m27s | Full42 behavior placements and technical gates PASS |
| `[approval:supervisor:r10]` | 02:19:30 | 02:19:30 | Boundary | Explicit bounded PLAN APPROVED |
| `[implement:implementer:r10]` | 02:19:30 | 02:27:00 | 7m30s | Observed activity02:20:11–02:27:00; frozen0d0a338f/parser30scripts23controlled |
| `[review-code:simplicity/style:r6]` | 02:27:13 | 02:30:55 | 3m42s | Full42 paths; separate11simplicity/9style verdicts |
| `[review-code:ownership:r6]` | 02:31:30 | 02:33:27 | 1m57s | Full49 behavior placements/technical gates PASS |

## Complete round 10 plan reviews — crash evidence

Same implementer read-only plan observed02:09:58–02:11:28 UTC; full42-path producer scope
retained, proposed diagnostic delta only in build script/README. Simplicity assignment02:14:20;
observed review **02:14:50–02:15:37 UTC**. Whole current plan independently reviewed; no earlier
PASS inherited. Ownership assigned02:15:54 UTC after simplicity finished; observed full review
**02:16:27–02:18:21 UTC**, all42behavior placements PASS. Owners derived blind from atlas,
Core/build/Runner READMEs before reading the full plan. No duplicate owner or second component job.

| Simplicity check | Current verdict / evidence |
|---|---|
| New apps/libraries | PASS: two private helpers, built-in PowerShell/.NET APIs |
| Reuse | PASS: existing invocation/failure log/canonical always-upload |
| Multiple paths | PASS: failed macOS only, no retry/alternative execution |
| Legacy paths | PASS: observed ips only; retained constructor serves actual published Client ABI |
| Knobs | PASS: fixed directories, identity, launch-time selection and bounded polling |
| Speculative abstractions | PASS: actual validation and filesystem duties separated |
| Library choice | PASS: built-in JSON/date/files; retained ONNX/native choices unchanged |
| Copy-paste | PASS: one collector; shared producer owners retained |
| Redundant definitions | PASS: collection cannot replace original failure or warning/runtime policy |
| Size/requirement | PASS: same42paths, only2diagnostic edits; reassess retention after diagnosis |
| Test volume | PASS: precise selection/bounded copying/failure-preservation checks; required defaults retained |

Reviewer independently read actual local separate header/body JSON with exact process and offset
launch fields. No service/identity/datastore/authority boundary changes. Both macOS failures remain
open; diagnostic plan readiness does not close native/publication/default gates.

| Behavior | Fresh derived owner / plan placement | Verdict |
|---|---|---|
| Distribution before environment evaluation | Core manifest/shared parser | PASS |
| Explicit assets/executable metadata | Core package contracts | PASS |
| Published six-argument ABI | Core contract overload | PASS |
| Generated JSON constructor | Core annotated semantic constructor | PASS |
| Pure package validation | Core validator/supplied diagnostics | PASS |
| Root including parameterless | Core TryCreate | PASS |
| Reachable inputs/profiles | Core traversal | PASS |
| Required/optional inputs | Core shared input policy | PASS |
| Environment/reserved-name refusal | Core admission | PASS |
| token_count/string parameters | Core validator/interpreter | PASS |
| Canonical paths/collisions/runtime reservations | Core validator | PASS |
| Digests/model resolution | Core validator | PASS |
| Hash extension/old identity | Core validator | PASS |
| Actual4MiB JSON | Core validator | PASS |
| Semantic fingerprint | Core replay | PASS |
| Checkpoint/old format rejection | Core codec | PASS |
| Relative inputs/completed effects | Core replay | PASS |
| Child workspace inheritance | Core runtime | PASS |
| Artifact registration | Caller/Runner; Core retains live map only | PASS |
| Output path calculation | Core pure workspace | PASS |
| Exact StepKey | Core trace/interpreter | PASS |
| Process-local mappings | Core exec | PASS |
| cwd/lookup/literal argv | Core exec | PASS |
| Bounded concurrent pipes | Core exec | PASS |
| Concrete declined stdin | Core exec | PASS |
| Atomic group/direct-root retention | Core POSIX lifecycle | PASS |
| macOS root-only proof | Core POSIX lifecycle | PASS |
| Atomic Windows job | Core Windows lifecycle | PASS |
| Bare PID1 prelaunch refusal | Core start boundary | PASS |
| Adopted orphan reap | Existing Runner init exclusively | PASS |
| Terminate/stream join/exact reap | Core lifecycle | PASS |
| Numeric/joined ONNX cancellation | Core ONNX | PASS |
| Pipe/pressure/workspace proof | Existing probe/build | PASS |
| Exact-image and separate negative | Existing build/Runner image | PASS |
| Failure-only macOS collection | Existing build boundary | PASS |
| Exact identity/current launch selector | Private build selector | PASS |
| Bounded validated copy | Private build collector | PASS |
| Original failure preservation | Existing build invocation | PASS |
| Evidence retention | Existing canonical always-upload | PASS |
| Meaningful controlled verification | Existing script verification/scratch | PASS |
| Immutable publication/restore/ABI | Existing package/release/supervisor | PASS |
| Installed Chat/Hands acceptance | Supervisor/default product | PASS |

| Gate | Fresh assessment |
|---|---|
| Security/data/credentials | PASS: scoped validated probe reports only, no hosted boundary change |
| API/ABI | PASS: complete producer shapes/JSON/actual published ABI defined |
| Engineering/failure | PASS: joined ownership, fixed conventions, no retry; original failure preserved |
| Scope/dependencies | PASS: same42paths,2diagnostic edits; existing ONNX/image |
| Native | OPEN: both macOS SIGSEGV failures unresolved |
| Diagnostic proof | OPEN pending implementation/actual crash stack |
| Publication/default | OPEN: normal merged publication/fresh cache/installed Chat and Hands required |
| UI | N/A |

Supervisor approval **02:19:30 UTC**: full current plan, scope and actual report/build/artifact
contract checked independently. No product correction justified; only diagnostic paths41–42
authorized. Next normal CI must supply causal evidence; no failure waiver.

## Approved Windows expectation correction — implementation r9

Same implementer stage **2026-10-10 01:37:02–01:38:24 UTC**. Frozen/pushed clean source
`5a80c56e690c2a1d6e51f4ea24e797b70f32d28c`, existing draft PR77. Only one expected-path
line changed in `tests/ForgeMission.Exec.Probe/Program.cs`; canonical separators convert to
the OS separator before combining the assertion's absolute path. The caller-relative-value
assertion, all payloads/deadlines and every production `src` byte remain unchanged.

Supervisor independently checked empty production diff, full42-path diff SHA256
`38CFF7AA3E51AC11988A98D2EFF4F524F5F485941F7C3E7A937EE4E96D2CAFCE`, clean push,
zero-warning managed probe build, all corrected managed probe cases and30script checks.
Open-peer blocked read/write cancellation observed6ms/1ms. Complete current inventory/raw logs:
`/private/tmp/phase76-core-windows-expectation-20261010T013702Z/EVIDENCE.md`.
Previous full managed/package/consumer checks support unchanged production sources; current
native [canonical38013941959](https://github.com/katasec/forge-mcl/actions/runs/38013941959) and
[four-host38013941972](https://github.com/katasec/forge-mcl/actions/runs/38013941972) are queued.
Fresh full sequential code reviews r5 are required, with simplicity/style running first.
No local full AOT repeated; no speculative macOS fix, deadline change or gate waiver.

Supervisor fetched current PR merge ref `f8696c714ae83e9fb2506884ec3d8d0f2cdcbb33`:
parents `8d28dc1` and `5a80c56e`, empty tree diff against reviewed HEAD. Current native jobs:
canonical `114099979075`; macOS `114099979637`; Windows ARM64 `114099979764`;
Linux x64 `114099979796`; Linux ARM64 `114099979935`. These are running, not accepted observations.
Documentation tracking [draft PR380](https://github.com/katasec/mission-control-language/pull/380)
remains open until the Core task's required native/publication/default gates close.

## Full code review r5 — portable expectation

Simplicity/style observed **2026-10-10 01:39:27–01:40:45 UTC**. Exact assignment timestamp
was not separately captured; no duration is reconstructed. All42 current paths at `5a80c56e`
reviewed afresh against `8d28dc1`, full diff hash above matched. Source PASS, no correction.

| Simplicity check | Current verdict / evidence |
|---|---|
| New apps/libraries | PASS: no dependency/host added; private ExecProcess owner |
| Reuse | PASS: existing TOML parser and pipeline extended |
| Multiple paths | PASS: one exchange with required OS lifetime branches |
| Legacy paths | PASS: six-argument member serves actual published Client; generated JSON selects primary constructor |
| Knobs | PASS: fixed cleanup/PID1 policy, no new setting |
| Speculative abstractions | PASS: real native resource boundary; live caller registry retained |
| Library choice | PASS: existing ONNX Runtime inference/cancellation |
| Copy-paste | PASS: shared admission/replay policy and single fingerprint encoder |
| Redundant definitions | PASS: asset/admission/workspace values have distinct roles |
| Size/requirement | PASS: complete approved42-path producer scope |
| Test volume | PASS: required public/failure/ABI facts; corrected expectation preserves payload and relative assertion |

| Code-style check | Current verdict / evidence |
|---|---|
| Progressive disclosure | PASS: adapter flow precedes mappings/exchange/result detail |
| Small functions | PASS: cohesive new stages; retained pipeline bodies receive narrow threading changes |
| Top-down | PASS: parser entry points precede dispatch/value helpers |
| Explicit errors | PASS: checked termination → joined I/O → exact reap; cleanup error precedence retained |
| Shallow nesting | PASS: staged parsing/early native and shape guards |
| Separate side effects | PASS: pure package/fingerprint decisions separate from native lifetime |
| Zero warnings | Managed/probe logs PASS; fresh Native AOT OPEN |
| Extract for reason | PASS: real parsing/resource/failure boundaries |
| Complexity | PASS: manual classic exchange8, POSIXjoin7, row dispatch9, pipe proof10; retained walker13/dispatcher12 |

Reviewer read actual current probe logs, prior unchanged-product191focused/1063full+6skips/
407Release-package/30scripts and fresh branch consumer. Current full managed probe cancellation
6ms/1ms and all pressure/workspace/lifecycle cases pass. Current native runs remain open;
earlier unexplained macOS crash is not waived. Ownership assigned **01:41:11 UTC** after
simplicity/style completed. Ownership observed **01:41:52–01:43:19 UTC**: full current source
PASS, no correction. Owners rederived blind from Desktop atlas and Core/build READMEs before
comparing the actual complete diff. The following paths are relative to forge-mcl; Core paths
below use `src/ForgeMission.Core/`.

| Behavior | Derived owner / current placement | Verdict |
|---|---|---|
| Explicit asset declarations | Core manifest; Manifest/ForgeManifest.cs:5 | PASS |
| Distribution selection before environment evaluation | Core manifest; ForgeTomlReader.cs:57 | PASS |
| Full local parsing/diagnostics | Core manifest; ForgeTomlReader.cs:15 | PASS |
| Construct resolved package | Core admission; DurableMissionPackageValidator.cs:19 | PASS |
| Validate without local/config/credential lookup | Core admission; validator:39 | PASS |
| First mission/primary input/parameterless root | Core semantics; validator:26 | PASS |
| Existing expert kinds | Core semantics; validator:142 | PASS |
| Reachable profiles without deployment availability policy | Core reports/Runner supplies; validator:58 | PASS |
| Internally derive reachable names | Core input policy; DurableMissionInputPolicy.cs:9 | PASS |
| Reserved/duplicate/undeclared/missing root rejection | Core admission; validator:69 | PASS |
| token_count/named-over-let values | Core execution; PipelineRunner.cs:763 | PASS |
| Reject authored environment expressions/reserved bindings | Core semantics; validator:149 | PASS |
| Asset digest/executable/path collision validation | Core admission; validator:164 | PASS |
| ONNX models within admitted assets | Core admission; validator:184 | PASS |
| Old no-assets hash/extended asset identity | Core identity; validator:84 | PASS |
| Actual generated JSON4MiB budget | Core admission; validator:47 | PASS |
| Actual published six-argument CLR member | Core contract; validator:215 | PASS |
| Generated JSON semantic constructor | Core contract; validator:210 | PASS |
| Immutable diagnostic source/no disk fallback | Core loader; ExpertLoader.cs:273 | PASS |
| Declared parameters recognized as strings | Core loader; ExpertLoader.cs:220 | PASS |
| Complete fingerprint/excluded scratch identity | Core replay identity; PipelineDefinitionFingerprint.cs:12 | PASS |
| Malformed consumed checkpoints/old format rejection | Core codec; PipelineToolPause.cs:103 | PASS |
| Relative inputs/completed effects across resume | Core replay; PipelineRunner.cs:77 | PASS |
| Stable lifecycle StepKey | Core trace; PipelineTraceEvent.cs:21/PipelineRunner.cs:172 | PASS |
| Live caller registry | Caller registers/Core consumes; PipelineExecutionWorkspace.cs:16 | PASS |
| Pure deterministic output directory | Core workspace; workspace:22 | PASS |
| Nested workspace inheritance | Core execution; PipelineRunner.cs:633 | PASS |
| Process-local verified path mapping | Core exec; ExecExpertRunner.cs:71 | PASS |
| Current runtime aliases/stale alias removal | Core exec; runner:110 | PASS |
| Command/literal argv/cwd | Core exec; ExecProcessArguments.cs:8 | PASS |
| Bare Linux PID1 refusal before allocation/spawn | Core lifecycle; ExecProcess.cs:28 | PASS |
| Atomic POSIX group/direct-root retention | Core lifecycle; ExecProcess.Posix.cs:15 | PASS |
| macOS root-only completion before reap | Core lifecycle; POSIX:88 | PASS |
| Preexecution Windows job ownership | Core lifecycle; ExecProcess.Windows.cs:14 | PASS |
| Concurrent bounded stdin/stdout/stderr | Core exec; runner:127 | PASS |
| Exact declined-stdin classifications | Core exec; runner:184 | PASS |
| JSON/output/status/reason/judge semantics | Core exec; runner:210 | PASS |
| Terminate/join I/O/reap/dispose | Core lifecycle; runner:143 | PASS |
| Cleanup IOException precedence | Core failure boundary; runner:164 | PASS |
| Adopted-orphan reap | Existing Runner init; no Core scanner/reaper; probe:277 observes separately | PASS |
| Numeric ONNX/parallel sibling launch | Core ONNX; OnnxExpertRunner.cs:11 | PASS |
| Joined native cancellation/disposal | Core ONNX; runner:19 | PASS |
| Open-peer BCL cancellation proof/no abandoned work | Existing probe; Program.cs:37 | PASS source/local |
| Decisive duplex pressure proof | Existing probe; ExecProbeChild.cs:58 | PASS source/local |
| Native expected path/relative caller assertion | Existing probe; Program.cs:186/176 | PASS |
| Unchanged published-image entrypoint/separate PID1 negative | Existing build owner; scripts/build.ps1:44 | PASS placement/native OPEN |
| Actual retained Client ABI verification | Existing integration tests; ForgeProjectTests.cs:17 | PASS |
| Immutable publication/source/dependencies | Existing Core publication/verifier | PASS placement/publication OPEN |

No owner acquires a second job; no new credential, datastore, provider-construction or policy
authority. Fresh search across named Forge repositories found one production exec, ONNX and
package validator, and no Core adopted-child/global-subreaper/waitpid(-1) path. Move nothing.

| Technical gate | Current observation |
|---|---|
| Artifact/scope | Clean frozen HEAD/full hash independently matched |
| Production identity | Both src trees `6c7cfd841a81fd60ad0fd3d7213ae75e4bd202f9` |
| API/ABI/admission/hash/JSON/replay/trace/live registry | PASS |
| Current probe build/all assertions/scripts | Zero warnings; read/write6ms/1ms/all cases/30checks PASS |
| Unchanged production evidence | Actual191focused/1063full+6skips/407Release-package, retained Client ABI and fresh branch consumer inspected |
| Branch package | Supporting source8f0 identity only; no normal publication claim |
| Security/engineering ownership | PASS; arbitrary-code policy and joined failures retained |
| Current canonical/four-host native | OPEN; earlier unexplained SIGSEGV not waived |
| Normal publication/fresh published restore | OPEN |
| Installed Project/Chat/Hands defaults | OPEN |
| UI | N/A |

Supervisor accepts both full current source reviews **2026-10-10 01:44:09 UTC**, after independently
checking the actual correction, byte-identical production tree, complete hash and raw evidence.
No source correction requested. Required native/publication/default observations still govern readiness.

## Current native verification — source 5a80c56e

### Recurrent macOS crash — investigation r11

Observed read-only stage **2026-10-10 02:06:54–02:09:38 UTC**. Exact previous failed executable
passed40direct pressure runs and4complete public probes locally. Exact current failed executable
passed direct pressure and a complete public probe locally. JSON contains1,000,000output
characters and exactly64KiB stderr; failures were not retried into acceptance. Local macOS27
success does not close the failed macOS14 gate. Existing local DiagnosticReports contain earlier
managed SIGABRTs, no matching native SIGSEGV. No product edit or causal conclusion.

Supervisor independently read the current canonical raw artifact log: signal11/exit139, distinct
from termination signal9/exit137. The current artifacts preserve executable and symbols but lack
the child crashreport/faulting stack. The same implementer is preparing a diagnostic-only plan
for the existing native build boundary to collect new probe crashreports into the already-uploaded
destination, preserving the failing result. No retry, payload/deadline change, new workflow or
production workaround. Existing locked design applies; diagnostic evidence adds no runtime,
identity/data boundary or UI behavior. Plan approval remains open until fresh sequential reviews.
Raw reproduction and evidence: `/private/tmp/phase76-native-crash-20261010T020654Z/EVIDENCE.md`.

Four-host run38013941972 actually checks out `f8696c714ae83e9fb2506884ec3d8d0f2cdcbb33`,
the synthetic merge whose tree matches reviewed `5a80c56e`. Both completed Linux jobs print
native CLI identity `0.10.1-dev.6+f8696c714ae83e9fb2506884ec3d8d0f2cdcbb33`.

| Gate | Current observation |
|---|---|
| Linux x64 | Job114099979796 SUCCESS,12m27s; complete public native probe PASS, read/write cancellation0ms/0ms; pressure/workspace/argv/lifecycle checks pass |
| Linux ARM64 | Job114099979935 SUCCESS,13m5s; same complete public native probe PASS, cancellation0ms/0ms |
| Exact published Runner image | Same immutable0.20.6 index `c0031d…670331c`, source17080b73; unchanged tini entrypoint, actual tiniPID1/dotnetPID8/probePID15 observed01:50:26 UTC |
| Under-init lifecycle | All native assertions PASS; orphan entries disappear01:50:27 and01:50:30, separately from no-late-sentinel timeout/cancel checks |
| Bare PID1 negative | Refused before child/root/sentinel creation01:50:32 UTC; owned test container removed |
| Linux zero warnings | Supervisor read both completed-job logs; no compiler/linker/ILC/trim warning match |
| Windows ARM64 | Job114099979764 SUCCESS,18m6s; corrected workspace assertion and all later native lifecycle checks pass; blocked read/write3ms/0ms; exact f8696c7 CLI/source identity |
| Windows warning gate | No compiler/linker/ILC/trim warnings; the separate Git LF-to-CRLF notice is not a compiler diagnostic |
| macOS matrix | Job114099979637 FAILED,26m27s; blocked-pipe2ms/0ms PASS, then pressure child exits139(SIGSEGV)02:04:42; later assertions not reached |
| Canonical macOS | Job114099979075 FAILED; managed1055PASS/10existing skips/0fail, nativepipe0ms/0ms PASS, then same pressure-child SIGSEGV02:06:16; artifact11655851471 retained |

Actual raw logs `/private/tmp/phase76-core-r9-linux-x64.log` and
`/private/tmp/phase76-core-r9-linux-arm64.log` and
`/private/tmp/phase76-core-r9-windows-arm64.log`,
`/private/tmp/phase76-core-r9-macos-arm64.log` and `/private/tmp/phase76-core-r9-canonical.log`.
The repeated macOS failure is not waived. Read-only investigation r11 compares the child/native
runtime behavior and exact binaries; no speculative correction is authorized. Normal publication
and installed-default acceptance remain open.

## Approved pipe correction — implementation r8

Same implementer stage **2026-10-10 01:03:13–01:08:43 UTC** (5m30s). Frozen and pushed source
`8f0fa851b439d45af4cdd68bb4aed06f257096e8`, clean branch, existing
[PR77](https://github.com/katasec/forge-mcl/pull/77). Three existing approved paths changed:
`ExecExpertRunner.cs` joins final pipe work before exact native join/reap;
`tests/ForgeMission.Exec.Probe/Program.cs` proves blocked read/filled write cancellation with
opposite endpoints open and joins the underlying operation on failure; `scripts/README.md`
documents that proof. No new product boundary, dependency, option, API or file.

Complete current 42-file diff against `8d28dc1` has 2618 insertions / 457 deletions, SHA256
`C7842F9664EBB8FB7FCA51E5E08D53EEF4CBAE051CFCB74360A93BBA5D4078BC`.
Full per-file inventory, commands and raw observations:
`/private/tmp/phase76-core-pipes-20261010T010313Z/EVIDENCE.md`.

| Gate | Current observation |
|---|---|
| Debug solution / Core Release builds | Zero warnings and errors |
| Focused eight families | 191 passed, no failures/skips; actual published Client 0.9.3 ABI exercised |
| Full unfiltered Debug | 1063 passed, no failures, six existing live skips; 2m20s; all tests finished |
| Release package / script checks | 407 passed / 30 checks passed; normal package metadata and dependencies verified |
| Managed public probe | Open-peer blocked read cancelled in 4ms and filled write in 0ms; retained pressure/workspace/argv/timeout/cancellation/lifecycle checks pass |
| Final-source branch package | Exact source `8f0fa851`; built/restored SHA256 `D549E8EF2C1A68AD4204A5E9D51A0B65889DFF53402DD4335566DFB36A077BB1` match |
| Fresh isolated branch consumer | Generated JSON, asset metadata/hash/admission, packaged exec/runtime/output bytes, exact StepKey, numeric ONNX, actual joined native cancellation/no score, six-argument constructor and no-assets old hash pass |
| Current-source native gates | Canonical [38011993998](https://github.com/katasec/forge-mcl/actions/runs/38011993998) FAILED; four-host [38011993926](https://github.com/katasec/forge-mcl/actions/runs/38011993926): Linux both and macOS PASS, Windows FAILED |

Supervisor independently read the complete three-path correction, raw full-test/probe/consumer/
pack logs and unchanged 42-file inventory. This is controlled branch-package evidence;
published package and installed defaults remain mandatory. Full current simplicity/style r4
assigned **01:09:43 UTC**; ownership follows sequentially. Earlier native runs were cancelled as
superseded source, not accepted. No merge or publication yet.

## Full code review r4 — pipe correction

Full simplicity/style assignment **01:09:43 UTC**, observed review **01:10:10–01:12:50 UTC**,
result available **01:13:10 UTC**, 2026-10-10. All 42 current paths at `8f0fa851` reviewed against
`8d28dc1`; complete diff hash independently matched. No earlier PASS inherited.

| Simplicity check | Current verdict / evidence |
|---|---|
| New apps/libraries | PASS: no dependency added; private native owner remains in Core |
| Reuse | PASS: existing TOML parser, interpreter, checkpoints and trace path |
| Multiple paths | PASS: one exchange; OS branches implement required native lifetime differences |
| Legacy paths | PASS: six-argument overload serves actual published Client; generated JSON selects semantic constructor |
| Knobs | PASS: fixed cleanup budget and prelaunch PID1 refusal; no operator setting |
| Speculative abstractions | PASS: real native lifetime boundary; workspace retains caller-owned live registry |
| Library choice | PASS: existing ONNX Runtime; joined native work before disposal |
| Copy-paste | PASS: shared admission, fingerprint, arguments and exchange |
| Redundant definitions | PASS: asset metadata and input policy have distinct roles |
| Size/requirement | PASS: entire approved 42-path producer scope; principal expansion is required native lifetime |
| Test volume | PASS: actual ABI, malformed checkpoint, concurrent I/O and native cancellation observations |

| Code-style check | Current verdict / evidence |
|---|---|
| Progressive disclosure | PASS: adapter entry flow precedes implementation helpers |
| Small functions | PASS: corrected exchange 29 lines, named coherent operations |
| Top-down order | PASS: parser entry points precede dispatch/value helpers |
| Explicit errors | PASS: final pipe join before exact native reap; checked cleanup failures retain precedence |
| Shallow nesting | PASS: early validation and staged parser dispatch |
| Separate side effects | PASS: pure package/workspace/fingerprint separate from native operations |
| Zero warnings | Managed Debug/Release logs PASS; current-source Native AOT evidence OPEN |
| Extract for reason | PASS: resource/failure boundaries and coherent parsing stages |
| Complexity | PASS: manual classic Exchange8, POSIX Join7, AddRow9, pipe proof10; retained walker13, dispatcher12; none newly introduced above15 |

No code correction requested. Actual blocked-pipe proof observes `IsCanceled` before peer disposal;
failure cleanup awaits the underlying operation. Raw logs independently checked: 191 focused,
1063 full with six existing skips, 407 Release/package, 30 scripts and final-source branch consumer.
Ownership r4 assigned **01:13:10 UTC** after simplicity/style completed; observed review
**01:13:36–01:15:57 UTC**, 2026-10-10. Full current source/ownership PASS. Owners derived again
from Desktop atlas, Core/build READMEs and verified Runner-init ownership before comparison.
The following paths/lines are in `/Users/ameerdeen/progs/forge-mcl`; Core runtime paths use
`src/ForgeMission.Core/` below.

| Behavior | Derived owner / actual placement | Verdict |
|---|---|---|
| Literal distribution metadata | Core manifest; Manifest/ForgeTomlReader.cs:17 | PASS |
| Full local parsing/diagnostics | Core manifest; Manifest/ForgeTomlReader.cs:44 | PASS |
| Asset declarations | Core manifest; Manifest/ForgeManifest.cs | PASS |
| Construct resolved package | Core admission; Runtime/DurableMissionPackageValidator.cs:19 | PASS |
| Pure package validation | Core admission; validator:39 | PASS |
| Existing kinds/reachable profiles | Core admission; validator:142 | PASS |
| Derive admitted names | Core policy; Runtime/DurableMissionInputPolicy.cs:10 | PASS |
| Required roots/reserved/duplicate names | Core admission; validator:69 | PASS |
| Parameterless roots/named-over-let/token_count | Core execution; Runtime/PipelineRunner.cs:763 | PASS |
| Asset bytes/digest/executable/collisions | Core admission; validator:164 | PASS |
| ONNX model resolves to admitted asset | Core admission; validator:164 | PASS |
| Preserve old hash/extend asset identity | Core identity; validator:84 | PASS |
| Actual JSON 4 MiB cap | Core admission; validator:47 | PASS |
| Published six-argument CLR constructor | Core contract; validator:215 | PASS |
| Generated JSON constructor | Core contract; validator:210 | PASS |
| Immutable expert source/string parameters | Core loader; Experts/ExpertLoader.cs:75,221 | PASS |
| Full semantic replay fingerprint | Core identity; Runtime/PipelineDefinitionFingerprint.cs | PASS |
| Malformed checkpoint refusal | Core codec; Runtime/PipelineToolPause.cs:103 | PASS |
| Relative inputs/completed effects across resume | Core replay; PipelineRunner.cs:77 | PASS |
| Exact lifecycle StepKey | Core trace; Runtime/PipelineTraceEvent.cs:21, PipelineRunner.cs:172 | PASS |
| Live caller registry | Caller registers, Core consumes; Runtime/PipelineExecutionWorkspace.cs:16 | PASS |
| Deterministic output directory | Core primitive; workspace:22 | PASS |
| Inherit nested workspace | Core execution; PipelineRunner.cs:633 | PASS |
| Process-local verified path mapping | Core exec; Adapters/ExecExpertRunner.cs:71 | PASS |
| Current runtime environment aliases | Core exec; runner:110 | PASS |
| Command/literal argv/cwd | Core exec; Adapters/ExecProcessArguments.cs | PASS |
| Bare Linux PID1 preallocation refusal | Core lifecycle; Adapters/ExecProcess.cs:28 | PASS |
| Atomic POSIX group/direct-root retention | Core lifecycle; Adapters/ExecProcess.Posix.cs | PASS |
| macOS root-only completion before reap | Core lifecycle; POSIX:88 | PASS |
| Atomic Windows job association | Core lifecycle; Adapters/ExecProcess.Windows.cs | PASS |
| Concurrent bounded duplex exchange | Core exec; runner:127 | PASS |
| Exact declined-input errors | Core exec; runner:174 | PASS |
| Terminate → join pipes → exact reap/dispose | Core lifecycle; runner:143 | PASS |
| Cleanup IOException/caller cancellation precedence | Core failure boundary; runner:164 | PASS |
| Adopted-orphan reaping | Runner-image init; Core scan/import removed | PASS |
| Numeric ONNX/parallel sibling launch | Core ONNX; Adapters/OnnxExpertRunner.cs:11 | PASS |
| Joined native cancellation/disposal | Core ONNX; runner:19 | PASS |
| Open-peer blocked read/write proof | Existing probe; tests/ForgeMission.Exec.Probe/Program.cs:37 | PASS placement/local; native OPEN |
| Decisive pressure/public workspace proof | Existing probe; ExecProbeChild.cs:58, Program.cs:152 | PASS |
| Exact-image topology/separate PID1 negative | Existing build owner; scripts/build.ps1:44 | PASS placement; execution OPEN |
| Immutable package/source/dependencies | Existing publication owner; publish-core-package.yml:20, eng/verify-core-package.sh:5 | PASS placement; publication OPEN |
| Actual retained Client ABI | Existing integration tests; tests/ForgeMission.Mcl.Tests/Cli/ForgeProjectTests.cs:17 | PASS |

No component acquires a second job. Duplicate search across named Forge repos found one production
exec adapter, ONNX adapter and package validator; no adopted-child/global subreaper path remains
in Core. Private native helpers do not acquire Desktop supervision, deployment, datastore,
credentials or artifact-registration ownership. Move nothing.

| Technical gate | Current verdict |
|---|---|
| Contracts/JSON/ABI/hash/admission/replay/workspace/trace | PASS |
| Supported BCL cancellation/held-root lifetime | PASS source/local; actual underlying task joined even on probe failure |
| Security/failure ownership | PASS; existing operator policy, no public/data/identity expansion |
| Managed/package evidence | Raw191/1063+6skips/407/30,0managed warnings; exact final-source package/cache identity PASS |
| Final Native AOT zero warnings | OPEN, current canonical/four-host jobs |
| Exact-image positive/bare-PID1 negative | OPEN |
| Normal publication/fresh normal restore/installed defaults | OPEN |
| UI | N/A |

Supervisor accepts both source reviews with no correction required, after independently checking
the current diff/order/probe and raw evidence. This does not supply readiness while native gates
remain open. Immutable publication and installed-default gates also remain open.

## Current native verification — source 8f0fa851

Four-host run [38011993926](https://github.com/katasec/forge-mcl/actions/runs/38011993926)
tests PR head `8f0fa851`. Actual checkout/build identity is GitHub's synthetic merge
`afe58a79719673b170403d87d6280ac5b8bf5e29`, parents `8d28dc1` and `8f0fa851`.
Supervisor fetched the exact PR merge ref and observed an empty tree diff against `8f0fa851`;
the identity is recorded rather than mislabelled as the branch commit.

| Gate | Named current observation |
|---|---|
| Linux ARM64 native | Job114093839242 SUCCESS, 12m52s. CLI and public exec probe compiled and ran; blocked read/write cancelled0ms/0ms with peers open; pressure, actual workspace/environment, argv/cwd, early-root timeout/cancellation/no-sentinel pass |
| Linux x64 native | Job114093839259 SUCCESS,13m15s; same ordinary native probe pass,0ms/0ms blocked pipe cancellation |
| Exact published Runner image | Actual pulled index `c0031d451d046d4f28a75ca7f7c7f26169b26e92555de4b601128a005670331c`, amd64 manifest `cf2a7e14f639519af223dd1efa0a2adf4d40cf1cac5f1b7d2017c5860ca1078b`, source17080b73; normal unchanged tini entrypoint; actual tiniPID1/dotnetPID7/probePID14 observed01:21:23 UTC |
| Native proof under init | Open-peer cancellation0ms/0ms, pressure/workspace/argv and parent-first timeout/cancel/no late sentinel pass; orphan-entry disappearance separately observed01:21:24 and01:21:27 UTC |
| Bare PID1 negative | Refusal before child/root/sentinel creation PASS01:21:29 UTC; separate overridden-entrypoint controlled negative |
| Linux warning gate | Supervisor read raw compiler/probe logs; no compiler/linker/ILC/trim warning match; ordinary Git initial-branch hint is not a compiler warning |
| Windows ARM64 | Job114093839095 FAILED: native pressure and blocked-pipe checks pass; workspace assertion reports source_file mismatch. Probe expected absolute path preserves forward-slash suffix while Core converts it to OS separators. Later lifecycle cases were not reached |
| Canonical macOS | Run38011993998 FAILED: managed1055 passed/10 existing skips; CLI/probe native compilation and CLI help/version pass. Detailed artifact exec-probe-run.log reports pressure-child exit139 (SIGSEGV11), after64KiB stderr; cause remains under investigation |
| Four-host macOS | Job114093839215 SUCCESS, completed01:34:47 UTC: all blocked-pipe/pressure/workspace/argv/cwd/descendant/timeout/cancellation native checks pass; no compiler warning match in raw log |

Raw completed-job logs `/private/tmp/phase76-core-r8-linux-arm64.log` and
`/private/tmp/phase76-core-r8-linux-x64.log`, obtained through the completed-job logs API.
The CLI run-log command waits for the entire workflow and initially refused while siblings ran;
that command refusal is not a native failure. Owned image test container removed successfully.
These are controlled component observations; published package/installed acceptance remain open.

Read-only implementer investigation r9 observed **01:29:02–01:31:43 UTC** on2026-10-10;
Windows expectation defect confirmed independently by supervisor. Canonical detailed logs became
available afterwards through bounded ranges of GitHub artifact11655741274, avoiding its150MiB
full archive. Source.txt records the same synthetic merge identity above. Exact logs are in
`/private/tmp/phase76-core-r8-canonical-range-logs`; the failed pressure child produced no managed
exception. Exit139 establishes a child crash, not a timeout or its cause. Same implementer continues
read-only diagnosis; no source edit or relaxed gate is authorized. Draft PR77 remains unmerged.

Supervisor compared failed/successful macOS runs: same source, macOS14.8.9/23J631,
runner image20260831.0302.1, SDK10.0.401/runtime10.0.12. Setup action v4/v5 and canonical
`cli-verify`/release `cli-package` differ, but both invoke the same Release/AOT native probe build;
these differences are observations, not a cause. Successful raw log:
`/private/tmp/phase76-core-r8-macos-arm64.log`. Exact failed canonical probe executable and
ONNX sidecar extracted to `/private/tmp/phase76-core-r8-canonical-binary` for reproduction.

Read-only continuation observed **01:33:45–01:36:46 UTC**. Exact canonical executable SHA256
`8af6dac56872297311059f4c97f480bd0d59af2cb931608c6f18f5d27bcda4a6` passed three direct child
pressure runs and the unchanged full public probe on local macOS27.0.1/arm64: exit0,6.642s,
all assertions, zero stderr. Supervisor independently read stdout and result JSON. These local
observations do not explain or waive the CI crash on macOS14. Only the confirmed Windows
expectation correction is approved at01:37:02 UTC; production source/deadlines unchanged,
fresh full code reviews and normal current-source native CI still required.

## Approved init correction — implementation r7

Same implementer stage2026-10-10 **00:47:38–00:56:27 UTC** (8m49s), following explicit complete-r9
approval. Frozen/pushed source`1d962702e38650fce73a8e3c6a759c4299d7081a`, existing
[product PR77](https://github.com/katasec/forge-mcl/pull/77), clean branch. Complete42-path diff
against8d28dc1 has2581 insertions/457 deletions; SHA256
`43772CC090BC11FB7EFF16F099CB061D097019A48CB97B328886C926BCC8388C` independently matched by supervisor.
Full per-file handback and raw commands/logs:
`/private/tmp/phase76-core-init-20261010T004738Z/EVIDENCE.md`.

Current correction spans nine already-approved paths: preallocation bare-Linux-PID1 guard,
removed adopted-child scan/reap/getpgid, decisive duplex pressure, retained public-Pipeline
workspace/environment and real parallel ONNX cancellation proof, exact immutable published-image
positive/negative native gates and corresponding READMEs. Supervisor corrected an overconstrained
probe assumption before freeze: find the direct dotnet Runner child among init's children;
do not require exactly one total child. No material deviation or additional scope.

| Gate | Named observed result |
|---|---|
| Debug solution build | Initial/final logs0warnings/0errors |
| Focused eight families |191passed/0failed/0skipped; actual published Client0.9.3 Create/Open/Reconnect included |
| Unfiltered full Debug |1063passed/0failed/6existing live-integration skips,2m54s; MCL_API_KEY absent,90s diagnostic timeout; all tests finished |
| Release/package |407passed/0failed/0skipped and normal metadata/content/dependenciesPASS; precommit metadata correctly labelleddc03, replaced by final-source package proof below |
| Build script checks |30boundary casesPASS; PowerShell syntaxPASS |
| Managed public Core probe | Concrete Unix declined-stdin native32; pressure, runtime/workspace/environment, literal argv/cwd/lookup, already-exited/early-root descendants, timeout/cancel and no late sentinelPASS |
| Fixture/source hygiene | Text diff checkPASS; unchanged binary ONNX heuristic excluded only from whitespace inspection, exact fixture hashes retained; no test suppression |
| Final-source branch package | Source1d962702; normal nuspec/license/README/Parser-Scout0.1.0PASS; built/restored SHA256`55FA01688A63F9D8DD69AE091E672B7918A773DE1E585DBBD8696B0FDD7068A2` match |
| Fresh isolated consumer | Generated JSON/assets/executable/hash/admission, packaged exec alias/cwd/runtime/output bytes, exactStepKey, numeric pipeline, actual in-flight joined cancellation/no score and six-argument/no-assets round tripsPASS |
| Actual retained Client DLL |0.9.3 Application DLL SHA256`CF472B56AEC324111750D6F03A7695E5E134A050BCB11C2A51076AAC9CDA23CA`; no rebuild |
| Final native gates | Canonical[38011044851](https://github.com/katasec/forge-mcl/actions/runs/38011044851) and four-host[38011044774](https://github.com/katasec/forge-mcl/actions/runs/38011044774) started at1d962702; supervisor requested cancellation after independently confirming the code-review ordering defect below. No native PASS inferred; revised-source gates remain mandatory. |

Supervisor independently read actual source/diff, raw build/full/probe/consumer logs and final nuspec.
Branch-package/managed probes are controlled evidence; published0.1.8 and installed defaults remain
required. Local Docker absence is not a waiver. Full current simplicity/style review assigned
**00:56:43 UTC**; ownership follows sequentially. Product remains unmerged/unpublished.

The simplicity/style reviewer identified `ExchangeAsync` final `JoinProcessAsync` before its final
`await io`: on cancellation/I/O failure the first try can leave pipe work unfinished, so POSIX
final reap releases the retained root before stream joins. Supervisor independently confirmed the
source violates the approved complete-r9 retained-root-through-stream-joins requirement. Complete
sequential reviews must finish before the same implementer receives one combined in-plan correction.
No new scope, lifetime framework or public contract is approved. Superseded-source native runs were
cancelled to avoid spending the final gate on code already known to require correction; this is
not a waiver of any revised-source verification.

## Full code review r3 — init correction

Simplicity/style assignment boundary00:56:43 UTC; observed2026-10-10
**00:57:12–01:00:19 UTC**, source1d962702/full42 paths. No earlier PASS inherited.

| Simplicity check | Current verdict / evidence |
|---|---|
| New apps/libraries | PASS: verification probe only; dependencies unchanged |
| Reuse | PASS: existing interpreter, validator, adapters and parser |
| Multiple paths | PASS: platform lifetime implementations under one exchange; one metadata parser |
| Legacy paths | PASS: actual published six-argument ABI retained; older checkpoints refused |
| Knobs | PASS: runtime workspace values, no new policy setting |
| Speculative abstractions | PASS: private native lifetime owns launch/identity/termination/cleanup |
| Library choice | PASS: existing ONNX1.27.0 |
| Copy-paste | PASS: shared admission, fingerprint and child options |
| Redundant definitions | PASS: caller-owned live artifact registry |
| Size/requirement | PASS:1331 product-text additions cover approved producer scope |
| Test volume | PASS:1175 test/probe/fixture-text additions prove required admission/replay/ABI/native behavior |

| Code-style check | Current verdict / evidence |
|---|---|
| Progressive disclosure | PASS: adapter entry flow precedes helpers |
| Small functions | PASS: coherent operations |
| Top-down order | PASS: parser entry points precede helpers |
| Explicit errors | REVISE: exact root reap precedes final pipe joins; reviewer also requests bounded pipe cleanup proof |
| Shallow nesting | PASS: parser/process early exits and bounded nesting |
| Separate side effects | PASS: pure workspace allocation; exec owns directory/process I/O |
| Zero warnings | Managed0warningsPASS; final NativeAOT gate open, cancelled superseded-source runs not counted |
| Extract for reason | PASS: semantic/native resource boundaries |
| Complexity | PASS: manual classic counts Exchange8, POSIXJoin7, parserAddRow9, Resume7, probe dispatcher12, retained diagnostics13 |

Supervisor accepts the concrete ordering defect. A separate uninterruptible-task premise is not
supported by actual.NET10.0.12: Windows anonymous-pipe async operations use
[PipeStream cancellation](https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/src/libraries/System.IO.Pipes/src/System/IO/Pipes/PipeStream.Windows.cs)
and[CancelSynchronousIo](https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/src/libraries/Common/src/System/Threading/AsyncOverSyncWithIoCancellation.cs);
Unix uses token-aware[Socket ReceiveAsync/SendAsync](https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/src/libraries/System.IO.Pipes/src/System/IO/Pipes/PipeStream.Unix.cs).
Local source evidence`/private/tmp/phase76-dotnet10-async-io-cancel.cs` and
`/private/tmp/phase76-dotnet10-pipe-unix.cs`. These APIs are cancellation-capable, not an absolute
guarantee against arbitrary kernel failure. Do not add a second pipe framework or abandon tasks
behind a wait timeout. Full ownership review is running; supervisor will combine findings and
require concrete blocked-pipe cancellation/join evidence using the exact existing BCL pipe type,
with opposite endpoints held open, alongside the in-plan final-reap ordering correction.

Ownership observed2026-10-10 **01:01:35–01:02:36 UTC**, complete same42-path diff/hash independently
matched; exact dispatch timestamp unavailable. Owners derived again from Desktop atlas, Core and
scripts READMEs before comparison. No competing implementation found across named Forge repos.

| Behavior | Derived owner / actual placement | Verdict |
|---|---|---|
| Explicit package assets | Core manifest / ForgeManifest | PASS |
| Distribution metadata before environment evaluation | Core TOML / ForgeTomlReader | PASS |
| Full local parsing | Same Core TOML parser | PASS |
| Immutable diagnostic sources | Core ExpertLoader | PASS |
| Declared string parameters | Core ExpertLoader | PASS |
| Pure package construction/validation | Core package validator | PASS |
| Reachable admitted inputs | Core input policy | PASS |
| Exact reserved names/token_count | Core input policy | PASS |
| Provider profile collection, not availability | Core validator; Runner owns availability | PASS |
| Asset path/collision/digest/model checks | Core validator | PASS |
| Empty-assets hash/new-assets identity | Core validator | PASS |
| Actual generated JSON size | Core validator | PASS |
| Published six-argument ABI | Core contract producer | PASS |
| Explicit generated JSON constructor | Core contract producer | PASS |
| Actual Client Create/Open/Reconnect proof | Existing integration tests | PASS |
| Complete execution fingerprint | Core replay owner | PASS |
| Malformed/current-incompatible checkpoint refusal | Core checkpoint codec | PASS |
| Relative inputs/completed effects across resume | Core PipelineRunner | PASS |
| Exact lifecycle StepKey | Core trace/PipelineRunner | PASS |
| Runtime workspace inheritance | Core PipelineRunner | PASS |
| Caller-owned live registry | Core workspace retains view; caller registers | PASS |
| Deterministic allocation path | Core workspace | PASS |
| Process-local verified aliases | Core exec adapter | PASS |
| cwd/literal args/runtime environment | Core exec adapter/argument owner | PASS |
| Preallocation bare LinuxPID1 refusal | Core exec launch | PASS |
| Adopted orphan reaping | Runner init; removed fromCore | PASS |
| Atomic POSIX group ownership | Core private exec lifetime | PASS |
| Root retention through stream joins | Core private exec lifetime; current ExchangeAsync violates order | FAIL |
| macOS held-root-only completion | Core private POSIX lifetime | PASS |
| Preexecution Windows job ownership | Core private Windows lifetime | PASS |
| Bounded concurrent stdin/stdout/stderr | Core exec adapter | PASS |
| Precise declined-stdin classification | Core exec write boundary | PASS |
| CleanupIOException over cancellation | Core exec failure boundary | PASS |
| Numeric ONNX/joined cancellation | Existing Core ONNX adapter | PASS |
| Actual parallel sibling progress | Core pipeline/adapter tests | PASS |
| Output-before-input pressure | Existing native verification owner | PASS |
| Public workspace/environment/native assertions | Existing native verification owner | PASS |
| Immutable normal Runner entrypoint/negativePID1 proof | Build/probe owner; Runner hosts | PlacementPASS; execution pending |
| Immutable Core publication/content checks | Existing publication owner | PlacementPASS; publication pending |
| Installed Project/Chat/Hands defaults | Supervisor through existing CLI/Client | Correct owner; acceptance pending |

| Ownership technical gate | Current verdict |
|---|---|
| Public APIs/generatedJSON/actualClientABI | PASS |
| Hash/pure admission/replay/trace/live registry | PASS |
| Security/data/credentials | PASS: no new service/store/identity/public entry point or credential authority |
| Duplicates/component jobs | PASS: no competing implementation/unrelated second job; orphan duplication removed |
| Failure containment | REVISE: definite root-reap order; actual blocked-pipe cancellation evidence required |
| Managed/package | Rawfocused191/full1063+6skips/Release407/scripts30/branchconsumerPASS, managed0warnings |
| Final native | Open; cancelled source does not satisfy gates |
| Published/default | Open; controlled branch package cannot close |
| UI | N/A |

### Combined correction approval

Supervisor approved **01:03:13 UTC**, assigned same implementer`implement:r8`. Placement moves
none. In existing ExchangeAsync keep checked termination before final pipe joins, then final
native join/exact reap, preserving cleanupIOException precedence and original context. Existing
native probe adds exact anonymous-pipe blocked-read/filled-write cancellation with opposite ends
held open, requiring actual operation completion within5s before closing those ends. Failure
cleanup closes only owned peer endpoints and joins pending operations; a wait timeout cannot
abandon work or supply PASS. No new product abstraction, framework, factory, option or file.
Root accepts the narrower ownership evidence finding; a hypothetical failure of kernel
cancellation is not authorization for speculative replacement machinery. Revised-source managed,
fresh consumer, full sequential code reviews and final canonical/all-host native gates remain
mandatory. Same42-path scope/public/security/default contracts remain approved.

## Prior correction delivery

Prior bounded r7 approval delivered through [MCL PR377](https://github.com/katasec/mission-control-language/pull/377),
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
they do not close the final-source/native/default gates. Canonical macOS remained running at
the next snapshot. macOSARM64 job114072387427 subsequently completed SUCCESS; supervisor
downloaded native-macos-arm64-dc03.log in the same raw-log directory. Native argv/cwd, PATH/spaced
lookup, already-exited descendant cleanup, early-root timeout/caller-cancel and lifecycle PASS
at00:03:19 UTC; no raw compiler/linker warning. The four-host run completed FAILURE solely
because of Linuxx64 bare-PID1. This still precedes the four-file test correction and required
hosting/Core revision; no final-source PASS.

Canonical run38005239039 subsequently completed SUCCESS atdc03. Supervisor downloaded job
114072386212 to canonical-macos-dc03.log in that raw-log directory: normal build0warnings/errors;
full managed1054PASS/10platform-live skips/0fail; current native macOS lifecycle PASS00:06:22;
terminal package27PASS and metadataPASS00:06:59. Raw compiler/linker warnings absent. This is
current pushed-source canonical evidence, still predating uncommitted r4 test additions and the
required Core hosting correction. It does not close the final revised-source/default gates.

Supervisor later directly observed release run37999080639 completed successfully on all four
native hosts at source770778d (GitHub updatedAt2026-10-09T22:54:45Z). These frozen-source
build/help/version results remain lower-layer evidence; that old source's canonical macOS
managed suite failed and did not test corrected-launch behavior.

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

## Hosting closed — full Core plan revision

Hosting independently verified00:37:11 UTC; [actual source/image/process and installed Chat
observations](phase-76.5-runner-process-hosting_completed.md#default-path-acceptance).
Documentation closurePR379 merged00:38:55 UTC to MCLmaincf6e8c7804ea8e0d6367f332dd53f672cd44817e.
Runner main17080b73, infra mainfa49f916 both clean/current. This is not Core completion.

Same implementer read-only full plan:r9 assigned2026-10-10 00:39:04 UTC. Existing full generic
design and independently reviewed init split are already locked, so Design is not repeated;
plan must retain every current42-file API/ABI/semantic/native/default obligation and specify
barePID1 prelaunch refusal, adopted-scan removal, stronger duplex pressure and exact published
Runner0.20.6 immutable-image probe with unchanged entrypoint. Full sequential current plan
reviews/approval precede all mechanism edits. Existing four-file corrections remain held;
root independently observed unchanged CoreHEADdc03b7b and exact four modified paths.
No later consumer upgrade or additional product repository is authorized by this bounded task.

## Complete round 9 plan reviews

Current replacement: [complete r9](phase-76.4-core-cloud-primitives-plan.md), returned read-only
assigned00:39:04 UTC; agent observed work end00:41:04, final response available00:42:36 UTC
(completed response timestamp in the implementer session log). Root transcribed the exact
final response; no product edits. Frozen
review artifact SHA256`98A52F1786398A7D8644EC077262647E43D66C12322E195C66A5BC70D01F7FB5`.
Documentation validation14docs/97links/2JSON/fences/global/diffPASS. Full simplicity plan r7
assigned00:43:25, observed00:43:48–00:44:29 UTC: no findings.

| Simplicity check | Complete r9 verdict |
|---|---|
| New apps/libraries | PASS: unchanged42paths, no dependency/framework/public factory |
| Reuse | PASS: existing parser/validator/checkpoint/pipes/adapters/build owners |
| Multiple paths | PASS: generic execution; positive/negative native cases distinct requirements |
| Legacy | PASS: scan deleted, obsolete checkpoint formats rejected |
| Knobs | PASS: fixed private guard, no capacity/admission setting |
| Abstractions | PASS: existing OS lifetime/failure seam |
| Library choice | PASS: existing ONNX/BCL/verified init, no renewed research |
| Copy-paste | PASS: shared parser/policy/workspace/native build |
| Redundant definitions | PASS: actual Client0.9.3sixargABI + separate JSON selection |
| Size | PASS: same42-file producer, no consumer/image/deploy scope |
| Test volume | PASS: decisive duplex/real parallel cancellation/image/native/default outcomes |

Security/engineering/default PASS; bounded joined work/cleanup precedence/live caller-owned
registry retained, exact immutable image and separate NuGet/installed gates explicit. All four
native hosts/canonical raw zero-warning AOT required; UI N/A. Ownership plan r7 assigned
00:45:01 UTC; observed00:45:38–00:47:05 UTC, technical/placement PASS on the exact frozen
plan hash above. No blocker, duplicate owner or component second job found. Complete verdicts:

| Behavior | Derived/proposed owner | Current r9 verdict |
|---|---|---|
| Explicit assets | Core Manifest | PASS |
| Distribution without provider evaluation | Existing Core TOML reader | PASS |
| Full parser/stages | Same reader | PASS |
| Supplied diagnostics/no filesystem fallback | Core ExpertLoader | PASS |
| Declared parameter string typing | Core ExpertLoader | PASS |
| Pure package construction | Core validator | PASS |
| Required root/reachable optional inputs | Core input policy/validator | PASS |
| Reserved names/token_count | Core input policy | PASS |
| Kinds/profile names | Core validator | PASS |
| Asset paths/collisions/bytes/models | Core validator | PASS |
| Asset hash/empty identity | Core validator | PASS |
| Actual generated4MiBJSON | Core validator | PASS |
| Actual Client six-arg ABI | Core producer contract | PASS |
| Published Client Create/Open/Reconnect proof | Existing integration/composition tests | PASS |
| Execution fingerprint | Core fingerprint | PASS |
| Malformed/incompatible continuation refusal | Core codec | PASS |
| Relative replay/completed effects | Core pipeline/checkpoint | PASS |
| Exact lifecycle StepKey | Core trace/pipeline | PASS |
| Child workspace inheritance | Core run options/pipeline | PASS |
| Later live-registry registration | Caller/Runner registers; Core read-only view | PASS |
| Pure allocation paths | Core workspace | PASS |
| Process-local verified aliases | Core exec | PASS |
| Literal argv/cwd/environment | Core exec | PASS |
| BarePID1 guard before pipes/spawn | Core launch | PASS |
| Adopted scan/reap removal | Runner init sole orphan owner | PASS |
| Atomic POSIX group/retained root | Core private lifetime | PASS |
| macOS root-only completion | Core POSIX lifetime | PASS |
| Preexecution Windows job | Core Windows lifetime | PASS |
| Concurrent bounded exchange | Core exec | PASS |
| Precise declined stdin | Core write boundary | PASS |
| Joined work/cleanup precedence | Core lifetime/adapter | PASS |
| Numeric/joined ONNX | Existing Core adapter | PASS |
| Actual ONNX sibling proof | Core pipeline/adapter tests | PASS |
| All-host native lifecycle | Existing probe/build owner | PASS |
| Exact published init image | Controlled Core probe using Runner artifact | PASS |
| Separate init reap/barePID1 negative | Probe/build; init performs reap | PASS |
| Immutable publication/provenance | Existing workflow/verifier | PASS |
| Installed Chat/Hands | Supervisor through existing defaults | PASS |

| Technical gate | Current verdict |
|---|---|
| Public types/shapes | PASS: concrete DTOs/results/workspace/trace/private seam |
| Binary compatibility | PASS: actualClient0.9.3sixarg + primaryJSONctor; published-DLL proof |
| Authority | PASS: no Core registration/staging/collection/persistence |
| Process/failure | PASS: preexecution ownership, retained identity, joined work, cleanupIOExceptionprecedence, existing failed-start envelope |
| Pressure | PASS:1Mstdout+64KiBstderr finish before2Mstdin, caps/deadlines retained |
| Dependency | PASS: hosting verified; revised Core exact-image observation still required |
| Security | PASS: no service/data/identity/public/credential boundary change; inherited trust/no sandbox |
| Engineering/scope | PASS: same42paths, existing owners, no new setting/framework; scanner removed |
| Native/build readiness | PASS as plan; exact routes/matrix/negative/zero-warning gates; results pending |
| Publication/default | PASS as plan; fresh normalpackage + installed regressions; controlled proof not acceptance |
| UI | N/A |

Supervisor independent check00:47:38 UTC: exact frozen plan SHA, complete current types/ABI,
all42paths, source dc03b7b/exact4held corrections, reviewed init/failure split, final-source
native/default and immutable publication obligations checked. **PLAN APPROVED00:47:38 UTC**;
same implementer implement:r7 assigned that boundary. Root changed approval metadata only;
no plan mechanism changed after review. No further product scope or consumer upgrade approved.

| Stage | Start UTC | Result/end UTC | Wall | Evidence |
|---|---|---|---|---|
| Full implementer plan r9 | 00:39:04 | 00:42:36 | 3m32s | agent work end00:41:04; final response timestamp42:36 |
| Simplicity plan r7 | 00:43:25 | 00:44:29 | 1m4s | full11PASS, observed start43:48 |
| Ownership plan r7 | 00:45:01 | 00:47:05 | 2m4s | full38/11gatesPASS, observed start45:38 |
| Independent approval/implement handoff | 00:47:38 | 00:47:38 | Boundary | supervisor, only complete r9 |

All stage dates2026-10-10 UTC. New implementation/current-source code reviews/native/publication/
default results remain future; earlier observations do not close those gates.

## Superseded round 7 full plan

Historical artifact; replaced by the complete r9 plan. No current mechanism-edit authority.

### Phase 76.4 — implementer correction plan, round 7

**Status: HISTORICAL APPROVAL — mechanism edits frozen.** Round7 was approved 2026-10-09
23:27:01 UTC. Actual native Linux PID1 failed; the [locked hosting correction](phase-76.5-runner-process-hosting.md)
supersedes its raw-PID1/adopted-reap choice. Hosting is now verified; full revised Core plan r9
is in progress before further mechanism edits. Existing four-file test correction is held
uncommitted; all merge/publication/default gates remain open. The complete prior plan below is
retained only until the revised plan replaces it; it grants no current mechanism-edit authority.
[Task/design](phase-76.4-core-cloud-primitives.md). Prior approvals and review findings are recorded in [completion evidence](phase-76.4-core-cloud-primitives_completed.md).

#### 1. Files

Complete Phase 76.4 correction plan, round 7. Supervisor clarification began **2026-10-09 23:23:18 UTC**, following the implementer's observed macOS cleanup deviation. Round 6 implementation is uncommitted over `770778d2d20a8c202552d7c3f3dd1e2cbdc7425c`, branch `adeen/phase-76-core-primitives`, draft [PR 77](https://github.com/katasec/forge-mcl/pull/77). No files or public contracts are added by this clarification. Continue that same unfinished branch only after supervisor approval. Governing documents are the complete Phase 76.4 plan/task and locked Phase 76.2 contracts. UI and browser visual gates are N/A; runtime/default-path acceptance applies.

All 42 product paths below are relative to `/Users/ameerdeen/progs/forge-mcl`. Entries 1–29 retain the original inventory; entries 30–42 are the explicit launch/lifetime and verification additions. No other product repository, consumer upgrade, registry behavior, Runner image, cloud deployment, permission policy or workflow bypass is included.

| # | File | Purpose |
|---|---|---|
| 1 | `src/ForgeMission.Core/Manifest/ForgeManifest.cs` | PackageConfig and default-empty Package |
| 2 | `src/ForgeMission.Core/Manifest/ForgeTomlReader.cs` | Existing parser's distribution mode; coherent parsing stages with bounded complexity |
| 3 | `src/ForgeMission.Core/Experts/ExpertLoader.cs` | Immutable diagnostic sources; declared mission parameter string keys |
| 4 | `src/ForgeMission.Core/Runtime/DurableMissionPackageValidator.cs` | Assets, pure construction, canonical identity, common validation and actual JSON size |
| 5 | `src/ForgeMission.Core/Runtime/DurableMissionInputPolicy.cs` | Single reserved/reachable-input policy |
| 6 | `src/ForgeMission.Core/Runtime/PipelineDefinitionFingerprint.cs` | Complete execution-semantic checkpoint identity |
| 7 | `src/ForgeMission.Core/Runtime/PipelineExecutionWorkspace.cs` | Runtime workspace retaining the live caller-owned artifact registry and pure output-directory convention |
| 8 | `src/ForgeMission.Core/Runtime/PipelineRunOptions.cs` | Optional inherited execution workspace |
| 9 | `src/ForgeMission.Core/Runtime/PipelineRunner.cs` | Admitted names, replay, nested workspace and StepKey/attempt threading |
| 10 | `src/ForgeMission.Core/Runtime/PipelineToolPause.cs` | Checkpoint format 3 and strict required-shape validation at the existing codec |
| 11 | `src/ForgeMission.Core/Runtime/PipelineTraceEvent.cs` | Inherited init StepKey without positional constructor changes |
| 12 | `src/ForgeMission.Core/Adapters/ExecExpertRunner.cs` | Generic workspace mappings, bounded joined I/O and explicit cleanup precedence; public methods first |
| 13 | `src/ForgeMission.Core/Adapters/OnnxExpertRunner.cs` | Joined native cancellation and parallel launch; public methods first |
| 14 | `src/ForgeMission.Core/ForgeMission.Core.csproj` | Core 0.1.8; existing dependencies retained |
| 15 | `src/ForgeMission.Core/README.md` | Package/input/replay/workspace/trace/native-lifecycle contracts and limits |
| 16 | `tests/ForgeMission.Mcl.Tests/Manifest/ForgeTomlReaderTests.cs` | Full and distribution reader behavior |
| 17 | `tests/ForgeMission.Mcl.Tests/Experts/ExpertLoaderTests.cs` | In-memory diagnostics, string parameters and mismatch locations |
| 18 | `tests/ForgeMission.Mcl.Tests/Runtime/DurableMissionPackageValidatorTests.cs` | Package/hash/JSON/assets/names/profile/model boundaries and generated round trips |
| 19 | `tests/ForgeMission.Mcl.Tests/Runtime/AgentToolPipelineTests.cs` | Replay and malformed-current-checkpoint refusal without invocation |
| 20 | `tests/ForgeMission.Mcl.Tests/Runtime/PipelineTraceTests.cs` | Exact lifecycle keys across sequential/nested/parallel/loop/delta/pause paths |
| 21 | `tests/ForgeMission.Mcl.Tests/Adapters/ExecExpertRunnerTests.cs` | Real process lifecycle/I/O/path/live-registration regressions and controlled private error-precedence test |
| 22 | `tests/ForgeMission.Mcl.Tests/Adapters/OnnxExpertRunnerTests.cs` | Numeric and genuinely in-flight cancellation observations |
| 23 | `tests/ForgeMission.Mcl.Tests/Cli/ForgeProjectTests.cs` | Actual retained published Client 0.9.3 Create/Open/Reconnect ABI regression |
| 24 | `tests/ForgeMission.Mcl.Tests/Fixtures/onnx/README.md` | Test-model generation, provenance and exact hashes |
| 25 | `tests/ForgeMission.Mcl.Tests/Fixtures/onnx/identity.onnx` | Tiny numeric semantics fixture |
| 26 | `tests/ForgeMission.Mcl.Tests/Fixtures/onnx/cancellable-loop.onnx` | Tiny bounded-test native cancellation fixture |
| 27 | `.github/workflows/publish-core-package.yml` | Normal Core 0.1.8/core-v0.1.8 publication and Linux native adapter checks |
| 28 | `eng/verify-core-package.sh` | Normal version/content/provenance/Parser-Scout dependency checks |
| 29 | `Makefile` | Existing Core version declaration aligned to 0.1.8 |
| 30 | `src/ForgeMission.Core/Adapters/ExecProcess.cs` | Internal owned-process lifetime boundary, managed pipes and private cleanup exception |
| 31 | `src/ForgeMission.Core/Adapters/ExecProcess.Posix.cs` | Atomic group launch, unreaped leader, joinable observation and Linux owned adopted-child drain |
| 32 | `src/ForgeMission.Core/Adapters/ExecProcess.PosixNative.cs` | Narrow libc imports, checked ABI storage/constants and native error conversion |
| 33 | `src/ForgeMission.Core/Adapters/ExecProcess.Windows.cs` | Atomic job association, root/job observation and joined handle ownership |
| 34 | `src/ForgeMission.Core/Adapters/ExecProcess.WindowsNative.cs` | Narrow Win32 imports, structures and owned native handles |
| 35 | `src/ForgeMission.Core/Adapters/ExecProcessArguments.cs` | Actual Unix command resolution and literal Windows argv formatting |
| 36 | `tests/ForgeMission.Exec.Probe/ForgeMission.Exec.Probe.csproj` | Small maintained package-reference-free public-Core-API native lifecycle probe |
| 37 | `tests/ForgeMission.Exec.Probe/Program.cs` | Native acceptance assertions, including explicit PID 1 mode |
| 38 | `tests/ForgeMission.Exec.Probe/ExecProbeChild.cs` | Controlled executable child modes for process/pipe/argument fixtures |
| 39 | `tests/ForgeMission.Mcl.Tests/ForgeMission.Mcl.Tests.csproj` | Build probe through existing test build, ReferenceOutputAssembly=false |
| 40 | `ForgeMission.slnx` | Include the probe in normal managed compilation |
| 41 | `scripts/build.ps1` | Existing verify/package gates publish/run the native probe; normal Linux x64 CI also runs no-init PID 1 proof |
| 42 | `scripts/README.md` | Explain these normal verification commands and evidence locations |

The new probe is test tooling outside the shipped CLI payload. Existing release and terminal-extension workflow files remain unchanged. No native helper binary, shell/Python wrapper, new library, public process factory, environment isolation claim or execution whitelist is introduced.

#### 2. Reuse

##### Public producer and compatibility contracts

Retain the following exact proposed public surface, including the already implemented six-argument CLR member:

```csharp
public sealed record PackageConfig(IReadOnlyList<string> Assets);
// ForgeManifest:
public PackageConfig Package { get; init; } = new([]);
// ForgeTomlReader:
public static ForgeManifest? TryReadDistribution(string missionFilePath);

public sealed record DurableMissionAssetInput(
    string Path, string ContentType, byte[] Bytes,
    string Sha256, bool Executable = false);
[method: System.Text.Json.Serialization.JsonConstructor]
public sealed record DurableMissionPackageInput(
    int FormatVersion, string PackageHash, string MissionSource,
    string RootMissionName, string RootInputName,
    IReadOnlyList<DurableResolvedExpertInput> ResolvedExperts,
    IReadOnlyList<DurableMissionAssetInput>? Assets = null)
{
    public DurableMissionPackageInput(
        int FormatVersion, string PackageHash, string MissionSource,
        string RootMissionName, string RootInputName,
        IReadOnlyList<DurableResolvedExpertInput> ResolvedExperts)
        : this(FormatVersion, PackageHash, MissionSource,
            RootMissionName, RootInputName, ResolvedExperts, null) { }
}
public sealed record ValidatedDurableMissionPackage(
    DurableMissionPackageInput Input, ForgeMission.Parser.Program Ast,
    Dictionary<string, ExpertDefinition> Experts,
    IReadOnlyList<string> AdmittedInputNames,
    IReadOnlyList<string> ProviderProfileNames);
// DurableMissionPackageValidator:
public static bool TryCreate(string missionSource,
    IReadOnlyList<DurableResolvedExpertInput> resolvedExperts,
    IReadOnlyList<DurableMissionAssetInput> assets,
    out DurableMissionPackageInput? package, out string? reason);
public static bool TryValidateInputNames(
    ValidatedDurableMissionPackage package,
    IReadOnlyCollection<string> names, out string? reason);

public sealed record PipelineExecutionWorkspace
{
    public PipelineExecutionWorkspace(string RootDirectory,
        IReadOnlyDictionary<string, string> ArtifactPaths);
    public string RootDirectory { get; }
    public IReadOnlyDictionary<string, string> ArtifactPaths { get; }
    public string GetStepOutputDirectory(string stepKey, int attempt);
}
// PipelineRunOptions:
public PipelineExecutionWorkspace? ExecutionWorkspace { get; init; }
// PipelineTraceEvent:
public string StepKey { get; init; } = "";
// ExpertLoader.Validate final optional argument:
IReadOnlyDictionary<string, string>? expertMarkdownByName = null
```

Existing `ExecExpertRunner(string defaultTimeout = "30s")` remains. Its internal workspace/key/attempt constructor serves the same generic path. No other retained published member needs a speculative shim: inventory of actual Client 0.9.3, Client.Contracts 0.2.1, Conversations.Contracts 0.7.0 and Hands 0.1.0 found the six-argument package constructor call, but no affected ExpertLoader.Validate/ValidatedPackage member reference. Preserve current consumer pins.

| New or changed seam | Existing equivalent searched / reuse decision |
|---|---|
| Distribution reading and parser stages | Reuse ForgeTomlReader's section, scalar, array, multiline and ResolveValue rules. Separate selecting/storing rows from constructing provider/execution/capability/package results inside that same parser; no second parser. Private ManifestRows/ManifestSection plus ReadRows, ReadSection, ReadAssignment, AddRow, BuildProviders and BuildExecution express real parsing stages. |
| In-memory diagnostics | Reuse ExpertLoader.ParseContent/SplitFrontmatter/FindKeyInBlock. When the supplied markdown map is present, do not call File.Exists/ReadAllLines or fall back on a missing entry. Local omitted-map callers retain existing disk diagnostics. |
| Package and input policy | Reuse Parser, ExpertLoader and existing canonical hash. DurableMissionInputPolicy replaces overlapping traversal/filtering; no caller-admitted list. Validator remains the sole pure construction/validation owner. |
| Semantic replay identity | Existing fingerprint omitted execution-affecting fields. PipelineDefinitionFingerprint encodes the actual AST/expert semantics once with length prefixes and invariant typed values; no generic serializer/fingerprint framework. |
| Workspace/output directory | Existing internal StepKey and attempt are authoritative. PipelineExecutionWorkspace retains the supplied caller/Runner-owned LIVE ConcurrentDictionary backing registry through IReadOnlyDictionary, without copying or freezing it. Registration authority remains with the caller/Runner. GetStepOutputDirectory is a pure shared convention needed by exec and later Runner collection; no new registry, identifier or path allocator. |
| Native numeric work | Reuse ONNX Runtime 1.27.0 SessionOptions.SetLoadCancellationFlag, RunOptions.Terminate and Run overload; reuse existing tensor/probability semantics. |
| Owned exec launch | Process.Start/Kill(entireProcessTree:true) loses ordinary descendants after an early root exit. No existing Forge lifetime owner supplies atomic group/job ownership or retained POSIX root identity. Keep managed AnonymousPipeServerStream/SafePipeHandle and their cancellable async operations; add only the private OS ownership seam below. |
| Established process libraries | Inspected asmichi.ChildProcess 0.18.0 at 823a7d5: SendSignal returns when isReaped_ is true, so it cannot supply the required retained-root lifecycle unchanged. Inspected ProcessKit 2.12.0 at 1ddc158: documented post-kill reap handoff and output-pump abandonment do not meet this task's joined-before-return boundary; adopting it would also add its dependency stack. No fork/library rewrite is justified. See pinned [ChildProcessState.cpp](https://github.com/asmichi/ChildProcess/blob/823a7d5f1ba674013706b2f1d90a916a87be4a49/src/ChildProcess.Native/ChildProcessState.cpp), [ProcessKit Backend.fs](https://github.com/ZelAnton/ProcessKit-fSharp/blob/1ddc15837f93895211d5a129c08d4c0504b50e06/src/ProcessKit/Backend.fs) and [RunningProcess.fs](https://github.com/ZelAnton/ProcessKit-fSharp/blob/1ddc15837f93895211d5a129c08d4c0504b50e06/src/ProcessKit/RunningProcess.fs). |
| Command/argument preservation | Reuse ProcessStartInfo as the existing command/args/cwd/environment preparation value. Unix private resolution matches actual .NET 10 ResolvePath; Windows uses CreateProcessW's existing command lookup and a narrow literal argv formatter because .NET's PasteArguments is internal. No shell expansion. |
| Declined stdin | Reuse platform IOException identity from .NET pipes, not a custom text parser. Unix is inner SocketException EPIPE/native32, SocketError.Shutdown. Windows is HRESULT_FROM_WIN32(109/232/233). |
| Cleanup-error policy | Add one private production failure selector at the real exchange boundary and an internal IOException subtype. Controlled reflection testing uses that actual selector; no injected process implementation or public fault hook. |
| macOS zombie-only group | XNU killpg1 excludes SZOMB members and can return EPERM when only the retained zombie root remains. Reuse system libproc proc_listpids, not a new process scanner/library. Its PROC_PGRP_ONLY query holds the process-list lock and includes zombies. One private import in ExecProcess.PosixNative.cs and bounded observation in ExecProcess.Posix.cs suffice; no new file, public seam, kill target or permission rule. |
| Native/PID1 verification | Reuse existing build.ps1 Invoke-Checked, source identity and raw warning gates. A small executable probe can spawn itself on all four native hosts and run as PID 1; it avoids an assumed installed Python/shell wrapper. Existing release workflow already calls the script. |

##### Exact internal launch/lifetime seam

```csharp
internal abstract class ExecProcess : IAsyncDisposable
{
    internal static ExecProcess Start(ProcessStartInfo options);
    internal abstract Stream StandardInput { get; }
    internal abstract Stream StandardOutput { get; }
    internal abstract Stream StandardError { get; }
    internal abstract Task ObserveExitAsync(CancellationToken cancellationToken);
    internal abstract void Terminate();
    internal abstract Task<int> JoinAsync(CancellationToken cleanupToken);
}
// Exact private policy member on ExecExpertRunner:
private static void ThrowExchangeFailure(Exception failure,
    IReadOnlyList<IOException> cleanupFailures,
    CancellationToken callerToken);
// Internal, never exposed as a new public exception contract:
internal sealed class ExecProcessCleanupException : IOException;
```

Concrete private implementations are `PosixExecProcess` and `WindowsExecProcess`. They own their pipes/native storage/handles from start through joined disposal. Native operation failures include operation and OS error code; the cleanup subtype preserves original exchange failure and all observed cleanup failures through inner/aggregate exception context. RunAsync rethrows that subtype before its ordinary I/O-envelope handling. No `_ =` tasks, background reaper, alternate local adapter or unowned launch fallback.

POSIX starts with `posix_spawn` attributes `POSIX_SPAWN_SETPGROUP`, pgroup 0, before target execution; dup2/chdir file actions preserve stdio and expert cwd. Darwin also uses its CLOEXEC-default spawn flag. Linux pipe handles retain close-on-exec until child dup actions; Windows alone needs inheritable child pipe ends. The parent closes its local child-end copies immediately after successful creation. Private static P/Invokes use checked supported ABI storage: Darwin spawn attribute/actions 8/8 bytes and siginfo 104; Linux x64/arm64 336/80/128, verified against current platform headers/CI. No dynamic ABI guessing or unmanaged helper compilation. Setup failures dispose acquired resources and remain visible; no fallback to Process.Start.

The root stays an owned, unreaped group leader through every group operation and pipe completion. `ObserveExitAsync` repeatedly calls **fixed-root** `waitid(P_PID, rootPid, ..., WEXITED|WNOWAIT|WNOHANG)` with a zeroed siginfo buffer and reads its first standard `si_signo` field. No event means an awaited 10 ms cancellable delay; EINTR retries. This neither reaps nor enumerates descendants. ECHILD/other unexpected native errors invalidate further identity-dependent operations and produce cleanup IOException; do not signal a possibly recycled identity. There is no blocking native-wait Task.Run. Final exact-root `waitpid(rootPid, WNOHANG)` occurs last. An absent signalable group (ESRCH) is accepted only with the retained, observed owned root establishing the identity/no-live-group case; other termination failures are errors.

On macOS only, an EPERM from the held group is a **pending cleanup error**, never evidence of completion. After root exit is observed and while its unreaped identity is retained, JoinAsync queries `proc_listpids(PROC_PGRP_ONLY=2, rootPid, int[2], 8)` from system libproc. Exactly four returned bytes containing the held root PID prove the complete group has only that exited root. Eight returned bytes mean additional members (possibly truncated); zero, negative, malformed length or a sole different PID is a visible observation failure. Additional members require an awaited 10 ms retry within the existing five-second budget. Apply this root-only observation after successful macOS group termination too, so ordinary descendants finish before root reaping. A retained EPERM is accepted only after that root-only observation; a genuine permission failure with remaining members expires as cleanup IOException preserving EPERM and any observation/deadline context. No root reap precedes the observation. The query neither establishes ownership nor selects signal targets. The root cannot fork after observed exit; its held PID prevents ordinary group reuse. No per-member state/UID reads, process-wide enumeration, new timeout, privilege or permission suppression is introduced. See primary [XNU signal implementation](https://github.com/apple-oss-distributions/xnu/blob/main/bsd/kern/kern_sig.c) and [group-list implementation](https://github.com/apple-oss-distributions/xnu/blob/main/bsd/kern/proc_info.c).

Windows creates a kill-on-close Job Object, sets STARTUPINFOEX `PROC_THREAD_ATTRIBUTE_JOB_LIST` and `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`, then calls CreateProcessW with that association **before target execution**. No post-launch AssignProcessToJobObject race. Keep the exact owned root/job handles through I/O and cleanup. Observe root with zero-timeout WaitForSingleObject plus cancellable delay; terminate with checked TerminateJobObject and observe job active-process count until zero before closing handles. WAIT_FAILED/query/termination failures are visible. No blocking abandoned waiter or PID-based Windows ownership.

#### 3. Sequence

1. After full plan reviews and explicit approval, recheck frozen SHA/clean status and Core 0.1.8 availability. Continue the existing branch, preserving round 1 logs. Create a new unique evidence directory, record start/source/package provenance, and implement only the listed corrections and retained approved producer scope. A version collision or material design deviation returns to supervisor before mutation.

2. Preserve actual binary/JSON compatibility first. Keep the exact six-argument constructor delegating to null Assets and select the seven-argument primary constructor with `[method: JsonConstructor]`. Generated-metadata round trips cover omitted/null/empty assets retaining old format-1 hash and nonempty assets preserving bytes, ContentType, Sha256 and Executable. Revalidate every round trip. Do not add a compatibility reader/public JSON context. Keep the actual Client 0.9.3 Application DLL regression through the existing configuration-aware CLI loader and ApplicationComposition.Create HTTP seam: MissionConversations.CreateChatProjectAsync, Projects.OpenChatAsync(ProjectOpenRequest(home,"Chat",DurableConversation)), then MissionConversations.ReconnectAsync. Exact deterministic typed transport responses echo the received launch and identities; unknown routes fail. Record selected DLL identity/hash; never recompile Client.

3. Refactor the one TOML parser into coherent row-read/section/assignment/manifest-construction stages, preserving duplicate-section resets, unknown-field handling, malformed headers, multiline arrays and source line diagnostics. Distribution mode drops provider/execution/capability rows before ResolveValue and rejects env expressions in selected distribution fields; full TryRead retains existing evaluation and literal assets. Missing manifest remains null. Each changed function targets 20–40 lines, nesting at most two and classic McCabe at most 15, preferably 10. Record actual classic decision counts; no tiny wrapper scoring games. Move public StreamAsync immediately after public RunAsync in both exec and ONNX before helpers.

4. Keep immutable diagnostics and common package validation. Supplied expert markdown must be complete and is the only diagnostic source; missing needed source is explicit error. Seed each mission's declared Params as string with TryAdd, preserving runtime keys and existing optional-input/binding/nested diagnostic policy. Validate root and reachable input identifiers by the locked ASCII rule; reserved names/prefixes remain exact Ordinal. Derive distinct Ordinal-sorted admitted names internally from root params/reachable expert inputs; unreachable experts cannot widen them. Root params are required, expert Inputs optional, token_count ordinary, and named inputs override literal let values.

5. Keep DurableMissionPackageValidator pure and authoritative. Select first mission/first parameter, including empty parameterless roots. Admit existing llm/rule/json_extract/exec/onnx/http/search kinds, nested/parallel/loops and more than two experts; collect reachable LLM profile names without a fixed/single-profile restriction. Reject environment AST, reserved bindings and unknown kinds. Validate asset bytes/SHA/Executable, canonical relative slash spelling with no traversal/backslash/colon, OrdinalIgnoreCase duplicates and staged collisions including mission.mcl/mcl.lock/forge.toml/expert markdown, reserved inputs/outputs directories, and ONNX relative model resolution to an admitted asset. A relative `../../models/...` model may be valid when it stays inside the package root. Preserve format-1 no-assets hash; append the locked ordered tagged asset section only when nonempty. Measure actual generated camelCase/null-omitting UTF-8 package JSON against 4 MiB including escaping/base64, not a raw-byte estimate.

6. Keep semantic fingerprint and replay behavior: explicit length-prefixed AST/expert semantics, typed dictionaries and invariant numbers; include args/timeout/model/endpoint/input/output/guards/loops/bindings/profile fields, exclude source spans/ExpertDirectory/workspace. Envelope remains 1; inner checkpoint is 3, old 2 refused. The admitted set is persisted and compared. Relative root inputs and completed-step writes replay unchanged into a fresh scratch workspace; completed effects run once. Children inherit workspace. Thread the actual internal StepKey/attempt into exec and every start/delta/completion/tool/checkpoint trace; retain positional trace constructors.

7. Repair required checkpoint shape at `PipelineCheckpointCodec.TryRead` before ResumeAsync consumes it. Reject null/missing Payload and null/missing RootInputs, including null values. Check actual consumed identities/fingerprints/root name/PausedKey, positive continuation ordinal/attempt, nonnull MissionPath/admitted-name entries, nonnull tool declaration entries/name/description/defined schema, nonnull turn messages/contents with exactly one pending last call, and nonnull log entries/keys/text/status/Writes/write values. Preserve legitimate empty inputs/admitted sets/logs, optional Reason and current typed logged-value semantics. Guard Undefined schema before fingerprinting. Use actual codec JSON member spelling (`rootInputs`, etc.), not a guessed naming policy. Malformed current-format mutations must return InvalidContinuation without provider or exec invocation. No catch-all serialization framework or compatibility format.

8. Preserve generic exec semantics while replacing ownership internally. ProcessInputs makes a process-local copy only: declared values matching validated ArtifactPaths become absolute under the segment root; root/checkpoint values stay relative. Root is absolute. ArtifactPaths retains the supplied caller/Runner-owned LIVE ConcurrentDictionary reference exposed through IReadOnlyDictionary; Core must not copy, freeze or take registration authority over it. A caller registration becomes visible to later exec invocations using the SAME workspace. `GetStepOutputDirectory(stepKey,attempt)` is pure `outputs/<lowercase SHA256 UTF8 StepKey>/<invariant attempt>` under that root. Exec alone creates the directory. Bind work_dir/input_dir/output_dir after authored values, but JSON still contains only declared inputs. Replace inherited stale FORGE_INPUT_* and FORGE_SOURCE_FILE aliases; set FORGE_WORK_DIR/INPUT_DIR/OUTPUT_DIR and verified FORGE_INPUT_<name>; legacy FORGE_SOURCE_FILE only for an actual verified declared source_file. Retain other environment/identity, existing expert-relative cwd, args, declared outputKey/status/reason/judge behavior and opaque authored output. This is arbitrary user-vetted code, without a sandbox/isolation/egress promise.

9. Preserve .NET command lookup exactly. On Unix resolve absolute request, then Environment.ProcessPath directory, then **parent** current directory, then executable PATH candidates, skipping empty PATH entries as the existing runtime does; only afterward apply expert cwd in child file actions. Execute the resolved path while argv[0] remains the authored command. Relative command meaning must not change to expert-cwd lookup. On Windows preserve CreateProcessW's existing null-application-name lookup, quoted executable token, literal ArgumentList escaping and environment block/cwd behavior. Cover empty arguments, spaces, quotes, backslash parity/trailing backslash, literal shell metacharacters, relative commands, parent cwd versus expert cwd, PATH and unavailable commands. The reference is [.NET 10 Process.Unix ResolvePath](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Unix.cs#L625).

10. Exchange raw UTF-8 stdin/stdout/stderr concurrently, bounded at 4 MiB/4 MiB/64 KiB. Avoid StreamWriter's buffered flush-on-close; write raw bytes once through cancellable PipeStream, then close the raw input end. A child declining input is recognized only at that write boundary after checking cancellation: macOS/Linux IOException inner SocketException native32/EPIPE and Shutdown; Windows HRESULT 0x8007006D (ERROR_BROKEN_PIPE109), 0x800700E8 (ERROR_NO_DATA232) or 0x800700E9 (ERROR_PIPE_NOT_CONNECTED233). Do not ignore generic IOException, ERROR_OPERATION_ABORTED995, invalid handle6, arbitrary EOF or message text. Genuine I/O failures retain their existing failed-envelope contract; a declined input still requires root/remaining I/O join, exit-code validation and JSON/outputKey validation. See actual [Unix pipe translation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.IO.Pipes/src/System/IO/Pipes/PipeStream.Unix.cs#L304) and [Windows pipe translation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.IO.Pipes/src/System/IO/Pipes/PipeStream.Windows.cs#L553). Caller cancellation is not a declined-input success.

11. Keep ownership until cleanup finishes, even when the root exits before descendants holding stdout/stderr. The exchange tracks the three I/O tasks and root observer, cancels/awaits all started tasks on failure, and checks termination before root reaping. Apply the same group/job cleanup on normal completion before releasing ownership, so closing pipes cannot hide a continuing ordinary descendant. No post-launch setpgid/job assignment and no descendant snapshot chooses kill targets. POSIX kills the held group; Windows terminates the held job. Apply the precise macOS root-only observation above, retaining any EPERM until completion is independently observed. A fixed private five-second cleanup budget with 10 ms delays bounds root/job/group/adoption observation; it is not a public timeout/config knob. Budget expiry is visible cleanup IOException, never presumed completion.

12. Implement the supervisor-locked Linux adopted-child drain only **after checked group termination and observed root exit**, retaining the root unreaped. Enumerate `/proc/self/task/*/children`, deduplicate exact child PIDs, exclude root, verify each candidate's getpgid equals the held root group, and reap only those exact candidates with waitpid(candidate,WNOHANG). Pending intermediate parents require another bounded observation pass; after each reap reread children to catch newly adopted deeper descendants. Root exit observation precedes scanning so kernel reparenting has occurred. ECHILD/ESRCH stale candidates trigger a fresh pass; a disappearing task directory triggers retry, not a silently empty result. Persistent /proc/native errors or budget expiry are cleanup IOException. Complete only after a full pass has no owned pending/adopted descendant, then reap root last. Never set a global subreaper, call waitpid(-1), reap unrelated children, kill from this scan or use Process.HasExited to classify orphan zombies as executing. This is the recorded private Type-2 choice; replace/remove it only when an authoritative supported primitive provides equivalent root-excluding adopted reaping, retaining the same tests and public boundary.

13. Make failure cleanup itself joinable. The root observer uses only nonblocking native calls and awaited cancellable delay; Windows root/job observation does likewise. Cancel the exchange observer/I/O tokens and await all already-started work, then perform bounded checked cleanup observation/reaping with a separate private deadline, not the cancelled caller token. Reuse .NET 10 PipeStream cancellation (Unix socket async; Windows async-over-sync CancelSynchronousIo) instead of adding blocked unmanaged I/O tasks. If Terminate fails, record it, stop/join observers and I/O, and perform only safe nonblocking/bounded observation; never block indefinitely in waitid/waitpid/WaitForSingleObject, abandon a task, or claim that a process was removed after OS cleanup failed. Do not reap/release the POSIX identity before any permitted remaining group operation. Native observation/termination/reap failures enter the cleanup failure list, taking precedence over caller cancellation. `ThrowExchangeFailure` throws ExecProcessCleanupException preserving cleanup operation/error and original failure context; only successful cleanup permits normal caller OCE. Ordinary timeout/size/I/O keep failed envelopes, malformed stdout/missing outputKey keep existing ExpertLoadException, and start/nonzero errors retain current visible behavior. Native ownership/setup failures never fall back to an unowned launch.

14. Prove that error selection safely through the actual private `ThrowExchangeFailure` member via the existing reflection test style: cancelled caller plus original OCE plus controlled cleanup IOException must throw IOException with both contexts, not OCE; ordinary I/O plus cleanup failure preserves both; cancelled caller with no cleanup failure gives OCE; uncancelled ordinary failure retains its original exception/stack. Test cases represent a controlled private boundary, not a real kernel termination/reap fault. Do not externally reap the held root, reuse its PID, sabotage native handles or create a wrong-target-kill window to manufacture a fault. Native behavior tests separately exercise real successful cleanup and native setup errors.

15. Keep existing ONNX features, float[1,n] input named input, probabilities-or-last output selection, index1-or0 and threshold semantics. Own the synchronous load/inference inside an awaited Task.Run(...,CancellationToken.None), allowing sibling branches to launch. Cancellation registration uses SessionOptions.SetLoadCancellationFlag/RunOptions.Terminate and the Run overload. Await native termination before disposing registration/options/session/results; never return on cancellation before native exit, and never write a cancelled score to context. The 130-byte identity fixture SHA256 is `691a2d6476544d32195258db2e889967b1b25964ba89c209363b40bc9593425a`; the 473-byte Loop fixture SHA256 is `8d27a5864193f0d90a225ef7b0ab2e39677b771a082c975878268ba6e912aa5a`. Preserve Python3.13/onnx1.19.0 test-only generation provenance. Native inference uses no generation dependency.

16. Extend the existing test build with a small self-spawning probe. Its child modes exercise unread-stdin closure, duplex pressure, root-exits-first ordinary child/grandchild retention, timeout/caller cancellation, literal argv and cwd/env mapping. Probe children communicate readiness/PIDs in unique scratch files, print valid JSON and inherit stdout/stderr naturally; bounded fixture lifetime and finally cleanup prevent a hung test leaving work. Assert no subsequent sentinel write and no live owned descendant. On Linux PID1 additionally require owned child/grandchild process entries gone after reap, distinguishing execution stopped from zombies. An unrelated .NET Process child must remain alive/waitable by its original owner and later return its requested nonzero exit code. No fixed shared temporary paths.

17. Use existing build.ps1 verify/package owners to publish the probe with normal Release/RID/PublishAot/warnaserror settings into a separate verification subdirectory, run it and preserve raw output; never add it to Write-Package's CLI payload or normal installed product. Every normal four-host package gate runs its native process behavior. In normal GitHub Actions Linux x64 verification, also run the same published native probe as container PID1 with no --init, using the Runner's actual `mcr.microsoft.com/dotnet/aspnet:10.0` runtime image. The existing CI fact GITHUB_ACTIONS plus linux-x64 selects this additional test, with no new user setting or workflow bypass. Docker/image/probe failure fails that gate; it is not skipped. No Runner/Dockerfile edit. Run the full unchanged canonical macOS managed/AOT route at corrected final SHA as well as all four native hosts.

18. Stabilize warning-free managed/focused/full/Release-package checks before the final native gate. Retain normal Core 0.1.8 publication changes and fresh disposable package-consumer verification. Commit/push and update the existing draft PR only as already authorized once stable; attach the PR if newly created. Supervisor receives the full frozen diff/evidence, runs new sequential complete code reviews, owns required CI follow-through, merges, normal publication, installed/published default acceptance and closure. No implementer merge/publication/acceptance/done claim.

#### 4. Verification

All following observations are required at the **corrected final source**, with unique scratch paths, source SHA, commands/exit codes, raw logs and warning counts. Preserve old logs; they do not inherit a PASS onto new source.

| Gate | Command / required observation |
|---|---|
| Focused managed | `dotnet test tests/ForgeMission.Mcl.Tests/ForgeMission.Mcl.Tests.csproj -c Debug -warnaserror --filter "FullyQualifiedName~DurableMissionPackageValidatorTests\|FullyQualifiedName~ForgeTomlReaderTests\|FullyQualifiedName~ExpertLoaderTests\|FullyQualifiedName~AgentToolPipelineTests\|FullyQualifiedName~PipelineTraceTests\|FullyQualifiedName~ExecExpertRunnerTests\|FullyQualifiedName~OnnxExpertRunnerTests\|FullyQualifiedName~ForgeProjectTests"`; shell filter separators are literal pipes, without Markdown backslashes. |
| Full managed | `dotnet build ForgeMission.slnx -c Debug -warnaserror`; `dotnet test ForgeMission.slnx -c Debug -warnaserror`, unfiltered with no new exclusions or suppression. Historical full Release reflection-loader limitation stays documented; it does not replace the normal Debug gate. |
| Release/package | `dotnet build src/ForgeMission.Core/ForgeMission.Core.csproj -c Release -warnaserror`; `make verify-core-package`; normal publication workflow Release tests/metadata checks. Failures are repaired, not replaced with Debug results. |
| Canonical AOT | Normal `make cli-script-test cli-verify verify-terminal-extensions-package` macOS-14 PR job at corrected final SHA. Raw compiler/trim/ILC warnings zero; no minimum-OS workaround. Existing managed exclusions in that script do not replace the separate unfiltered full gate. |
| Four-host native | Existing release PR matrix `make cli-script-test cli-package` on osx-arm64, linux-x64, linux-arm64 and win-arm64. In addition to CLI help/version, each normal package invocation publishes/runs the lifecycle probe with zero warnings. Record actual platform closed-stdin exception identity, literal argv, cwd/PATH, cancellation/timeout, early-root-exit descendant and sentinel observations. |
| Linux PID1 | In that normal linux-x64 CI job, run `docker run --rm --mount type=bind,source=<published-probe-dir>,target=/proof,readonly --entrypoint /proof/ForgeMission.Exec.Probe mcr.microsoft.com/dotnet/aspnet:10.0 --verify-pid1`. No --init. Assert actual PID1, held-root lifecycle, multilevel orphan adoption, no later sentinel, owned process entries removed and unrelated .NET child remains waitable with exact requested exit status. Record image identity/digest and logs. Docker absence/failure in this required CI gate is failure, not skip. |
| Native ONNX Linux | Normal Ubuntu Core publication adapter slice executes numeric identity and genuinely in-flight native cancellation without skip. Assert bounded test lifetime, joined native exit/disposal, sibling branch launch and no cancelled score. |
| Package identity | Normal Core0.1.8/core-v0.1.8 publication, merged provenance/README/license/contents and Parser-Scout0.1.0 dependencies; ordinary immutable/private-feed checks. Version availability rechecked before mutation. |
| Branch API consumer | New unique disposable consumer plus fresh isolated package cache; package refs only, retained source/commands/hash. Generated JSON old/no-assets/nonempty-assets round trips, TryCreate/TryValidate/admitted-name checks, workspace exec aliases/directories/keys, numeric ONNX/cancellation. Branch package is lower-layer proof only. |
| Published API consumer | Supervisor independently restores normally published0.1.8 into another fresh cache with normal feed/credentials; records nupkg SHA/nuspec merged commit/dependencies and same API observations. No locally substituted/sibling DLL. |
| Retained Client ABI | Existing ForgeProjectTests uses actual CLI-selected published Client0.9.3 Application DLL, no recompilation, Create/Open/Reconnect through normal composition and narrow closed HTTP seam. Record DLL/package identities and hash. |
| Installed Project/chat | Supervisor clean merged-main normal `make install`, no RID/RELEASE_TAG/CLI_OUTPUT override. Disposable `forge project create`, then real piped plain Chat using its produced declaration, normal saved login/API route, actual IDs/reply/terminal completion; no fake endpoint/provider. |
| Installed regression | Same normal install/provider/config: init/run real Hands Write→Read, independent file bytes/outside sentinel, tool-free run and cancelled-session cleanup. No prepared override can close the default gate. |

Focused positive/negative requirements remain:

- Distribution excludes local credentials/provider env before evaluation, accepts literal/multiline assets, rejects selected env expressions and preserves full local parsing/diagnostic behavior. Table/array/header/repeated-section regressions accompany parser refactoring.
- Package accepts parameterless/nested/parallel/loop/>2-expert missions, existing kinds and multiple reachable profiles. Reject changed bytes/hash/Executable, escaped/base64 actual JSON excess, noncanonical/case-colliding/reserved paths, unknown/env kinds and missing ONNX model asset. No-assets identity remains unchanged.
- Input admission accepts optional expert inputs, empty literals/token_count and named-over-let precedence; rejects required-missing, duplicate, undeclared, reserved and unreachable widening. Supplied goal:string succeeds; goal:double gets MCL012 at immutable source location; missing supplied source cannot read local disk.
- Replay rejects changed semantics/admitted sets/old checkpoint format and malformed current required shapes. Explicitly mutate both missing and null rootInputs in an otherwise valid checkpoint, plus the actual consumed required-field family. Every invalid result is InvalidContinuation with no additional provider/exec invocation. Fresh scratch keeps relative root inputs/completed writes and executes completed effects once.
- Trace records exact internal keys for sequential/nested/loop/parallel/delta/pause paths; workspace absolutes do not enter root/checkpoint values. Output text remains opaque. Add the focused public-behavior regression in existing ExecExpertRunnerTests: create one workspace with a caller-owned LIVE ConcurrentDictionary backing ArtifactPaths; register a newly staged path after the first exec, then invoke a subsequent exec using that SAME workspace. Observe the newly registered declared input mapped to its verified absolute path only in the process-local JSON/environment, while the caller context/root input and checkpoint input remain the original relative string. Do not construct a second workspace or add a registry mechanism.
- Exec covers fast child never reading stdin at small and near-cap payloads, explicit close-before-exit and close-then-wait, exact platform exception mapping, genuine unrelated I/O errors and caller cancellation. Concurrent stdin/stdout/stderr pressure, exact/over caps and both parent-sleeps and parent-exits-first child/grandchild cases are required. No ordinary descendant or later sentinel remains on successful cleanup. Start/nonzero, cwd, FORGE forwarding, output/status/reason/judge and malformed-output behavior remains compatible.
- Controlled private error-precedence cases assert cleanup IOException wins over OCE and preserves context. They do not claim observed real OS termination/reap failure. Native setup failures are exercised safely without removing an owned root or corrupting a live handle.
- Unix group identity remains held until cleanup/reap; unrelated .NET child ownership is preserved. Native probe records live-state/sentinel evidence separately from Linux PID1 zombie-entry removal. No observer/I/O task continues after return.
- macOS native probe must cover an ordinary fast child with no descendant (observed EPERM on the retained zombie group), a live inherited-group descendant, and an already-exited descendant. Completion requires the retained-root-only observation before reaping. Genuine/controlled cleanup errors still preserve error precedence; an OS permission fault is not claimed without an actual observation.
- ONNX numeric score/threshold, missing feature/model, real in-flight termination/join/disposal and no cancelled context write are observed with exact fixture hashes/provenance.

Read-only observations supporting this plan, not corrected-source acceptance:

- Frozen round1 managed/consumer evidence remains at `/private/tmp/phase76-core-primitives-20261009T220732Z`, including EVIDENCE.md and complete.diff. The actual selected Client DLL hash is `cf472b56aec324111750d6f03a7695e5e134a050bcb11c2a51076aac9cda23ca`.
- Canonical old-source run37999080680 failed ForwardsForgeEnvironmentVariables before AOT (1030 passed/10 skipped/1 failed). Its existing log does not expose envelope Reason; do not assert its exact cause. The disposable public-API reproduction at `/private/tmp/phase76-core-r4-probes/fast-stdin-r3.log` observed 30/30 pass with 1 and 16,384-byte values, 30/30 broken-pipe failures with 262,144-byte values. Separate pipe observation was IOException HResult80131620, inner SocketException native32/Shutdown. This establishes a real supported-input defect and concrete mapping; final tests must also diagnose/reproduce the canonical behavior.
- Old-source release run37999080639 completed all four native hosts successfully. Those help/version builds contain neither corrected launch nor the required new lifecycle/PID1 facts.
- `/private/tmp/phase76-posix-launch-probe/probe-r3.log` observed atomic macOS group launch, parent exit while a descendant held pipes, group kill/join, root reap and no late sentinel. `/private/tmp/phase76-posix-r5-probe/probe.log` additionally observed fixed-root WNOHANG cancellation/join, resumed observation of the same unreaped root, descendant pipe closure and sentinel=false. Root getpgid=-1 after exit is not evidence of group absence; the child-reported group matched the held root PID. This remains disposable managed macOS mechanism evidence, not final native or Linux proof.
- `docker version --format '{{.Server.Version}}'` failed locally because `/Users/ameerdeen/.docker/run/docker.sock` is absent. The exact first PID1 verification is the mandatory normal Linux x64 CI command above. No local Linux/Windows or PID1 PASS is claimed.

Core/library evidence does not close later Runner-image/cloud acceptance. No merge/publication until full corrected code reviews and required current-source gates pass; no task closure until normal published/installed default acceptance passes.

#### 5. Principles that changed a decision

| Implementer rule | Concrete choice |
|---|---|
| 1 — No NIH | Reuse parser/diagnostic/pipe/native ONNX/publication APIs; inspect two actual process libraries before selecting the narrow missing ownership seam. |
| 2 — No duplicate paths | One input traversal, one output-directory convention, one generic exec adapter with private OS implementations; no separate local/cloud interpreter. |
| 3 — Minimum needed | Preserve approved Core producer scope and retained consumer pins; add only lifecycle tooling required to prove supported hosts/PID1. |
| 4 — No speculative abstractions | No process framework, public factory, cancellation knob, injected native backend or serialization compatibility reader. |
| 5 — Stay in scope | Supervisor resolves PID1 adopted-reap and failure precedence; any material implementation deviation returns before edits. |
| 6 — Verified means done | Separate scratch mechanism observations from corrected-source CI, real published package, installed defaults and later cloud proof. |
| 7 — Outline first | Entry functions disclose prepare/start/exchange/cleanup/result and parser stages in their first screen. |
| 8 — Small functions | Coherent native setup, observation, owned reap, row parsing and error-policy steps stay about 20–40 lines. |
| 9 — Top-down order | Both public RunAsync/StreamAsync precede private helpers; launch/lifetime entry points precede native details. |
| 10 — Explicit errors | Native cleanup IOException wins over cancellation; all process/I/O/native work is joined; no fallback/unobserved error. |
| 11 — Shallow nesting | Early shape guards, parser section dispatch and exact error classifiers stay at most two levels. |
| 12 — Separate side effects | Native launch/reap, /proc reads, directory creation and I/O have named boundaries apart from path/argument/shape/error selection. |
| 13 — Zero warnings | Stable managed source first, then unchanged canonical and all-four-host AOT warning gates. |
| 14 — Extract for real reason | New private lifetime owner supplies missing atomic authority/identity; private failure selector is a real precedence boundary, not a test-only hook. |
| 15 — Complexity ≤15 | Refactor actual McCabe30 TOML parser into meaningful stages; measure classic decisions and keep every changed function≤15, preferably≤10. |

#### 6. Open questions and assumptions

No unresolved material design question. ArtifactPaths is the live caller/Runner-owned ConcurrentDictionary reference exposed through IReadOnlyDictionary; the implementation retains it correctly. Round 6 corrected that plan wording and specified the existing-file public-behavior regression. Round 7 adds only the precise private macOS group-completion observation above. This plan selects atomic POSIX groups with retained root and atomic Windows job association; final implementation/native/PID1 verification remain pending. Round 6 full plan reviews passed and its approval is historical; round 7 approval is governed by the current header. The Linux adopted-child drain is the supervisor-recorded private Type-2 choice, with the reversal/removal condition stated above. Ordinary descendants are the lifecycle guarantee; code deliberately escaping its group/job, security isolation, permissions and egress policy are outside this task. Remaining inherited authority is explicit.

Current-source verification, exact platform error observations and actual PID1 proof are readiness gates, not assumed PASS results. Unexpected ABI/platform behavior, unavailable required primitive, version collision or required file/scope expansion returns to supervisor. There is no minimum-OS/toolchain workaround in this plan.

Required reading remains AGENTS/default-path/code-style, the owning forge-mcl/Core and scripts READMEs, current task/plan/locked parent contracts and the actual Runner entrypoint for the scoped runtime fact. Round 7 is the supervisor's bounded documentation correction following the implementer's stopped deviation. No product edit implementing that correction, merge/publication or completion claim is authorized by this plan alone.

Historical implementer round 6: **2026-10-09 23:10:31–23:11:13 UTC**, scratch `/private/tmp/phase76-core-r6-plan.md`. Supervisor round 7 clarification: **2026-10-09 23:23:18–23:23:38 UTC**; this file is the complete current six-section artifact.

## Superseded diagnostic correction plan r10


**Current r10 status: PLAN APPROVED2026-10-10 02:19:30 UTC.** Same implementer observed
**2026-10-10 02:09:58–02:11:28 UTC**. Supervisor transcription of its complete read-only return:
all retained contracts,42-path inventory, sequence and checks below remain applicable, plus the
following diagnostic correction. Current clean source `5a80c56e`, base `8d28dc1`, draft PR77;
production tree unchanged from `8f0fa851`. Supervisor approves r10 after fresh full simplicity
and ownership PASS, independently checking exact scope, existing artifact route, observed report
format, original-failure preservation, bounded polling, security/engineering/API/default gates.
Only paths41–42 may change; production correction and merge/publication remain unauthorized.

## Diagnostic correction r10 — proposed

Both current macOS jobs fail in the pressure child with SIGSEGV/139. Exact failed binaries pass
locally on macOS27; this does not establish cause or close macOS14 verification. Investigation
r11 ended02:09:38 UTC; [evidence](phase-76.4-core-cloud-primitives_completed.md#recurrent-macos-crash--investigation-r11).
Only inventory paths41–42 (`scripts/build.ps1`, `scripts/README.md`) change. No production,
workflow, dependency, public API, payload, cap, timeout, process mechanism or test-fixture change.
Existing locked design applies; no new service/identity/data/UI boundary. Browser/UI N/A.

Add two private script functions below their caller:

```powershell
function Save-NativeProbeCrashReports {
    param([string]$Destination, [DateTimeOffset]$StartedAt)
}
function Read-NativeProbeCrashReport {
    param([string]$Path, [DateTimeOffset]$StartedAt)
}
```

Immediately before the existing native probe invocation record `[DateTimeOffset]::UtcNow`.
Wrap only that invocation; preserve its original exception/nonzero failure unchanged. Failure-only
macOS cleanup invokes the collector. Collector failure is recorded independently and cannot
replace the probe failure. Successful/non-macOS invocations do not collect. No failed-test retry.

Collector contract:

- Search only user `~/Library/Logs/DiagnosticReports` and system
  `/Library/Logs/DiagnosticReports`, nonrecursively, for native probe `.ips` candidates.
- Parse observed separate header/body JSON with built-in `ConvertFrom-Json`. Require exact
  header `app_name` and body `procName`, both `ForgeMission.Exec.Probe`. Parse body `procLaunch`
  using invariant `DateTimeOffset.TryParse`; require launch UTC at/after this invocation start.
  New mtime never authorizes an older process's delayed report. Do not guess legacy formats.
- Poll250ms for at most10s, after failure only. Copy eligible reports once into
  `Destination/exec-probe-crashreports`, separating user/system source directories.
- Record collection start/end, copied filenames/launch times, absent directories/no reports and
  access/read/parse/copy errors in a small status file. Incomplete reports may be retried inside
  that same bounded window. Never copy unvalidated reports or dump unrelated report contents.
- If status writing itself fails, print that diagnostic failure; retain the original probe failure.

Actual local `.ips` fields/launch offset were inspected, and both source directories exist.
Existing `Invoke-Checked` owns probe failure/run log; existing canonical `always()` artifact
already includes Destination. The matrix still uploads only its successful CLI ZIP; no claim that
it retains reports. No new workflow or framework. Reuse Stopwatch, JSON/date/filesystem APIs.

Meaningful controlled scratch verification calls the actual script selector/collector and existing
`Test-NativeExec` with the repository's function-interception pattern, without real crashes,
native rebuilds, OS report-directory writes or public hooks. Prove exact-new launch acceptance,
old launch despite new mtime/wrong identity/malformed launch/unsupported content rejection,
eligible-only copies/user-system separation, no-report/access/read/copy failures, bounded polling,
and original-failure preservation when collection succeeds/fails. Script syntax and existing
`make cli-script-test` pass. No43rd test path; `scripts/tests.ps1` unchanged.

After fresh full sequential plan reviews and explicit approval: reconfirm clean source/inventory,
edit only paths41–42, document scoped behavior/removal condition, run these meaningful checks,
freeze/push PR77, and obtain fresh full sequential code reviews. No repeated full local AOT or
unchanged consumer rebuild for report copying; normal current canonical/four-host gates remain
mandatory. Inspect actual new child crashreport/stack against exact executable/symbols before
proposing any production fix. An absent report is a recorded gap, not a waiver or guessed cause.
After diagnosis the supervisor decides whether retaining this small collector remains justified;
it may not grow into general observability or report/environment bundling.

Supervisor implementation clarification: retain the two reviewed helpers. A cohesive collector
around45lines is acceptable under the approximate20–40line guidance; do not extract only to hit
a count. Polling/foreach use two loop levels and early guards. A per-report try/catch may add one
syntactic level solely to preserve each report's visible failure and continue bounded collection;
this narrow style exception applies only to that diagnostic collector, changes no failure contract,
and is removed with the collector or reassessed after causal diagnosis. No third helper authorized.

All15 implementer principles affect this plan: reuse existing owner/APIs(1), one invocation/no
retry(2), same42paths(3), two coherent private functions(4), evidence before guessed fix(5), failed
gates remain open(6), visible caller flow(7), selection versus collection duties(8), helpers below
caller(9), original failure plus visible collection errors(10), early guards(11), named I/O(12),
unchanged warning gates(13), real failure/test boundary(14), coherent complexity≤15(15).

Open causal question remains the SIGSEGV; no unresolved architecture choice in this diagnostic
plan. Normal immutable publication and fresh published-package/installed-default acceptance
below remain required. Prior source-specific PASS results support unchanged code; no prior
review verdict or other-host success closes the failed macOS gates.

## Retained producer approval history before r11

## Retained complete producer plan — current contracts and earlier approval history

**Status: PLAN APPROVED** 2026-10-10 00:47:38 UTC after fresh full simplicity11 and ownership38behaviors/11gates PASS; supervisor independently checked scope, actual APIs/ABI, native/security/default/dependency/failure gates and current held source. Supervisor transcription of the same implementer’s complete read-only return. Prior r7 artifact moved to [completion evidence](phase-76.4-core-cloud-primitives_completed.md#superseded-round-7-full-plan). [Current design/task](phase-76.4-core-cloud-primitives.md); [verified hosting prerequisite](phase-76.5-runner-process-hosting.md).

**Combined in-plan code correction approved01:03:13 UTC:** retain checked termination, join all
outstanding pipe work, then perform final native join/exact root reap. Existing native probe must
independently prove cancellation of a blocked read and filled blocked write on the exact BCL
anonymous-pipe type while opposite endpoints remain open; require completion within5s before
peer closure, and close owned peer endpoints/join pending work on verification failure. No
abandoned task, replacement pipe framework, product factory, public option or additional file.
This enforces the already-approved retention/cancellable-I/O contract; full
[code review findings](phase-76.4-core-cloud-primitives_completed.md#full-code-review-r3--init-correction).

## 1. Files

**In-plan probe correction approved2026-10-10 01:37:02 UTC:** only
`tests/ForgeMission.Exec.Probe/Program.cs`, normalize the canonical relative suffix with
`relative.Replace('/', Path.DirectorySeparatorChar)` before combining the expected absolute path.
Preserve the unchanged-relative-input assertion and every probe/deadline. Windows native failure
is an evidenced expectation defect; Core already normalizes correctly. No production change is
justified by the unexplained canonical macOS SIGSEGV: the exact artifact passed direct pressure
and full public-probe reproduction, and its same-source macOS sibling passed. After the probe
correction, fresh full code reviews and normal current-source canonical/four-host CI remain
required; no previous failure is waived. Existing full managed/package results remain evidence
for unchanged product sources; run the corrected managed probe and script checks before pushing.

Complete revised Core plan r9. Assignment start **2026-10-10 00:39:04 UTC**; observed stage end **2026-10-10 00:41:04 UTC**. Plan only: no file, branch or external state changed.

Continue `/Users/ameerdeen/progs/forge-mcl`, branch `adeen/phase-76-core-primitives`, pushed `dc03b7b9f295bf923436eaec5f645771142e061a`, draft PR77, against intended base `8d28dc1ff8f127facfd708b1c89369179e98cf18`. Preserve the four held test/document corrections. Recheck source/status and immutable version availability before implementation.

All43 paths below are relative to that product repository. This is the complete current inventory, including retained implemented work and failed-matrix diagnostic retention.

## Complete round 11 plan reviews — failed matrix retention

Same implementer observed02:54:07–02:56:02 UTC, read-only. Complete current43-path plan returned,
only42README/43release.yml edits permitted. Source clean0d0a338f/base8d28dc1/tree6c7cfd84.
Reviewed plan SHA256 `F3575D421F1A05D64F97AB70B25B93A1E2296D73D2ABE3F3E0E2536851DCB1E0`,
independently matched by supervisor and both reviewers before approval stamp.
Simplicity assignment02:58:19 UTC; observed02:58:42–02:59:49, full11 PASS. Ownership assignment
03:00:12 after simplicity finished; observed03:00:42–03:01:34 UTC, complete behavior/technical PASS.
No previous PASS inherited; owners derived blind first from atlas/Core/build/Runner READMEs.

| Simplicity check | Verdict / observation |
|---|---|
| New apps/libraries | PASS: existing artifact action/collector/probe |
| Reuse | PASS: script selects, workflow transports; existing producer owners |
| Multiple paths | PASS: one failure-evidence route, successful release unchanged |
| Legacy paths | PASS: actual Client ctor retained, old checkpoints refused |
| Knobs | PASS: six fixed paths/condition/name, existing caps/deadlines |
| Speculative abstractions | PASS: direct YAML, no helper/wrapper/framework |
| Library choice | PASS: existing actionv7/installed Psych/retained ONNX/BCL |
| Copy-paste | PASS: no duplicated selection/publication |
| Redundant definitions | PASS: existing destinations; name outside forge-* |
| Size/requirement | PASS: only README/workflow, one inventory addition |
| Test volume | PASS: meaningful YAML/routing/preservation; all final native/causal/pub/default gates retained |

| Behavior | Fresh derived owner / proposed placement | Verdict |
|---|---|---|
| Distribution before env evaluation | Core manifest/shared parser | PASS |
| Normal local TOML | Core manifest/shared parser | PASS |
| Immutable source without disk fallback | Core expert loader | PASS |
| Declared parameters typed string | Core expert loader | PASS |
| Explicit assets/executable metadata | Core contracts | PASS |
| Actual Client six-argument ABI | Core contract overload | PASS |
| Generated semantic JSON constructor | Core contract producer | PASS |
| Pure package construction/validation | Core validator | PASS |
| First/parameterless root | Core validator | PASS |
| Internally derived reachable names | Core input policy | PASS |
| Exact reserved names/token_count | Core input policy | PASS |
| Required root/optional expert inputs | Core validator | PASS |
| Reachable profiles | Core semantic validator; Runner availability | PASS |
| Existing kinds/unknown-env refusal | Core semantic validator | PASS |
| Asset paths/collisions/digest/model containment | Core package validator | PASS |
| Old hash/asset identity extension | Core package validator | PASS |
| Actual generated JSON4MiB | Core package validator | PASS |
| Complete semantic fingerprint | Core replay | PASS |
| Checkpoint shape/old-format refusal | Core codec | PASS |
| Relative inputs/effects once | Core interpreter/checkpoint | PASS |
| Live registration authority | Caller/Runner; Core view only | PASS |
| Deterministic output paths | Core workspace primitive | PASS |
| Nested workspace/exact StepKey | Core interpreter/trace | PASS |
| Process-local verified mappings/aliases | Core exec | PASS |
| cwd/lookup/argv/remaining env | Core exec | PASS |
| Concurrent bounded streams | Core exec | PASS |
| Concrete declined stdin | Core exec | PASS |
| PID1 preallocation refusal | Core start boundary | PASS |
| Atomic POSIX retained root | Core private lifetime | PASS |
| macOS root-only proof | Core private POSIX lifetime | PASS |
| Atomic Windows job/handle ownership | Core private lifetime | PASS |
| Adopted orphan reap | Existing image init | PASS |
| Terminate→pipejoins→reap/error precedence | Core lifetime/failure | PASS |
| Numeric/joined ONNX cancellation | Core ONNX | PASS |
| Pressure/pipe/workspace/parallel proof | Core tests/probe/build invocation | PASS |
| Exact image/init and PID1 negative | Build verification/native probe | PASS |
| Scoped crash selection/copy | Existing build diagnostic collector | PASS |
| Failed macOS retention | Existing matrix artifact transport | PASS |
| Six report/log/binary/symbol/sidecar paths | Build artifact transport | PASS |
| Diagnostics excluded from shipped release | Existing release owner/distinct name | PASS |
| Failed build/upload remain visible | Build verification boundary | PASS |
| Child report/binary/symbol UUID correlation | Supervisor causal investigation | PASS |
| Immutable Core/actual Client/fresh restore | Existing package/release owners | PASS |
| Normal installed Project Chat/Hands | Supervisor default acceptance | PASS |

| Technical gate | Verdict / observation |
|---|---|
| Scope/dependency | PASS:43inventory; only2edits; no new dependency/version |
| API/ABI | PASS: exact producer shapes/constructor/generated JSON retained |
| Security/data/credentials | PASS: no endpoint/store/identity/permission boundary; scoped artifacts only |
| Engineering/failure | PASS: script selects/workflow transports; failed job/upload remains failed |
| Release routing | PASS: actual CLI_OUTPUT/dist paths/ZIP pattern/dependencies/permissions checked |
| Verification readiness | PASS: YAML/exact-step/negative-routing/scope checks; no unchanged local rebuild |
| Native/causal | OPEN: actual missing child report/symbol evidence, matrix SIGSEGV not waived |
| Publication/default | OPEN: immutable publication/fresh package/installed defaults required |
| UI | N/A: no visual/layout change |

No component gains a second job; named-Forge searches found no competing implementation or failed
matrix route. Reviewer recommendation: move nothing. Supervisor independently checked full plan
identity/source clean0/0, actual successful canonical archive probe/dSYM/sidecar paths, original
failure, six allowed paths, successful ZIP/publisher separation, unchanged permissions and all
retained producer gates. **PLAN APPROVED03:02:53 UTC**, only README/release-workflow correction.
No production fix, Native AOT waiver, merge/publication or completion approved.

## Matrix retention — implementation r11

Same implementer observed03:03:53–03:06:00 UTC on2026-10-10, after supervisor approval03:02:53.
Exact implementation-assignment boundary was not separately recorded; those observed times do
not stand in for it. Supervisor received the final handback before03:07:48 UTC.
Committed/pushed `c4176ba5598b77538829350e8bb7c8fc986bf8b0`, PR77, clean branch with0/0 upstream
count independently observed by supervisor. Increment only release.yml/scripts README21+/3-;
full43paths2716+/457-, binary diff SHA256
`4b90536b4e8db66929df06935448c5dc7ce05805e9bd3e4d1b368b17a0005131`.
Production tree unchanged `6c7cfd841a81fd60ad0fd3d7213ae75e4bd202f9`; tests/collector also unchanged.

Evidence: `/private/tmp/phase76-core-matrix-retention-20261010T030353Z/EVIDENCE.md`, full.diff,
inventory.txt, correction.diff and raw retention/parser/script logs. Supervisor read actual
two-file diff and logs:21parsed routing/preservation/scope checks,4PowerShell parsers and30existing
CLI script checks PASS; diff hygiene PASS. Existing Ruby PATH warning retained; initial parser-log
redirect orchestration error corrected and recorded, with no source change. No new warning waiver.

Existing matrix action now uploads the six approved paths only on failed macOS builds, using a
distinct diagnostic name excluded from forge-* publication. Original build/upload failures remain
failures. Successful CLI ZIP/publication/permissions/commands unchanged. No production fix guessed.
Normal push dispatched canonical38019324267 and four-host38019324271 at03:05:20 UTC; supervisor
observed both in progress at reviewed HEAD. Required fresh full code reviews, actual native causal
evidence, final required checks, immutable publication and published/default acceptance remain open.

## Full code review r7 — failed matrix retention

Both existing reviewers independently re-read the complete current43-path artifact at
`c4176ba5598b77538829350e8bb7c8fc986bf8b0`, base8d28dc1; no inherited PASS or correction-only
review. Both matched binary diff SHA256
`4b90536b4e8db66929df06935448c5dc7ce05805e9bd3e4d1b368b17a0005131`,43paths2716+/457-, clean0/0
branch and production tree6c7cfd84. Separate full simplicity/style tables, then full ownership
table; no actionable source correction found. Source PASS does not close native/default gates.

| Stage | Assignment start UTC | Result available UTC | Wall | Agent observed activity UTC |
|---|---|---|---|---|
| `[review-code:simplicity:r7]` including full style | 03:07:48 | 03:11:52 | 4m04s | 03:08:20–03:11:24 |
| `[review-code:ownership:r7]` | 03:11:52 | 03:17:02 | 5m10s | 03:12:25–03:15:58 |

All2026-10-10. Result-available boundaries measured by supervisor after receiving final verdicts;
agent-observed ends are recorded separately, not substituted for assignment ends.

| Simplicity check | Verdict / current evidence |
|---|---|
| New apps/libraries | PASS: no dependency; probe uses existing Core |
| Reuse | PASS: shared input policy/parser/interpreter/collector/action |
| Multiple paths | PASS: OS launch implementations own required lifetime; one interpreter |
| Legacy paths | PASS: actual Client0.9.3 ctor; old checkpoints refused |
| Knobs | PASS: fixed cleanup/output/diagnostic conventions |
| Speculative abstractions | PASS: native identities/resources and caller-owned workspace view |
| Library choice | PASS: existing ONNX1.27/BCL pipes |
| Copy-paste | PASS: shared invocation; existing uploader/collector |
| Redundant definitions | PASS: existing StepKey, one output convention, distinct artifact routing |
| Size/requirement | PASS:43approved paths; latest21+/3- only two retention files |
| Test volume | PASS: meaningful admission/replay/ABI/live-registry/pressure/cancellation/lifecycle proof |

| Code-style check | Verdict / current evidence |
|---|---|
| Outline first | PASS: public preparation/execution before named details |
| Small functions | PASS with recorded narrow46-line collector exception |
| Top-down order | PASS: public Run/Stream/build orchestration before helpers |
| Explicit errors | PASS: cleanup precedence, narrow declined stdin, failed build/upload preserved |
| Shallow nesting | PASS with recorded bounded per-report catch exception |
| Separate side effects | PASS: pure validation/fingerprints separated from process/artifact effects |
| Zero warnings | OPEN for pending current native jobs; supporting unchanged Debug/Release0warnings; Ruby PATH warning disclosed |
| Extract for real reason | PASS: helpers follow admission/identity/ownership/exchange/report selection |
| Complexity | PASS: manual classic McCabe; counts below |

Manual count base1 plus branches/loops/nondefault cases/catches/ternaries; no separate boolean
operand or final-else count. Exchange8, POSIX Join7, AddRow9, pipe-cancellation10, child dispatcher12,
collector12, selector5, retained typed-key walk13. Reassess collector after diagnosis; remove/merge
nothing currently required. Reviewer read actual raw191focused/1063full(6existing skips)/407Release,
fresh branch consumer/managed probe and current21routing/4parser/30script evidence; historical
native success is not current acceptance.

Ownership derived blind before plan/diff from actual Desktop atlas/Core/build/Runner READMEs:

| Behavior | Derived owner / actual placement | Verdict |
|---|---|---|
| Explicit package assets | Core manifest | PASS |
| Distribution without provider evaluation | Core manifest reader | PASS |
| Full local configuration parsing | Same Core reader | PASS |
| Immutable diagnostics/no disk fallback | Core expert validation | PASS |
| Declared mission parameters as strings | Core expert validation | PASS |
| Package construction/first root | Core package validator | PASS |
| Resolved content/language validation | Core validator/ExpertLoader | PASS |
| Existing kinds/reachable profiles | Core semantic validation | PASS |
| Reachable declared inputs | Core input policy | PASS |
| Invalid/reserved/duplicate names/root requirements | Core validator/input policy | PASS |
| Exact reserved policy/token_count | Core input policy | PASS |
| Asset identity/old hashes | Core package validator | PASS |
| Actual serialized4MiB | Core package validator | PASS |
| Portable collisions/runtime directories/model assets | Core package validator | PASS |
| Actual Client six-argument ABI/generated JSON | Core public contract | PASS |
| Published Client Create/Open/Reconnect regression | Core consumer tests | PASS |
| Admitted inputs across resume | Core interpreter/checkpoint | PASS |
| Complete semantic identity/no scratch | Core replay fingerprint | PASS |
| Old/malformed checkpoint refusal | Core codec | PASS |
| Completed effects replayed once | Core interpreter/log | PASS |
| Live registry authority | Caller owns registration; Core retains view | PASS |
| Deterministic step output directory | Core workspace convention | PASS |
| Nested workspace inheritance | Core interpreter | PASS |
| Exact StepKey lifecycle/tool/delta facts | Core trace/interpreter | PASS |
| Verified process-local path/environment mappings | Core exec adapter | PASS |
| Literal argv/cwd/lookup | Core exec/private launch support | PASS |
| Concurrent bounded stream exchange | Core exec adapter | PASS |
| Concrete declined stdin/other errors | Core exec adapter | PASS |
| Atomic POSIX group/retained root | Core private lifetime | PASS |
| macOS root-only proof/exact reap | Core private lifetime | PASS |
| Atomic Windows job/handle ownership | Core private lifetime | PASS |
| Termination/pipe joins/exact reap/disposal | Core lifetime | PASS |
| Cleanup IOException precedence | Core exec failure boundary | PASS |
| Bare LinuxPID1 preallocation refusal | Core launch boundary | PASS |
| Adopted orphan reaping | Existing image init, excluded from Core | PASS |
| Numeric ONNX/expert-relative model | Existing Core ONNX | PASS |
| Parallel sibling/joined native cancellation | Existing ONNX/interpreter | PASS |
| Actual BCL pipe cancellation/duplex pressure | Public-Core native probe | PASS |
| Exact Runner image/entrypoint/negative topology | Build verification | PASS |
| Scoped new-report selection/original failure | Build verification | PASS |
| Failed macOS artifact transport/release exclusion | Existing release workflow | PASS |
| Immutable package/provenance/visibility | Existing package workflow/verifier | PASS |

No owner gains a second job. Named-Forge searches found consumer adapters calling Core validation,
not duplicate validators; wire admission/profiles/project identity stay outside Core. No competing
launcher/replay codec/registry/collector. Move nothing.

Technical review: source scope/security/data/credentials/API/ABI/generated JSON PASS; current
21routing/4parser/30scripts PASS; current Linuxx64 native/image proof PASS from actual raw log.
Remaining current native/zero-warning gates, actual crash report plus matching UUID, normal
publication/fresh restore and installed Project/Chat/Hands defaults OPEN. UI N/A.

Supervisor independently checked the actual two-file delta, all43-path inventory/hash, production
and full-tree identity, raw checks/logs, clean0/0 status and unchanged failure/release boundaries.
Synthetic merge `049c61029f0a766992f02a462c1710d97eb3b512` has parents8d28dc1/c4176ba5 and exact
reviewed full tree `c802391445fd7d105a697295adb6f760080f250b`. Source accepted03:17:50 UTC;
merge/publication/causal/default approval withheld.

Current Linuxx64 job114116566177 SUCCESS, raw `/private/tmp/phase76-core-r11-linux-x64.log`:
identity0.10.1-dev.8+049c61029f0a766992f02a462c1710d97eb3b512;30scriptchecks; native pipes0/0ms,
pressure/workspace/argv/cwd/lookup/exited-descendant/cancel/timeout PASS. Exact published Runner
index/platform/source and unchanged entrypoint verified03:14:10; tiniPID1/dotnetRunner7/probe14.
Bounded orphan-entry disappearance03:14:12/03:14:14 and no later sentinel independently asserted.
Bare PID1 refused before child/root/sentinel03:14:17; owned container removed. Actual log has no
compiler/AOT warning; git's default-branch hint contains the word warning but is not a compiler
diagnostic. Other current native jobs still running at source acceptance; no all-host PASS inferred.

Current Linux ARM64 job114116566338 SUCCESS: raw `/private/tmp/phase76-core-r11-linux-arm64.log`
independently read by supervisor. Exact same synthetic source identity verified03:17:13;30script
checks, native pipe read/write cancellation0/0ms, pressure/workspace/argv/cwd/lookup/exited-descendant
and root-first timeout/cancel PASS03:17:42–03:17:48. No compiler/AOT warnings found. At03:22:13 UTC,
Windows and both macOS jobs still running; no remaining-platform PASS or causal conclusion inferred.

Current Windows ARM64 job114116567016 SUCCESS, completed03:23:08 UTC. Supervisor read actual
`/private/tmp/phase76-core-r11-windows-arm64.log`: identity0.10.1-dev.8+049c61029f0a766992f02a462c1710d97eb3b512;
30scriptchecks; blocked read3ms/write0ms; pressure/workspace/argv/cwd/lookup/exited-descendant/
root-first timeout/cancel PASS03:22:36–03:22:43. No compiler/AOT warnings found. Both macOS jobs
still pending at this observation; no causal conclusion or all-host PASS inferred.

## Matched native crash — investigation r13

Current source `c4176ba5598b77538829350e8bb7c8fc986bf8b0`; actual synthetic merge
`049c61029f0a766992f02a462c1710d97eb3b512` has the identical source tree. Matrix38019324271
macOS job114116566327 failed pressure-child139 and parent134. Canonical38019324267
job114116565649 independently failed pressure139 at03:38:15 UTC, completed03:38:46 UTC.
Linux x64/ARM64 and Windows ARM64 current checks passed as recorded above. No retry or waiver.

### Retention and exact identity

The failure-only matrix upload succeeded: [artifact11658167304](https://github.com/katasec/forge-mcl/actions/runs/38019324271/artifacts/11658167304),
`native-exec-diagnostics-osx-arm64-38019324271-1`,23,340,627bytes,
archive SHA256 `435b538a51cf9a09f185abcd710043fd56630801cf98dfc832a1f156e3b53b42`.
It contains two actual reports plus logs and exact executable/dSYM. Collector copied both user
reports, no errors/absent directories, and preserved original failed probe134. This closes the
retention observation only, not runtime acceptance. The successful ZIP upload remained skipped.

Root downloaded reports to `/private/tmp/phase76-core-r11-matrix-reports` and complete symbols to
`/private/tmp/phase76-core-r11-matrix-symbols`; independently matched executable AND DWARF UUID
`E1082A2A-29B5-396E-B741-3491896B2982` to the child report before successful atos symbolication.
Binary SHA256 `ccb7fd1b0ae1882d6baaacc7b871415b23efddd90306ec902b20784089ef3c6f`.
`matched-child-symbolication.json` retains actual command observations. An initial dwarf check
before the download finished failed; the completed-download check succeeded, with no diagnostic.
Raw failed logs: `/private/tmp/phase76-core-r11-macos-arm64.log` and
`/private/tmp/phase76-core-r11-canonical.log`. Emitted CI error time is not asserted to be crash time.

### Child and parent are separate failures

Child39655 launched03:34:06.3550 UTC, faultingThread2: SIGSEGV/EXC_BAD_ACCESS,
KERN_INVALID_ADDRESS0, instruction-abort PC0/FAR0. Registers x0=30(SIGUSR1), x1/x2 point to
signal info/context, x3=0, x8=66(0x42). Reported UTF8 frame imageoffset0x2e2be8 maps to an ADD
following a direct ASCII-widening call, not an indirect null branch. Exact matching binary's
ActivationHandler instead loads saved handler into x3 and saved flags into w8, tests SA_SIGINFO,
then tail-branches `br x3` at0x10006f52c. Report state matches a null previous-handler chain.
No ONNX image was loaded. Parent39652 is a separate SIGABRT assertion failure after child139;
its FailFast/TaskAwaiter/Program frames are not the child cause.

### Report-grounded mechanism and bounded correction

Same implementer read-only stage observed03:36:57–03:41:36 UTC; result available to supervisor
by03:42:57 UTC (the exact arrival boundary was not captured). Root independently read matched
reports/disassembly and executed the safe scratch disposition probe at03:43:26 UTC:

```
child mode=implicit-exec-default handler=0x0 flags=0x42 SIGUSR1=30 SA_SIGINFO=1
child mode=explicit-default handler=0x0 flags=0x0 SIGUSR1=30 SA_SIGINFO=0
```

This installs a harmless caught action only in its own scratch process, spawns inspection-only
children with existing group/CLOEXEC flags, and waits exact children. No signal delivery, crash,
workload stress loop, AOT rebuild or repository mutation. clang -Wall -Wextra -Werror passed.
Local host27.0.1 differs from CI14.8.9: this proves the launch-state mechanism, not corrected CI
or published acceptance. The first scratch attempt lacked preserved stdout under CLOEXEC_DEFAULT;
only the corrected captured probe supplies evidence. Full scratch evidence:
`/private/tmp/phase76-core-native-r13-20261010T033657Z/EVIDENCE.md`.

Exact embedded runtime identifies commit95017c711e6afc1085133d440e42b4bd78155701.
Its [NativeAOT ActivationHandler](https://github.com/dotnet/dotnet/blob/95017c711e6afc1085133d440e42b4bd78155701/src/runtime/src/coreclr/nativeaot/Runtime/unix/PalUnix.cpp#L1050)
chains sa_sigaction whenever SA_SIGINFO is set. [System.Native documents macOS default plus SA_SIGINFO](https://github.com/dotnet/runtime/blob/v10.0.12/src/native/libs/System.Native/pal_signal.c#L63)
and its [normal process launcher resets caught dispositions before exec](https://github.com/dotnet/runtime/blob/v10.0.12/src/native/libs/System.Native/pal_process.c#L365).
[Apple's spawn SETSIGDEF path](https://github.com/apple-oss-distributions/xnu/blob/xnu-10063.141.1/bsd/kern/kern_exec.c)
sets default actions with flags0; its exec signal reset leaves the signal-info bitmap.
The Apple tag is relevant23-series source, not claimed exact CI23J631 source.

Strongly supported cause: macOS exec resets caught handler to default/null while retaining
SA_SIGINFO; the child NativeAOT activation handler later chains that null action. Limits:
no crash-time saved-handler memory snapshot and no exact system-LR symbolication. This supports
private pre-exec caught-disposition default attributes; it does not justify a serialization rewrite,
signal-mask reset, blanket ignore removal, runtime patch, deadline change or retry. Supervisor
correction design is in the active spoke; full design/plan reviews and approval must precede edits.

## Implemented retention design r11

### Implemented retention correction

Retention r11 is implemented and verified by an actual failed-matrix artifact containing two
reports and exact UUID-matched symbols. See the completed record for the prior missing evidence.

This remains the locked build-verification design and owner, not a new product/security boundary.
No design decision is deferred to implementation; no new hosted service, permission, dependency,
public API, retry, payload, deadline, warning policy or process mechanism. Existing Core design
and full producer Done when remain authoritative. Product/default gates apply; visual/browser N/A.
The plan must explicitly expand the full inventory from42 to43 paths by adding
`.github/workflows/release.yml`; only that workflow and `scripts/README.md` may change now.
Other existing product paths remain frozen at0d0a338f. Both complete plan reviews passed and
supervisor approved03:02:53 UTC; the same implementer delivered only the two permitted files.

Use the already referenced `actions/upload-artifact@v7` in the existing matrix build job, after
the native build, only on failed macOS execution. Give the diagnostic artifact a distinct name
outside the release publisher's `forge-*` download pattern. Retain only the existing validated
`dist/cli/exec-probe-crashreports`, native probe run/publish logs, exact probe executable/dSYM and
its required native sidecar. Do not upload the general DiagnosticReports tree, environment,
credentials, broad workspace or shipped CLI payload. Preserve the existing successful CLI ZIP
artifact unchanged and preserve the original failed job; upload failure cannot make it pass.
Use the normal current-source CI route; collect faulting report and exact matching symbols before
any production fix. The completed record must state that the earlier reports were not retained.

The unchanged collector retains the previously reviewed narrow style exception: its cohesive
46-line body and per-report try/catch beyond two syntactic nesting levels are allowed only for
bounded independent report failure handling. Two helpers remain; no metric-only extraction.
Reassess this exception with the collector after diagnosis.

Verification: inspect actual output/artifact paths, YAML parse/action condition/name/path routing,
existing script checks, fresh complete sequential code reviews, and normal canonical/four-host
checks. No unchanged local AOT/managed/consumer rebuild merely for artifact retention. The decisive
diagnostic observation is a normally uploaded failed-matrix artifact containing its actual child
report and matching executable/symbol UUID. Missing evidence remains an open gate. Reassess
diagnostic retention after diagnosis; it cannot grow into general observability.


### Corroborating canonical report

Root downloaded canonical artifact11658107726,151,658,447bytes, from
[run38019324267](https://github.com/katasec/forge-mcl/actions/runs/38019324267).
Its one retained child report is PID48596, launch03:38:10.6848 UTC, SIGSEGV PC0/FAR0,
faultingThread2, x0=30/x3=0/x8=66: the same signal-shaped null-call register state.
UUID644a0ac4-6305-3357-948c-77f8494378e6 differs from the independently built matrix binary;
root did not use matrix symbols to claim exact canonical symbolication. Reports/logs retained in
`/private/tmp/phase76-core-r11-canonical-reports`; parsed-report command completed successfully.

## Complete correction design reviews r12

Supervisor applied full designer persona, observed03:43:26–03:44:00 UTC. Source remains frozen
c4176ba5. Both reviewers read the complete current Core design, derive/recheck their own evidence,
and treat earlier verdicts as history. Root independently inspected actual child reports, matched
UUIDs/disassembly, executed the safe disposition probe and checked SDK signal/spawn declarations.
Design locked03:48:45 UTC; same implementer plan assigned03:48:49 UTC. No code authorized.

| Stage | Supervisor assignment clock → result available (UTC) | Agent observed (UTC) |
|---|---|---|
| review-design:simplicity:r12 | 03:44:00 →03:46:28 | 03:44:30–03:46:08 |
| review-design:ownership:r12 | 03:46:28 →03:48:45 | 03:46:53–03:47:49 |

### Simplicity — complete current design

| Check | Verdict / current evidence |
|---|---|
| New apps/libraries | PASS: existing Core/libc/probe, no dependency |
| Reuse | PASS: shared parser/validator/interpreter/adapters; private POSIX launcher |
| Multiple paths | PASS: one exec path; observed macOS ABI correction in existing platform branch |
| Legacy | PASS: reject checkpoint2; actual loaded Client0.9.3 six-arg ABI retained |
| Knobs | PASS: fixed caught defaults; no mask/retry/permission setting |
| Speculative abstractions | PASS: private PosixNative ABI and existing failure owner |
| Library | PASS: existing ONNX1.27/libc; matched report/source/repro |
| Copy-paste | PASS: existing semantic traversal/diagnostic parser/probe dispatcher |
| Redundant definitions | PASS: no second runtime/signal API; exact private ABI is plan gate |
| Size | PASS: producer and observed macOS boundary only |
| Test volume | PASS: caught/ignored/parent observations plus unchanged decisive/native/default gates |

Remove/merge nothing. Exact ABI and regression arrangement require plan review; no current corrected
native or published acceptance claim.

### Ownership — complete current design

Blind derivation first: forge-desktop/src/README atlas, Core README Why/Owns, scripts README and
Runner README. Named Forge repo searches found no existing caught-default implementation or
competing semantic/process owner. No component gains a second unrelated job.

| Behaviour | Derived / proposed owner | Verdict |
|---|---|---|
| Explicit assets | Core manifest DTOs | PASS |
| Distribution before environment evaluation | Existing ForgeTomlReader | PASS |
| Immutable package construction/validation | Core package validator | PASS |
| Old hashes and extended asset identity | Same canonical hash | PASS |
| Actual serialized4MiB cap | Same generated JSON validator | PASS |
| Paths/collisions/bytes/models | Same semantic/asset validation | PASS |
| Parameterless roots/reachable inputs | Core shared semantic/input traversal | PASS |
| Exact reserved names/token_count | Core input policy | PASS |
| Existing expert kinds/profile names | Core validation; deployment owns availability | PASS |
| Immutable diagnostics/no disk fallback | Core ExpertLoader | PASS |
| Mission parameter typing | Existing ExpertLoader availability map | PASS |
| Actual Client constructor ABI | Core package contract | PASS |
| Generated JSON construction | Same annotated semantic constructor | PASS |
| Admitted inputs across pause | Core interpreter/checkpoint3 | PASS |
| Complete replay fingerprint | Core replay identity | PASS |
| Malformed/old continuation rejection | Core codec before invocation | PASS |
| Completed-effect replay | Core interpreter/log | PASS |
| Live artifact registry | Core workspace view; Runner registers | PASS |
| Child workspace inheritance | Core interpreter options | PASS |
| Deterministic outputs | Core StepKey/attempt convention | PASS |
| Exact trace StepKey | Core trace/interpreter | PASS |
| Verified process-local path conversion | Core exec adapter | PASS |
| Literal argv/cwd/environment | Core exec/private launcher | PASS |
| Concurrent bounded I/O | Same exec adapter | PASS |
| Precise declined stdin | Same I/O failure owner | PASS |
| Atomic group/job | Core private lifetime | PASS |
| Retained root/termination/joins/reap | Same lifetime | PASS |
| Cleanup error precedence | Same failure boundary | PASS |
| BareLinuxPID1 refusal | Same prelaunch guard | PASS |
| Adopted orphan reaping | Existing image init, outside Core | PASS |
| Numeric ONNX/joined cancellation | Existing Core ONNX adapter | PASS |
| Derive caught macOS dispositions | Existing Core ConfigureSpawn/private ABI | PASS |
| Default caught before exec | Same checked spawn attributes/group | PASS |
| Preserve ignored/default/mask/parent | Same launch owner, no global mutation | PASS |
| Signal setup failure/disposal | Existing start failure/resource boundary | PASS |
| Public-adapter signal observation | Existing native probe/child dispatcher | PASS |
| Native gates/scoped retention | Existing build/release owners | PASS |
| Publication/installed defaults | Existing package/build; supervisor accepts | PASS |

| Technical gate | Verdict |
|---|---|
| Causal evidence | Strongly supported, qualified; exact matrix symbols/registers/source/repro |
| Canonical report | Corroborating PC0/x3=0/x8=66/SIGUSR1; differentUUID, no cross-symbol claim |
| ABI | Plan must specify exactlayout/range/flags/errno/checkedcleanup |
| Regression | Plan must avoid runtime reinstall masking observation; ignored and parent preservation |
| Security | PASS: no new service/store/identity/credential/authority/isolation |
| Engineering | PASS: one owner/fixed platform correction/no blanketreset/retry/framework |
| Corrected native | OPEN: correction unimplemented; both currentmacFAIL |
| Publication/default | OPEN: normal Core publication/freshrestore/cleanmainHands/Chat |
| UI | N/A |

Move nothing. Supervisor agrees with both complete verdicts and locks the bounded design, while
withholding implementation approval until full current implementer plan reviews pass.
