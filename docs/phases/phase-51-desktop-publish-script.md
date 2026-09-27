# Phase 51 — Desktop publish-script extraction

> **Active.** Extract only the existing `desktop-publish` Make recipe into a PowerShell script.
> This refactor preserves the Makefile interface, the canonical workflow, and the Desktop bundle
> contract exactly; it does not replace Make.

## Locked design

| Area | Decision |
|---|---|
| Owner and scope | New `forge-desktop/scripts/Publish-Desktop.ps1` owns only the current `desktop-publish` sequence. `Makefile` retains every target and delegates this one target to the script. |
| Callers | `.github/workflows/desktop-build.yml` stays byte-for-byte unchanged. It continues to invoke `make OS=Windows_NT desktop-publish` on Windows and `make desktop-publish` on macOS. |
| Platform mapping | Preserve the Makefile's current target decisions: Windows uses `win-arm64` plus `net10.0-windows10.0.19041.0`; macOS selects `osx-arm64`/`maccatalyst-arm64` or `osx-x64`/`maccatalyst-x64` from `uname -m`; all other hosts fail before output deletion. |
| Publish order and output | Restore the MAUI workload, delete only `dist/forge-desktop`, publish Application Host, then Supervisor, then MAUI Host last into that directory. The last-publish ordering remains mandatory because shared-output publish may prune files it does not own. |
| macOS output | Require the generated `Forge.app`, then copy it to `dist/forge-desktop/Forge.app` with `ditto`, matching the original recipe. |
| Windows identity | Run the existing timestamp normalizer on the shipped Supervisor and on an isolated repeat publish. Compare the normalized files byte-for-byte; fail on inequality. Delete only the uniquely created temporary directory. |
| Non-goals | Do not remove or change Make targets, install/remove tools, modify the workflow, alter artifacts/checksums/permissions, change project/runtime source, or introduce supported target options. |

## Release build target

| Area | Decision |
|---|---|
| Make target | Add `build-desktop` as an alias for the existing AOT `desktop-publish` target. Keep `build`, `desktop`, and `desktop-publish` unchanged. |
| Release distinction | Production remains `-c Release` with the projects' existing `PublishAot=true`, publishes the self-contained `dist/forge-desktop` bundle, and retains the Windows identity guard. |
| Non-goals | Do not change the workflow, release artifact names/contents, project-level AOT defaults, runtime behavior, product default-path configuration, or introduce a non-AOT publish script. |

## Gate record

| Gate | Result |
|---|---|
| Architecture / security | **N/A, PASS.** No component boundary, public endpoint, tier, data store, identity, secret, package permission, or credential flow changes. This is a local build-entry-point refactor. |
| Engineering philosophy | **PASS.** One extracted recipe has one owner. Existing `Normalize-AotPeTimestamps.ps1` remains the PE mutation/validation seam; no retry, fallback, configuration knob, or generalized build framework is added. |
| Failure boundary | **PASS.** Unsupported host, failed restore/publish, missing Mac app bundle, normalizer failure, or unequal Windows images terminates the requested Make target. The script cleans only its own temporary repeat-publish directory. |
| Native AOT | **PASS.** Preserve the self-contained Supervisor publish and the Windows repeat-publish normalized byte-identity guard. |
| Desktop quality / parity / visual reference | **N/A.** No product action, native-host behavior, lifecycle policy, or visual surface changes. |
| Default-path acceptance | **Unchanged, but artifact delivery is verified.** The normal user path remains a canonical-workflow bundle, downloaded unchanged and launched with zero arguments and absent `FORGE_*` overrides. The refactor must not claim local/script evidence as product-path acceptance; a successful canonical workflow and a checksum-verified unchanged artifact are required before completion, with the established platform launch observation retained as the product acceptance authority. |

## Task 1 — Extract `desktop-publish`

**Scope:** create `scripts/Publish-Desktop.ps1` and replace only the body of the `desktop-publish`
recipe in `forge-desktop/Makefile` with a call to it. Preserve its target name and description.

**Done when:**

1. `make desktop-publish` produces the same expected platform bundle layout on macOS and Windows.
2. The script retains the original target selection, publish order, Mac app copy, and Windows normalized repeat-publish byte identity check.
3. `make build`, `make test`, `make clean`, `make desktop`, and the entire workflow file remain unchanged.
4. `make desktop-publish`, `dotnet build src/ForgeMission.slnx`, and `dotnet test src/ForgeMission.Tests` pass on the local supported host.
5. The canonical workflow succeeds unchanged for both bundles; each unchanged archive verifies against its SHA-256 sidecar. Product default-path launch acceptance remains the established downloaded-artifact procedure, not a substituted local build.

**Current evidence (2026-09-27):** `scripts/Publish-Desktop.ps1` and the one-line Make target
delegation are implemented on `forge-desktop` branch `codex/extract-desktop-publish-script`.
`make desktop-publish` passed on macOS ARM64 and produced `dist/forge-desktop/Forge.app`;
`dotnet build src/ForgeMission.slnx` passed with 0 warnings and 0 errors; and the workflow file has
no diff. `dotnet test src/ForgeMission.Tests` currently reports 25 application/transport HTTP-500
failures (328 passed, 1 skipped); this task does not touch those paths. Canonical Windows/macOS
workflow and downloaded-artifact checksum evidence remain pending.

## Task 2 — Add the release build target

**Scope:** add the `build-desktop` target. Remove the unaccepted non-AOT development publish
wrapper/mode so `Publish-Desktop.ps1` remains the parameterless production publisher. Retain the
release target's default invocation and behavior.

**Done when:**

1. `make build-desktop` produces the existing AOT release bundle in `dist/forge-desktop` and retains the Windows identity guard.
2. Existing `build`, `desktop`, `desktop-publish`, workflow, artifact names, and release MSBuild project defaults are unchanged.
3. The focused release-target checks and the existing solution/full-suite checks are recorded; the known suite failures remain explicit until independently resolved.

**Current evidence (2026-09-27):** `make build-desktop` completed on macOS ARM64, produced
`dist/forge-desktop/Forge.app`, and preserved the production publisher's cleanup block. The
workflow has no diff. `dotnet test src/ForgeMission.Tests` still reports 25 application/transport
HTTP-500 failures (328 passed, 1 skipped); canonical Windows/macOS workflow and downloaded-artifact
checksum evidence remain pending.

## Task 3 — Remove the unused Photino adapter

**Scope:** delete `src/ForgeMission.Desktop.Photino`; remove its solution entry, its obsolete
architecture-test assumptions, and its current component-atlas row. Update only active/current
architecture documentation that still describes it as the selected adapter. Preserve historical
phase and retrospective records as evidence of prior decisions.

**Locked facts:**

| Area | Decision |
|---|---|
| Runtime fit | The production Desktop path composes `ForgeMission.Desktop.Host` (MAUI), not Photino. Repository search finds no project reference to the Photino adapter; `desktop-publish` never publishes it. |
| Dependency removal | Deleting the adapter project removes the sole `Photino.NET` package reference from the live solution. No replacement package, runtime, or abstraction is added. |
| Boundary test | Retain the Supervisor-versus-MAUI-Host and runtime-dependency structural assertions. Remove only assertions/data rows that name the deleted adapter or its package. |
| Current documentation | Remove the adapter from the source component atlas and revise current architecture text to name MAUI Host as the native implementation. Historical documents retain their Photino references. |
| Security / architecture | **N/A, PASS.** This removes an unused local package/project; it changes no public entry point, identity, credential, store, tier, or cross-context contract. It is a Type-2 deletion, recoverable from source history. |
| Failure boundary | The solution build and package/reference sweep prove that no live project resolves the removed path or package. A missing residual reference fails build or the structural test; no fallback is introduced. |
| Default path | The published Desktop artifact, zero-argument Supervisor launch, runtime topology, and canonical workflow remain unchanged. Local build evidence does not replace the established downloaded-artifact acceptance. |
| UI / parity | **N/A.** The MAUI native Host and Presentation are unchanged; no product action or visual surface is modified. |

**Done when:**

1. The Photino project and its sole package reference are deleted, and it no longer appears in the solution or active component inventory.
2. The narrowed structural test continues to protect the Supervisor/MAUI Host boundary without reading a deleted project.
3. A live-source sweep finds no Photino package/project/runtime reference outside intentional historical records.
4. `make build-desktop` and `dotnet build src/ForgeMission.slnx` pass; the full suite result is recorded without attributing the existing HTTP-500 failures to this deletion.
5. The unchanged canonical workflow and release-artifact acceptance remain required before Phase 51 completion.

**Current evidence (2026-09-27):** the three tracked Photino project files, solution entry, source
component-atlas row, and obsolete boundary-test data are removed. A live-source sweep found no
Photino package/project/runtime reference; the retained boundary test passed (5/5).
`dotnet build src/ForgeMission.slnx` and `make build-desktop` passed on macOS ARM64; the latter
produced `dist/forge-desktop/Forge.app` with no Photino-named item. The full suite reported 25
application/transport HTTP-500 failures (327 passed, 1 skipped), including the unchanged
Project-transport baseline; this deletion does not touch those paths. The canonical Windows/macOS
workflow and downloaded-artifact default-path/checksum acceptance remain pending.

## Task 4 — Remove obsolete local probe projects

**Scope:** delete the unused `ForgeMission.Application.TransportProbe` and
`ForgeMission.ProjectServiceProbe` diagnostic executables, their test-project build references,
and the tests that require those child processes. Remove their current atlas rows, together with
the seven mission-language rows whose projects now belong to `forge-mcl`.

| Area | Decision |
|---|---|
| Production path | No production Application, Application Host, Transport, Desktop, publish, workflow, or runtime behavior changes. `ApplicationHostProcess` remains because the Project transport contract tests still use it. |
| Intentional test removal | Remove the out-of-process transport probe test and the ProjectService separate-process/crash-boundary tests; retain the in-process concurrent-write and injected publication-fault tests. This intentionally removes external-process/crash-safety coverage without a replacement. Source history is the reversal path if that coverage is needed again. |
| Dependency removal | Remove the TransportProbe solution/test references, both probe test-project references, and the ProjectServiceProbe friend-assembly attributes. ProjectServiceProbe has no solution entry. |
| Current documentation | Remove only the active component-atlas and domain-inventory claims that describe these probes or mission-language projects as local components. Historical Phase 43 and retrospective records remain evidence of the prior topology. |
| Security / architecture | **N/A, PASS.** This is a Type-2 local deletion: no public entry point, tier, identity, credential, data store, cross-context contract, or production failure behavior changes. |
| Engineering / failure boundary | **PASS.** The deleted diagnostics and child-process tests own no product path. Residual references fail the build; a live-source sweep proves their removal. No fallback or new abstraction is introduced. |
| Default path / UI | **Unchanged, N/A.** The published artifact, zero-argument Supervisor launch, MAUI Host, and visual surface are unchanged; local checks do not replace canonical artifact acceptance. |

**Done when:**

1. Both probe directories, the TransportProbe solution entry, test-project references, and ProjectServiceProbe friend-assembly attributes are gone.
2. Only tests dependent on the deleted executables are removed; retained tests require no deleted child assembly.
3. The source atlas no longer lists the two probes or the seven `forge-mcl` projects, and the active domain inventory no longer presents the probes as current components.
4. A live-source sweep finds no residual probe project/reference path outside intentional historical records; solution and release publish builds pass, and the full-suite result is recorded without assigning unrelated existing failures to this cleanup.

**Current evidence (2026-09-27):** both probe directories (including ignored build outputs), the
TransportProbe solution entry, both test-project references, and the two ProjectServiceProbe
friend-assembly entries are removed. The two TransportProbe child-process tests and the four
ProjectService process/crash test cases are intentionally removed; `ApplicationHostProcess`, the
in-process concurrent-write test, and the injected publication-fault test remain. The source atlas
no longer lists the probes or the seven `forge-mcl` projects; the active ownership inventory no
longer claims the probes are current components. A live-source sweep found no probe references.
`dotnet test src/ForgeMission.Tests --filter FullyQualifiedName~ProjectServiceTests` passed 58/58;
`dotnet build src/ForgeMission.slnx` passed with 0 warnings and 0 errors; and `make build-desktop`
published `dist/forge-desktop/ForgeMission.Desktop` on macOS ARM64. The full suite currently
reported 23 failures, 323 passed, and 1 skipped. Task 5 identified the omitted Application Host
registration as the cause; it was not caused by this deletion. Canonical Windows/macOS workflow and
downloaded-artifact default-path/checksum acceptance remain pending.

## Task 5 — Restore the Application Host Project-service registration

**Scope:** restore the omitted `IProjectService` singleton registration in
`ForgeMission.Application.Host/Program.cs`, mapping it to `ApplicationComposition.Projects` in the
same way as the remaining typed Application facades. This corrects the real Application Host used
by the project transport contract tests.

| Area | Decision |
|---|---|
| Composition | Register `IProjectService` immediately after `ApplicationComposition` and before its dependent facade registrations. The registration is identical to `LocalApplicationHost`'s existing mapping. |
| Non-goals | Do not add endpoints, DTOs, runtime configuration, a fallback, workflow changes, or any replacement for the intentionally deleted probe tests. |
| Security / architecture | **N/A, PASS.** This restores existing in-process dependency composition. It changes no public entry point, tier, identity, credential, datastore, or cross-context contract; it is a Type-2 repair. |
| Engineering / failure boundary | **PASS.** Endpoint signatures already require the typed service. Registration makes DI resolve that declared dependency rather than allowing minimal API binding to fail before the route executes. |
| Default path / UI | **Unchanged, N/A.** The zero-argument Desktop launch, Application Host address protocol, published artifact, and visual surface are unchanged. The real-Host transport contract test is test evidence, not substituted release-artifact acceptance. |

**Done when:**

1. The production `Program` maps `IProjectService` to `ApplicationComposition.Projects` exactly once.
2. `ProjectTransportContractTests` pass through `ApplicationHostProcess`, which starts the real Host process and exercises its HTTP routes.
3. `dotnet build src/ForgeMission.slnx` and the full test suite pass; canonical Windows/macOS workflow and downloaded-artifact acceptance remain required before Phase 51 completion.

**Current evidence (2026-09-27):** the restored singleton mapping is the only production-source
change. `dotnet test src/ForgeMission.Tests --filter
FullyQualifiedName~ProjectTransportContractTests` passed 25/25 through the spawned real Application
Host; `dotnet build src/ForgeMission.slnx` passed with 0 warnings and 0 errors; and the full suite
passed 346/346 with 1 intentional skip. The Kind port-forward at `127.0.0.1:18080` remained running
but was not required for those tests. Canonical Windows/macOS workflow and downloaded-artifact
default-path/checksum acceptance remain pending.

## Task 6 — Preserve Mac Catalyst inherited pipe arguments

**Scope:** correct the Mac Catalyst entry point so the Desktop Host receives the complete fixed
Supervisor pipe-argument pair passed to its `Main(string[] args)` entry point.

| Area | Decision |
|---|---|
| Owner and contract | `ForgeMission.Desktop.Host` owns this bootstrap handoff. C# `Main` arguments already exclude the executable path, so it passes `args` unchanged to `HostStartupArguments`. |
| Boundary preservation | Do not change `MauiProgram`'s `Environment.GetCommandLineArgs()[1..]` fallback, the Supervisor, pipe protocol, workflow, runtime ownership, or UI. The environment API includes the executable path; the `Main` parameter does not. |
| Regression guard | A focused source-contract test asserts the Mac entry point preserves `args` and cannot reintroduce the slice. It is intentionally source-level because the normal test target does not compile/reference the Mac Catalyst executable. |
| Security / architecture | **N/A, PASS.** This Type-2 local process-bootstrap repair changes no public entry point, tier, datastore, identity, credential, or cross-context contract. Source history is the reversal path. |
| Engineering / failure boundary | **PASS.** The existing fixed Supervisor-to-Host pipe contract remains the only seam. The Host continues to show its existing failure content for invalid handles; no fallback, retry policy, option, or helper abstraction is introduced. |
| Desktop quality / parity / visual reference | **PASS, N/A for visual design.** Required behavior is that the Host launched by the Supervisor accepts its inherited pipe handles. The Host remains disposable and the Supervisor remains framework-free; no product action, native lifecycle policy, or visual surface changes. |
| Default path | The normal action is the zero-argument Supervisor executable from the published bundle, with relevant `FORGE_*` overrides absent. A local bundle launch is focused packaged-app evidence only; canonical downloaded-artifact acceptance remains required for Phase 51 completion. |

**Done when:**

1. The Mac Catalyst `Main` passes its `args` unchanged to `HostStartupArguments`; only that
   argument handoff changes.
2. The focused regression guard passes and rejects the prior `args[1..]` form.
3. The focused test and `git diff --check` pass; a `make build-desktop` bundle launch through the
   zero-argument Supervisor moves past the inherited-pipe-handles failure.

**Current evidence (2026-09-27):** the Mac Catalyst entry point preserves the complete `Main`
argument array; the focused source-contract guard passed. `dotnet build src/ForgeMission.slnx`
passed with 0 warnings and 0 errors, and `make build-desktop` produced the local macOS bundle.
The operator launched that zero-argument Supervisor and confirmed that it reached the application,
instead of the inherited-pipe-handles failure. This is local packaged-app evidence only; canonical
downloaded-artifact checksum acceptance remains pending.

## Task 7 — Read Application Host readiness directly

**Scope:** replace the Supervisor's event-based observation of its Application Host child's existing
stdout readiness marker with direct line reads from that same redirected stdout pipe.

| Area | Decision |
|---|---|
| Owner and contract | `ForgeMission.Desktop` owns Application Host supervision. It continues to wait for the exact existing `FORGE_CLIENT_RUNTIME_URL=` marker and passes the reported loopback URL to the native Host unchanged. |
| Read and failure boundary | Read stdout line-by-line with one linked 20-second timeout and caller cancellation token; capture stderr concurrently. A closed stdout pipe without the marker and a timeout remain startup failures; caller cancellation remains cancellation. |
| Regression guard | A redirected lightweight child emits the exact marker while deliberately keeping stderr open. It proves the readiness wait returns before the child exits, without starting a UI, network listener, or application process. It is controlled component evidence, not default-path acceptance. |
| Non-goals | Do not change the Supervisor-to-native-Host pipe protocol, Host/UI code, child process ownership, runtime configuration, workflow, or published artifact contract. |
| Security / architecture | **N/A, PASS.** This Type-2 local supervision repair changes no public entry point, tier, datastore, identity, credential, or cross-context contract. Source history is the reversal path. |
| Engineering philosophy | **PASS.** The existing readiness marker remains the one contract. Direct reading removes an unreliable observer without adding a fallback, retry policy, option, or abstraction framework. |
| Default path / UI | **Unchanged.** The normal path remains the zero-argument published Supervisor and its owned Application Host. The in-memory and lightweight-child tests are explicitly non-acceptance; a manual zero-argument bundle launch must reach navigation for local packaged-app evidence. |

**Done when:**

1. The Supervisor reads the existing marker directly from its redirected child stdout, with one
   20-second timeout, caller cancellation, and concurrent stderr capture retained.
2. The focused in-memory marker and open-stderr child tests and `git diff --check` pass.
3. `make build-desktop` succeeds, and a manual zero-argument bundle launch moves from Starting
   Forge to application navigation. This is local packaged-app evidence; canonical downloaded-
   artifact acceptance remains required before Phase 51 completion.

**Current evidence (2026-09-27):** the focused reader tests passed, including a redirected child
that writes the ready marker while keeping stderr open; this proves ready completion does not wait
for the child's lifetime. `dotnet build src/ForgeMission.slnx` passed with 0 warnings and 0
errors, and `make build-desktop` produced the local macOS bundle. The operator confirmed that the
zero-argument Supervisor reached the application. This is local packaged-app evidence only;
canonical downloaded-artifact checksum acceptance remains pending.

## Task 8 — Completion record

After verification, move execution and workflow evidence to
`phase-51-desktop-publish-script_completed.md`; retain a short status pointer here.
