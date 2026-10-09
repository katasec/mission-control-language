# Phase 76.4 — Core package and execution primitives

**Status:** Core corrections are written; full Debug build and focused parser/checkpoint checks passed. Complete r7 with the precise private macOS cleanup clarification passed fresh full reviews and received supervisor approval 2026-10-09 23:27:01 UTC. Final code reviews, current-source native checks, merge/publication and acceptance remain open.
Parent: [Phase 76](phase-76-unified-cloud-run.md).
Design authority: [locked execution contracts](phase-76.2-unified-cloud-run-contracts.md).
Prerequisite: [accepted OCI library](phase-76.3-oci-integrity-auth.md); this task does not consume
or change OCI retrieval. Later Host, Runner and Client tasks consume this task's published package.

## Bounded outcome

Deliver the provider-neutral Core APIs needed to validate and execute the agreed generic cloud
package through the existing interpreter/adapters. Publish and independently restore the exact
package before consumer plans rely on it. This task does not implement cloud admission, binary
storage, Runner staging/collection/progress, Client actions, CLI migration or deployment.

Operator requirement, verbatim:

> Please check the next item on the plan which should be Phase 76 for unified cloud execution. Please kick of  the documented supervisor workflow in order to complete the tasks in that phase autonomously.

Arbitrary user-vetted executable code and persistent Host / ephemeral Runner ownership are settled
in the parent contract. No new permission question or executable whitelist is authorized here.

## Owner and reuse

Only `/Users/ameerdeen/progs/forge-mcl` product files change. Its repository README and
`src/ForgeMission.Core/README.md` were read before scoping. Core owns pure package/manifest semantics,
pipeline execution, trace identity and generic adapters. Existing Parser, ExpertLoader,
ForgeTomlReader, DurableMissionPackageValidator, PipelineRunner/checkpoint and exec/ONNX adapters
remain the implementation. No second interpreter, provider client, registry reader, filesystem
broker or cloud-aware Core dependency.

The complete parent design passed full sequential round 7 simplicity/ownership reviews before
supervisor lock. This bounded task directly implements its Core rows. Round 3 implementation
received approval after both full plan reviews; code review found lifecycle, checkpoint and style
defects. Complete round6 correction plan now passes fresh full sequential reviews; the expanded bounded
file inventory is approved. Any further material deviation returns before product edits.

## Required changes and order

| Order | Existing owning surface | Required behavior from locked design |
|---|---|---|
| 1 | Manifest/package DTOs and validator | Distribution-only metadata read without environment evaluation; explicit assets including executable bit; canonical hash extension with old no-assets hashes unchanged; source-generated actual 4 MiB serialization cap; pure TryCreate and shared input-name validation |
| 2 | Validator semantic traversal | Parameterless roots, reachable named input set, exact reserved names, all existing expert kinds, environment-expression rejection, multiple LLM profile names; validate asset paths/collisions and ONNX model-to-admitted-asset resolution |
| 3 | Pipeline/checkpoint/trace | Full execution-semantic fingerprint, validated admitted inputs retained across nested root pauses, runtime-only workspace inherited by child options, exact internal StepKey exposed on all lifecycle facts; opaque text/status retained |
| 4 | Existing exec adapter | Allocated per-step output bindings, verified artifact relative-path conversion in process-local copies, inherited process authority, shared stdin semantics, bounded concurrent I/O, kill-and-join lifecycle and explicit failures |
| 5 | Existing ONNX adapter | Existing numeric semantics and expert-relative model; joined native cancellation using observed ONNX 1.27.0 APIs, disposed native resources |
| 6 | Existing package/release checks and READMEs | Publish immutable Core version containing these exact APIs, with provenance and actual Linux native inference checks; document current ownership/API semantics |

The plan names every changed file and exact API/DTO shape. `AdmittedInputNames` and
`ProviderProfileNames` on validated output are ordinal `IReadOnlyList<string>` values, distinct and
sorted. Core collects profile names; deployment availability remains Runner's responsibility.
No configured provider secrets are read by package construction/validation. Runtime scratch
does not enter fingerprints, context writes or checkpoint payloads.

Supervisor clarification before plan (2026-10-09 21:44:30 UTC): Core derives the admitted-name
set internally from root parameters and reachable expert inputs using the single shared traversal
and reserved-name policy; there is no caller-widenable run option. Store/check the sorted set in
inner checkpoint format **3**, retaining outer envelope version 1; explicitly reject old inner
format 2 rather than adding a compatibility reader. Engine/build identity already prevents durable
cross-build resume. Asset paths have canonical forward-slash relative spelling; duplicate and all
staged-file collisions use `OrdinalIgnoreCase` for portability, including mission/lock/distribution
metadata/expert markdown paths. Reserved input names/prefixes remain exact `Ordinal` matching.
These complete reversible implementation details within the locked design, with no new owner.

Pure-validation source seam: existing `ExpertLoader.Validate` reads expert files for typed-key
diagnostic locations. Add a final optional `IReadOnlyDictionary<string,string>?
expertMarkdownByName` argument and pass the complete immutable package markdown map from the
validator. When supplied, use existing `SplitFrontmatter`/`FindKeyInBlock`; a missing required
source is explicit failure and never falls back to filesystem reads. Existing local callers omit
the map and retain their file-based diagnostic source. This changes the source of evidence, not
the diagnostic policy, and avoids a second semantic validator.
Verified named-input correction: the existing typed-key walk currently omits mission parameters
and rejects a required `goal` with `inputKeys: { goal: string }` as MCL011. Seed that mission's
declared parameters as strings in its existing availability map without replacing runtime keys;
common admission rejects reserved root parameters. Preserve optional expert-input, binding and
nested-call diagnostic policy. Paired in-memory regressions accept `goal:string` and report the
existing MCL012 for `goal:double`, using supplied source rather than filesystem fallback.

Package candidate: `Katasec.Forge.Mcl.Core 0.1.8` (current 0.1.7; GitHub package inventory checked
before planning). Recheck availability before implementation/publication; never overwrite a
published version. Existing publish workflow and `eng/verify-core-package.sh` must agree with
the project version. Update all in-repo callers affected by public shape changes; downstream
repositories retain their pinned old package until their own reviewed upgrade tasks. No sibling
repository references. The implementer plan inventories those future consumers.

Actual retained binary contract: CLI loads published Client0.9.3 alongside current Core. Metadata
inspection confirms that DLL calls the six-argument `DurableMissionPackageInput` constructor.
Adding optional Assets alone removes that CLR member. Preserve an explicit six-argument overload
delegating to the seven-argument constructor with null Assets; this serves the current loaded
binary, not a speculative compatibility path. Add a focused regression exercising that published
Client's Project/chat launch through existing application composition, without recompiling Client,
and installed-default `forge project create` followed by a real piped Chat turn after publication.
The actual selected Client/Contracts/Conversations.Contracts/Hands DLL inventory found no changed
ExpertLoader.Validate or ValidatedDurableMissionPackage member reference requiring another shim.
Select the seven-argument semantic constructor with `[method: JsonConstructor]` so retaining the
six-argument CLR member does not make source-generated deserialization ambiguous. Verify JSON
round trips for no-assets/null (old hash unchanged) and real asset bytes/executable metadata.
Supervisor .NET10 source-generated probe observed unannotated record deserialization throwing
NotSupportedException and annotated primary constructor succeeding; evidence is in
`/private/tmp/phase76-json-ctor-probe`. No runtime dependency or compatibility reader is added.

## Failure, security and engineering gates

| Boundary | Required containment / observation |
|---|---|
| Pure package admission | Invalid hashes, oversized actual JSON, reserved/colliding paths/names, undeclared names and environment expressions return explicit failure before execution; valid token_count and changed arbitrary exec assets remain accepted |
| Replay | Changed executable args/model/inputs/endpoint/semantic definition or admitted-name set refuses continuation; a fresh scratch path alone does not invalidate it; completed side effects are not repeated |
| Process | Concurrent stdin/stdout/stderr prevents pipe deadlock; caller cancellation/timeout/I/O failure kills and joins ordinary process tree; oversize output fails without silent truncation |
| Native library | Exact restored ONNX package performs numeric inference and cancellation; missing runtime/model is explicit; macOS probe cannot claim Linux/image support |
| Publication | Immutable version, source commit, nuspec dependencies and restored public APIs independently match; failure leaves consumer upgrades unapproved |

Security: no public entry point, datastore, identity role or credential boundary changes in Core.
Host remains persistent owner and Runner owns scratch. Submitted code's process authority follows
the approved policy; this task makes no sandbox/isolation claim. Engineering: fixed conventions,
one parser/interpreter/adapter path, pure validation separated from process effects and explicit
failure observations. Desktop/ForgeUI/TUI visual gates are N/A: no layout change.

## Verification and default path

### Code-review correction boundaries

The existing lifecycle contract requires ownership through pipe completion even if the direct
parent exits first. Its private launch implementation is approved in the complete r6 plan; it has no new public
API, permission policy, datastore or identity authority. Product edits are authorized only through the same implementer's explicit r6 handoff; no further scope expansion is approved.

| Boundary | Required correction / proof |
|---|---|
| Exec ownership | Establish OS ownership before target execution; retain it through stream completion and termination. No post-launch race or descendant snapshot substitutes. The approved plan uses POSIX unreaped-leader and atomic Windows job ownership. |
| Exec failure | Failed ownership setup never falls back to an unowned launch. Timeout/caller cancellation and I/O failure keep their declared results; termination failure remains visible. Parent-first-exit tests prove no surviving ordinary descendant or later sentinel write. |
| Cleanup failure precedence | An observed OS termination/reap failure propagates as a visible IOException even when caller cancellation is requested; do not replace it with OCE. Normal caller cancellation propagates OCE after successful cleanup. Preserve the cleanup operation/error and original failure context through a private distinction at the exec boundary; no new public exception contract or fallback. |
| Supported PID1 runtime | The Runner image directly starts dotnet as its entrypoint. If it adopts ordinary descendants, reap only owned-group children after termination; never reap unrelated processes or set a global subreaper policy. Distinguish stopped execution from zombie process entries. Native observation and I/O must remain joinable when cleanup itself fails. |
| Declined stdin | A child may close unread stdin. Recognize only the platform's concrete broken-pipe/closed-input condition; still join the process and remaining streams, honor cancellation, validate exit code and parse declared JSON output. Other I/O errors remain failures. The correction plan must name the exact error mapping and tests; blanket IOException swallowing or message matching is not approved. |
| Checkpoint input | Existing codec rejects missing/null required current-format shape before resume dereferences it; InvalidContinuation occurs without provider/executable invocation. |
| Existing parser / adapters | Refactor coherent parser stages to satisfy complexity/nesting limits, preserve one parser, place public streaming entry points before helpers, diagnose the canonical fast-child failure from observed envelope/error evidence. |
| Platform evidence | Any new platform launch primitive requires behavioral checks on every supported host as well as current-source zero-warning AOT. Existing release help/version probes alone do not exercise launch semantics. |

Supervisor Type-2 decision (2026-10-09): Linux PID1 adopted-child reaping stays
private to the existing exec lifetime owner. After checked termination of the atomically owned
process group and observation of root exit, retain the root unreaped; enumerate this process's
task children, exclude the root, verify each candidate belongs to that held group, and reap exact
owned children. Repeat through intermediate-parent exit/adoption until no owned descendants
remain, then reap the root last. This post-termination scan neither establishes ownership nor
chooses kill targets. No global subreaper, waitpid(-1), unrelated-child reaping or silent procfs
failure fallback. Native/procfs cleanup failures retain visible IOException precedence. Reversal
and removal: replace this private drain with an authoritative supported OS primitive if one
provides equivalent root-excluding adopted-child reaping, preserving the same public contract and
tests. The normal Linux x64 CI gate must prove actual PID1 behavior in a container without
--init, including an unrelated .NET child remaining waitable by its original owner; all four
supported native hosts still require their ordinary lifecycle checks. Docker's absent local
daemon is an observed environment limitation, not PID1 acceptance or a gate waiver.

### Existing required gates

Focused tests cover the changed package, manifest, replay, trace and adapter boundaries; full
normal repository managed tests/builds pass with zero warnings. Controlled provider fixtures prove
only their named layer. Run final current-source Native AOT through the canonical normal build
route after managed checks stabilize, with zero warnings; no warning suppression or alternate
release configuration supplies acceptance.
The existing PR verify job in `.github/workflows/publish-terminal-extensions-package.yml` runs
`make cli-script-test cli-verify verify-terminal-extensions-package` on macOS 14 and supplies the
premerge canonical zero-warning Native AOT route. The existing `release.yml` also runs its
four-host native build matrix on pull requests, then publishes after merge; those native checks
are additional premerge evidence and published assets are separate postmerge evidence. Existing local-install linker diagnostics cannot
substitute for the canonical warning-free gate; no build-script workaround is part of this task.

This producer's default artifact is the normally published private Core NuGet package from merged
main, independently restored into a fresh consumer with no sibling source reference or manually
swapped DLL. The supervisor exercises package creation/validation, arbitrary packaged exec input
binding, exact StepKey trace and numeric ONNX through those public APIs. Normal Linux publication
checks execute the native numeric model, not merely inspect package assets. Record exact package
version/hash/source commit and observed outputs. Existing installed `forge run` default behavior
must also pass a disposable normal-provider Hands Write/Read regression from clean merged-main
`make install`, with its normal local config, keys and native sidecars and absent test overrides.

These observations close this producer task only. Real Runner-image/cloud execution, Host content
persistence and the new installed unified command remain required downstream acceptance and cannot
be marked complete by this package probe.

## Done when

1. The exact reviewed Core API/semantic changes above are implemented in their existing owners;
   focused, full and final Native AOT checks pass with zero warnings.
2. Full current simplicity/code-style and ownership reviews pass, and supervisor checks the diff
   and failure evidence independently before merge.
3. Product PR is merged, Core is normally published with verified immutable provenance, and the
   supervisor's fresh published-package/default regression observations pass.
4. Completion evidence and explicit stage times are recorded; touched repositories are clean on
   current main. Full Phase 76 remains open for the dependent cloud/client tasks.

## Current work

| Item | State |
|---|---|
| Design | Parent round 7 PASS; supervisor locked 2026-10-09 21:40:31 UTC |
| Implementer plan | [Complete r6](phase-76.4-core-cloud-primitives-plan.md) approved 23:13:40 UTC; same implementer receives bounded correction handoff |
| Independent plan reviews | Fresh complete r6 simplicity/ownership PASS; [full current verdicts/timing](phase-76.4-core-cloud-primitives_completed.md) |
| Plan approval / implementation | Initial round1 handback770778d; correction implementation assigned23:15:09 UTC under complete r6 approval, see [review evidence](phase-76.4-core-cloud-primitives_completed.md#code-review--simplicitystyle-round-1) |
| Published/default acceptance | Required; not performed |
