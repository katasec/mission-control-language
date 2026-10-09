# Phase 76.4 — Core package and execution primitives

**Status:** Locked parent design; implementer Plan required. No code approval.
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
supervisor lock. This bounded task directly implements its Core rows, so a second design is N/A;
its implementer plan and both plan reviews remain mandatory.

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

Package candidate: `Katasec.Forge.Mcl.Core 0.1.8` (current 0.1.7; GitHub package inventory checked
before planning). Recheck availability before implementation/publication; never overwrite a
published version. Existing publish workflow and `eng/verify-core-package.sh` must agree with
the project version. Update all in-repo callers affected by public shape changes; downstream
repositories retain their pinned old package until their own reviewed upgrade tasks. No sibling
repository references. The implementer plan inventories those future consumers.

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

Focused tests cover the changed package, manifest, replay, trace and adapter boundaries; full
normal repository managed tests/builds pass with zero warnings. Controlled provider fixtures prove
only their named layer. Run final current-source Native AOT through the canonical normal build
route after managed checks stabilize, with zero warnings; no warning suppression or alternate
release configuration supplies acceptance.

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
| Implementer plan | Next; read-only, no product edits authorized |
| Plan approval / implementation | Not granted / not started |
| Published/default acceptance | Required; not performed |
