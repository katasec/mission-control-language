# Phase 76.4 — Complete implementer correction plan, round 11

**PLAN APPROVED2026-10-10 03:02:53 UTC.** Same implementer observed
**2026-10-10 02:54:07–02:56:02 UTC**, read-only. Supervisor transcribes the complete return into
this artifact: the current43-path inventory, retained API/sequence/gates below plus this correction
are one complete plan. Fresh full r11 simplicity and ownership reviews PASS; supervisor independently checked exact artifact paths/routing, unchanged production tree, failure/API/security/default gates. This approval authorizes only the two current retention edits, not a production fix or merge/publication.
Source `0d0a338f672469356b3c412e7a4ccf2dc11d74e7`, base `8d28dc1`, same branch/PR77.
Only paths42–43 may change after fresh full sequential reviews and explicit approval.
The implemented r10 plan is [historical evidence](phase-76.4-core-cloud-primitives_completed.md#superseded-diagnostic-correction-plan-r10).

## Current correction r11 — failed matrix retention

### 1. Files

Complete inventory is below, expanded42→43 only by `.github/workflows/release.yml`.
Only that workflow and `scripts/README.md` change now. Every other path remains frozen.
No production/API/fixture/dependency/image/process/payload/deadline/warning/consumer/deploy change.
Existing build-verification design/owners and locked Core design apply; no new architecture choice.
UI/browser N/A. Runtime, security/engineering and default-path gates remain applicable.

### 2. Reuse

Reuse existing matrix `actions/upload-artifact@v7`, existing collector's validated output and native
probe publish. No new action, helper, dependency, collector or release path. The existing publisher
selects `forge-*`; diagnostic artifact naming must remain outside that pattern. Existing Core APIs,
ABI, generated JSON, pure admission, workspace, native lifetime, ONNX and package owners below
remain exact. Installed Ruby Psych3.1.0 parses YAML; PyYAML is absent and is not installed.
Availability check's existing world-writable-PATH warning is preserved, not suppressed.

### 3. Sequence

Reconfirm source/branch/base/PR/inventory and unique evidence path after approval. Immediately
following the existing native matrix build, before the unchanged successful ZIP upload, add:

```yaml
- name: Preserve failed macOS native probe evidence
  if: ${{ failure() && runner.os == 'macOS' }}
  uses: actions/upload-artifact@v7
  with:
    name: native-exec-diagnostics-${{ matrix.rid }}-${{ github.run_id }}-${{ github.run_attempt }}
    path: |
      dist/cli/exec-probe-crashreports
      dist/cli/exec-probe-run.log
      dist/cli/exec-probe-publish.log
      dist/cli/exec-probe/ForgeMission.Exec.Probe
      dist/cli/exec-probe/ForgeMission.Exec.Probe.dSYM
      dist/cli/exec-probe/libonnxruntime.dylib
    if-no-files-found: error
```

The dSYM bundle includes only this probe's existing Info.plist/DWARF/relocations. Report directory
contains only existing validated reports/status. Do not upload general DiagnosticReports,
credentials, environment, broad workspace or shipped CLI payload. Preserve successful
`forge-${{ matrix.rid }}` ZIP/path, publisher `forge-*`, permissions/dependencies/concurrency/
commands. No continue-on-error, retry or failure replacement; upload failure keeps the job failed.

README records this route and the actual earlier loss: two matrix reports were copied but not
uploaded. Canonical success does not diagnose that same-tree matrix failure. Reassess retention
once diagnosed; no general observability. Keep collector unchanged: failed macOS only, exact
header/body identity and procLaunch>=invocation start, nonrecursive user/system directories,
250ms/10s bounded selection/copy-once/source separation/status/errors, terminating status writes,
original probe error preserved. No mtime substitution, payload/deadline change or failed-test retry.

Retain every producer step below: actual published Client six-argument ABI and generated constructor,
shared parser/pure diagnostic source, internal named admission, assets/hash/actual JSON cap,
format3 semantic replay/consumed-shape refusal, exact StepKey/live workspace, prelaunch PID1 refusal,
atomic retained POSIX groups/Windows jobs, checked termination→pipejoins→exact reap, error precedence,
joined numeric ONNX cancellation/parallel sibling, unchanged decisive native pressure/pipe probes,
exact Runner image/init proof and bare-PID1 negative. No consumer changes or guessed signal fix.

After meaningful checks, freeze/commit/push the same PR77; hand back full43-path binary diff/hash/
inventory and unchanged production tree. Fresh full sequential code reviews and normal current
canonical/four-host CI follow. Retrieve actual failing matrix report/thread/exception/PC/image UUID
and exact executable/dSYM before proposing a production fix. No normal merge/publication until
required gates pass; no completion until published-package/installed defaults pass.

### 4. Verification

| Gate | Required observation |
|---|---|
| YAML | Installed `ruby -rpsych -e 'Psych.parse_file(ARGV.fetch(0)); puts "YAML parse PASS"' .github/workflows/release.yml` |
| Exact step | Parsed condition/actionv7/name/six paths match above |
| Negative routing | Successful macOS/failing non-macOS do not qualify; name cannot match forge-* |
| Release preservation | Existing ZIP/publisher pattern/dependencies/permissions unchanged |
| Scope | Only README/workflow delta;43-path inventory; src tree byte-identical |
| Existing scripts | make cli-script-test; parser and text diff hygiene |
| Reviews | Fresh complete sequential simplicity/style and ownership/technical reviews |
| Native | Normal current canonical/all four hosts; unchanged warning/payload/deadline gates |
| Diagnostic | Normally retained failed matrix artifact; actual child report and matching binary/symbol UUID |

All retained focused/full/Release/package/ABI/branch-consumer/normal publication/default gates
below remain required. Current unchanged production evidence is source-specific; no unchanged
local full managed/AOT/consumer rebuild solely for an upload declaration. A passing sibling or
missing reports never closes the unresolved SIGSEGV. Recheck immutable0.1.8/tag before publication.

### 5. Principles that changed a decision

| Implementer rule | Choice |
|---|---|
| 1 No NIH | Existing action/collector/destinations/installed Psych |
| 2 No duplicate paths | One scoped transport; no second collector/publisher |
| 3 Minimum | Two edited files, one inventory addition |
| 4 No speculative abstractions | Direct YAML step, no helper/dependency/hook |
| 5 Scope | Evidence before cause/fix |
| 6 Verified | Actual failed-job report/binary correlation and defaults |
| 7 Outline | Explicit step purpose |
| 8 Small functions | No new function |
| 9 Top-down | Retention follows native execution |
| 10 Explicit errors | Failed build/upload remain failed |
| 11 Shallow nesting | One condition |
| 12 Side effects | Artifact action transports; script selects |
| 13 Warnings | Existing raw-warning gates unchanged |
| 14 Extraction | No helper for YAML declaration |
| 15 Complexity | No added parser/wrapper complexity |

### 6. Open questions and assumptions

No unresolved architecture or implementation choice for retention. Cause remains unknown:
current matrix38016928684 copied two reports but did not upload; canonical38016928681 passed.
Actual faulting child thread/PC/UUID remain missing. No production correction, waiver, merge,
publication or completion approved. Ordinary descendants remain the lifetime guarantee; deliberate
escape/isolation/permissions/egress remain outside scope. Exact Runner source/digests below remain.

## Complete retained producer contract

The following APIs, inventory, sequence and gates remain part of current r11. Earlier approval
history is [recorded separately](phase-76.4-core-cloud-primitives_completed.md#retained-producer-approval-history-before-r11);
it does not authorize the new correction. Already implemented producer behavior stays frozen.
Windows expected-path normalization remains implemented; no signal-mask or production fix is
justified by an unexplained native failure. Current r11's no-unchanged-rebuild rule governs this
workflow-only correction, while the retained source-specific production evidence remains required.

## 1. Files

Continue current clean0d0a338f on `adeen/phase-76-core-primitives`, base8d28dc1, same draftPR77.
All43 paths below are relative to `/Users/ameerdeen/progs/forge-mcl`. This is the complete current
inventory. Only42–43 change after current plan approval.

| # | File | Purpose |
|---|---|---|
| 1 | `src/ForgeMission.Core/Manifest/ForgeManifest.cs` | `PackageConfig` and default-empty `Package` |
| 2 | `src/ForgeMission.Core/Manifest/ForgeTomlReader.cs` | One parser, distribution selection and cohesive parsing stages |
| 3 | `src/ForgeMission.Core/Experts/ExpertLoader.cs` | Supplied diagnostic sources and declared mission parameter keys |
| 4 | `src/ForgeMission.Core/Runtime/DurableMissionPackageValidator.cs` | Pure construction, assets, hashes, common validation and actual serialized-size cap |
| 5 | `src/ForgeMission.Core/Runtime/DurableMissionInputPolicy.cs` | Shared reserved-name and reachable-input policy |
| 6 | `src/ForgeMission.Core/Runtime/PipelineDefinitionFingerprint.cs` | Complete deterministic execution identity |
| 7 | `src/ForgeMission.Core/Runtime/PipelineExecutionWorkspace.cs` | Live caller-owned registry reference and pure output-path convention |
| 8 | `src/ForgeMission.Core/Runtime/PipelineRunOptions.cs` | Inherited runtime-only workspace |
| 9 | `src/ForgeMission.Core/Runtime/PipelineRunner.cs` | Admitted inputs, replay, workspace inheritance and exact StepKey threading |
| 10 | `src/ForgeMission.Core/Runtime/PipelineToolPause.cs` | Inner checkpoint format3 and consumed-shape validation |
| 11 | `src/ForgeMission.Core/Runtime/PipelineTraceEvent.cs` | Nonpositional init `StepKey` |
| 12 | `src/ForgeMission.Core/Adapters/ExecExpertRunner.cs` | Generic mappings, concurrent bounded exchange and cleanup precedence |
| 13 | `src/ForgeMission.Core/Adapters/OnnxExpertRunner.cs` | Numeric semantics, awaited native execution and joined cancellation |
| 14 | `src/ForgeMission.Core/ForgeMission.Core.csproj` | Core0.1.8, existing dependencies |
| 15 | `src/ForgeMission.Core/README.md` | Current contracts; init owns orphan reaping, Core owns direct root/group |
| 16 | `tests/ForgeMission.Mcl.Tests/Manifest/ForgeTomlReaderTests.cs` | Full/distribution parsing and stage-refactor regressions |
| 17 | `tests/ForgeMission.Mcl.Tests/Experts/ExpertLoaderTests.cs` | Pure diagnostic source and declared string parameter regressions |
| 18 | `tests/ForgeMission.Mcl.Tests/Runtime/DurableMissionPackageValidatorTests.cs` | Assets/hash/size/input/profile/model and generated JSON round trips |
| 19 | `tests/ForgeMission.Mcl.Tests/Runtime/AgentToolPipelineTests.cs` | Semantic replay and malformed checkpoint refusal without invocation |
| 20 | `tests/ForgeMission.Mcl.Tests/Runtime/PipelineTraceTests.cs` | Exact sequential/nested/parallel/loop/delta/pause keys |
| 21 | `tests/ForgeMission.Mcl.Tests/Adapters/ExecExpertRunnerTests.cs` | Process/I/O/mappings/live registry and controlled cleanup precedence |
| 22 | `tests/ForgeMission.Mcl.Tests/Adapters/OnnxExpertRunnerTests.cs` | Numeric, actual in-flight cancellation and real parallel sibling launch |
| 23 | `tests/ForgeMission.Mcl.Tests/Cli/ForgeProjectTests.cs` | Actual published Client0.9.3 Create/Open/Reconnect ABI |
| 24 | `tests/ForgeMission.Mcl.Tests/Fixtures/onnx/README.md` | Unchanged fixture provenance and hashes |
| 25 | `tests/ForgeMission.Mcl.Tests/Fixtures/onnx/identity.onnx` | Unchanged numeric fixture |
| 26 | `tests/ForgeMission.Mcl.Tests/Fixtures/onnx/cancellable-loop.onnx` | Unchanged native cancellation fixture |
| 27 | `.github/workflows/publish-core-package.yml` | Normal0.1.8 publication and Linux numeric/cancellation adapter slice |
| 28 | `eng/verify-core-package.sh` | Normal metadata/content/source/dependency verification |
| 29 | `Makefile` | Existing Core version declaration0.1.8 |
| 30 | `src/ForgeMission.Core/Adapters/ExecProcess.cs` | Owned lifetime boundary; bare Linux PID1 guard before allocating/launching |
| 31 | `src/ForgeMission.Core/Adapters/ExecProcess.Posix.cs` | Retained atomic group/root; remove adopted-child scanning/reaping |
| 32 | `src/ForgeMission.Core/Adapters/ExecProcess.PosixNative.cs` | Retained narrow imports; remove scan-only `getpgid` import |
| 33 | `src/ForgeMission.Core/Adapters/ExecProcess.Windows.cs` | Preexecution job ownership and joined handles |
| 34 | `src/ForgeMission.Core/Adapters/ExecProcess.WindowsNative.cs` | Existing narrow Win32 declarations |
| 35 | `src/ForgeMission.Core/Adapters/ExecProcessArguments.cs` | Preserved Unix resolution and literal Windows argv |
| 36 | `tests/ForgeMission.Exec.Probe/ForgeMission.Exec.Probe.csproj` | Existing maintained public-Core-API native probe |
| 37 | `tests/ForgeMission.Exec.Probe/Program.cs` | All-host assertions, exact-image init proof and bare-PID1 refusal |
| 38 | `tests/ForgeMission.Exec.Probe/ExecProbeChild.cs` | Decisive pressure and retained controlled child modes |
| 39 | `tests/ForgeMission.Mcl.Tests/ForgeMission.Mcl.Tests.csproj` | Existing probe build reference, `ReferenceOutputAssembly=false` |
| 40 | `ForgeMission.slnx` | Probe included in normal managed compilation |
| 41 | `scripts/build.ps1` | Normal native gates; exact published-image proof and separate PID1 negative |
| 42 | `scripts/README.md` | Correct native commands, image identity, scoped diagnostic retention and ownership/evidence distinctions |
| 43 | `.github/workflows/release.yml` | Failure-only macOS probe diagnostic artifact outside shipped-release pattern |

No file beyond this43-path inventory, dependency, process framework, public factory or configuration knob. No Registry, CLI behavioral migration, Host, Runner, Client, infra or deployment change. Existing consumers remain pinned until their separately reviewed upgrades.

UI reference, layout, theme, responsive and browser gates are **N/A**. Runtime/default-path acceptance applies. Core changes no public service entry point, datastore, managed identity or credential boundary. Persistent Host and ephemeral Runner ownership, arbitrary user-vetted code and inherited process authority remain locked; no sandbox or egress claim.

## 2. Reuse

Retain these exact public producer surfaces:

```csharp
public sealed record PackageConfig(IReadOnlyList<string> Assets);

// ForgeManifest
public PackageConfig Package { get; init; } = new([]);

// ForgeTomlReader
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
    DurableMissionPackageInput Input,
    ForgeMission.Parser.Program Ast,
    Dictionary<string, ExpertDefinition> Experts,
    IReadOnlyList<string> AdmittedInputNames,
    IReadOnlyList<string> ProviderProfileNames);

// DurableMissionPackageValidator
public static bool TryCreate(
    string missionSource,
    IReadOnlyList<DurableResolvedExpertInput> resolvedExperts,
    IReadOnlyList<DurableMissionAssetInput> assets,
    out DurableMissionPackageInput? package,
    out string? reason);

public static bool TryValidateInputNames(
    ValidatedDurableMissionPackage package,
    IReadOnlyCollection<string> names,
    out string? reason);

public sealed record PipelineExecutionWorkspace
{
    public PipelineExecutionWorkspace(
        string RootDirectory,
        IReadOnlyDictionary<string, string> ArtifactPaths);
    public string RootDirectory { get; }
    public IReadOnlyDictionary<string, string> ArtifactPaths { get; }
    public string GetStepOutputDirectory(string stepKey, int attempt);
}

// PipelineRunOptions
public PipelineExecutionWorkspace? ExecutionWorkspace { get; init; }

// PipelineTraceEvent, preserving existing positional constructors
public string StepKey { get; init; } = "";

// ExpertLoader.Validate final optional parameter
IReadOnlyDictionary<string, string>? expertMarkdownByName = null
```

`ExecExpertRunner(string defaultTimeout = "30s")` remains public. Its existing internal workspace/StepKey/attempt constructor serves the single generic adapter path.

The six-argument package constructor is required by the actual CLI-selected published Client0.9.3 DLL. Keep that exact CLR member, with the primary seven-argument constructor selected for source-generated JSON. The retained Client/Contracts/Conversations.Contracts/Hands metadata inventory established no other changed-member shim requirement.

| Need | Existing equivalent and reuse decision |
|---|---|
| TOML distribution selection | Existing section/scalar/array/multiline reader and `ResolveValue`; select relevant rows before evaluation, then construct results through coherent stages in that parser |
| Pure diagnostics | Existing `ParseContent`, `SplitFrontmatter`, `FindKeyInBlock`; supplied complete markdown replaces filesystem evidence only |
| Package semantics | Existing Parser, ExpertLoader, canonical hash and validator; one shared reachable-input traversal |
| Replay identity | Existing checkpoint/fingerprint owners; explicit execution-semantic encoding replaces omissions without a serializer framework |
| Workspace | Retain the supplied live caller/Runner-owned `ConcurrentDictionary` through `IReadOnlyDictionary`; no copying, freezing or Core registration authority |
| Output paths | Existing StepKey/attempt; one pure workspace convention shared with later Runner collection |
| Native ONNX | Existing1.27.0 `SetLoadCancellationFlag`, `RunOptions.Terminate` and `Run` APIs |
| Process ownership | Retain the implemented atomic POSIX group/unreaped-root and Windows preexecution-job seam; `Process.Kill(entireProcessTree:true)` cannot retain descendants after early root exit |
| Container reaping | Verified distribution tini in published Runner0.20.6; remove Core’s duplicate adopted-child owner |
| Managed pipes | Existing `AnonymousPipeServerStream`/`SafePipeHandle` and BCL cancellable async I/O, including Windows async-over-sync cancellation |
| Literal execution | Existing `ProcessStartInfo`, current Unix lookup implementation and Windows argument formatter |
| Error precedence | Existing private `ThrowExchangeFailure` and `ExecProcessCleanupException`; controlled test exercises the actual policy |
| Native verification | Existing self-spawning probe, `build.ps1`, `Invoke-Checked`, normal canonical and release matrix owners |
| Exact-image proof | Reuse immutable public published Runner image and Docker; no image rebuild, entrypoint replacement or provider/server fixture for positive proof |
| Publication/default | Existing Core workflow/package verifier and normal installed CLI routes |

Previously inspected `asmichi.ChildProcess` and ProcessKit do not provide this retained-root/joined boundary unchanged. Their pinned evidence remains in the historical plan; no additional library research or replacement is needed.

Retain the private lifetime surface:

```csharp
internal abstract class ExecProcess : IAsyncDisposable
{
    internal static readonly TimeSpan CleanupBudget;
    internal static ExecProcess Start(ProcessStartInfo options);
    internal Stream StandardInput { get; }
    internal Stream StandardOutput { get; }
    internal Stream StandardError { get; }
    internal abstract Task ObserveExitAsync(CancellationToken cancellationToken);
    internal abstract void Terminate();
    internal abstract Task<int> JoinAsync(CancellationToken cleanupToken);
}

private static void ThrowExchangeFailure(
    Exception failure,
    IReadOnlyList<IOException> cleanupFailures,
    CancellationToken callerToken);

internal sealed class ExecProcessCleanupException : IOException;
```

`PosixExecProcess` and `WindowsExecProcess` remain private implementations. The new Linux check is a guard in existing `ExecProcess.Start`, not a new public lifecycle seam.

## 3. Sequence

1. **Approval and source boundary.** After fresh full plan reviews and explicit approval, recheck branch/SHA and the exact four held modifications. Recheck Core0.1.8 availability and preserve all earlier evidence. Continue the same unfinished branch without rebasing or touching another repository. Record unique evidence directory, source, commands, warnings and stage times.

2. **Retain package ABI/serialization.** Preserve the six-argument constructor and primary `[method: JsonConstructor]`. Existing generated metadata must round-trip omitted/null/empty assets with unchanged no-assets hash and nonempty bytes/ContentType/Sha256/Executable. Revalidate restored values. Do not introduce a public JSON context or compatibility reader.

3. **Retain actual Client regression.** Use the actual published Client0.9.3 `ForgeMission.Application.dll` through the configuration-aware CLI loader and existing `ApplicationComposition.Create` narrow HTTP seam. Exercise `CreateChatProjectAsync`, `Projects.OpenChatAsync(ProjectOpenRequest(home,"Chat",DurableConversation))`, then `ReconnectAsync`; typed responses correlate launch and identities, unknown routes fail. Record selected DLL/package identity/hash; no recompilation.

4. **Retain one parser and pure diagnostics.** Keep coherent row/section/assignment/construction stages, repeated-section resets, malformed-header handling, arrays/multiline values and source diagnostics. Distribution reads discard provider/execution/capability rows before environment evaluation and reject environment expressions in selected fields. Full local reads preserve current behavior. Supplied expert markdown is authoritative and complete; missing required source fails without disk fallback. Seed declared mission parameters as strings with `TryAdd`, preserving runtime keys and existing diagnostic policy. Keep `RunAsync`/`StreamAsync` before private helpers in both adapters.

5. **Retain pure common admission.** Select first mission and first parameter, including parameterless roots. Derive distinct Ordinal-sorted admitted names from root parameters and reachable expert inputs internally. ASCII identifiers and exact Ordinal reserved names/prefixes apply. Root parameters are required; expert inputs remain optional forwarding declarations; named inputs override literal lets; `token_count` remains ordinary. Reachable LLM profiles are distinct/sorted with no single-profile restriction. Admit existing llm/rule/json_extract/exec/onnx/http/search kinds and nested/parallel/loop composition; reject unknown kinds and environment AST.

6. **Retain assets/hash/size.** Validate actual bytes/digests/executable metadata; slash-relative canonical paths with no traversal/backslash/colon; OrdinalIgnoreCase duplicates and collisions with mission/lock/distribution/expert files and runtime inputs/outputs directories. Resolve ONNX model relative to expert location inside admitted package assets, permitting contained `../../models/...`. Preserve the existing format1 prefix exactly. Append the tagged, Ordinal-path-sorted asset section only when nonempty, using existing UTF-16-code-unit-length `Append` convention, invariant lengths/counts, lowercase digest and executable1/0. Cap actual generated camelCase/null-omitting UTF-8 package JSON at4MiB including base64/escaping.

7. **Retain replay/checkpoint/trace.** Fingerprint every execution-affecting AST/expert field with deterministic length-prefixed/invariant typed encoding; exclude source locations, expert scratch directory and workspace. Keep envelope1/inner3, reject older inner formats and changed admitted names/semantics. Keep stable relative root values and replayed writes unchanged across scratch roots, completed effects once, child workspace inheritance and exact StepKey/attempt on all applicable lifecycle facts.

8. **Retain consumed-shape rejection.** Existing codec rejects null/missing payload/root inputs and null input values before resume dereferences them. Validate actual consumed identity/fingerprint fields, positive ordinal/attempt, mission path/admitted entries, tool declarations/name/description/defined schema, messages/contents/pending last call and log keys/text/status/writes/write values. Preserve legitimate empty collections, optional Reason and typed write semantics. Mutate actual serialized member names, including both absent and null root-input fields, from a valid current checkpoint. Return `InvalidContinuation` with no provider/exec invocation; no serialization framework or compatibility format.

9. **Retain workspace mappings/live authority.** Root remains absolute; registry is the supplied live caller-owned reference. Only declared values equal to validated registry keys become absolute in the process-local copy. Preserve root/checkpoint/context relative values and opaque output text. `GetStepOutputDirectory` remains pure `outputs/<lowercase SHA256 UTF8 StepKey>/<invariant attempt>`; exec creates it. Runtime directory bindings overwrite authored values only in the process copy. Remove stale inherited `FORGE_INPUT_*`/`FORGE_SOURCE_FILE`; supply work/input/output aliases and verified file aliases, legacy source only for an actual verified declared `source_file`. Preserve other authored/inherited environment and process identity.

10. **Apply bare-PID1 prelaunch guard.** At the beginning of existing `ExecProcess.Start`, before constructing either process implementation, allocating pipes or calling native spawn:
    ```csharp
    if (OperatingSystem.IsLinux() && Environment.ProcessId == 1)
        throw new InvalidOperationException(
            "Executable execution requires an init parent; Linux PID 1 is unsupported.");
    ```
    Existing `ExecExpertRunner.RunAsync` start handling returns its failed envelope containing the reason. No alternate launch, global signal handler or public exception contract. The negative probe invokes the public adapter with a child mode that would create root/PID/sentinel files and verifies failure plus absence of every launch marker.

11. **Remove duplicate adopted-child owner.** Delete Linux `DrainAdoptedChildrenAsync`, `InspectAdoptedChildren`, `ReadChildren`, `ReadTaskChildren`, `ReapAdoptedChild` and their call from `JoinAsync`. Delete scan-only `getpgid` import. No Core `/proc/self/task` child enumeration, adopted-child reap, global subreaper or `waitpid(-1)`. Init alone owns orphan adoption/reaping.

12. **Retain POSIX direct ownership.** `posix_spawn` establishes pgroup0 with `POSIX_SPAWN_SETPGROUP` before target execution; existing dup/chdir actions preserve handles/cwd. Retain checked ABI allocations/constants, Darwin CLOEXEC-default flag and prompt parent child-end closure. Fixed-root nonblocking `waitid(P_PID, WEXITED|WNOWAIT|WNOHANG)` observes without reaping; awaited10ms cancellable delay, EINTR handling and identity-loss refusal remain. Retain root through group termination, stream joins and exact final `waitpid(rootPid,WNOHANG)`. ECHILD invalidates identity; never signal a possibly recycled group. Linux no longer attempts orphan-entry cleanup.

13. **Retain macOS completion proof.** With observed/unreaped root, pending kill EPERM is resolved only by `proc_listpids(PROC_PGRP_ONLY=2, rootPid, int[2],8)` returning exactly4bytes and held root PID. Apply root-only observation after successful kill too. Eight bytes means additional members and bounded retry; malformed/error/root-missing results fail. Five-second cleanup budget and10ms awaited retries remain. Preserve original EPERM plus deadline/observation context on failure; no blanket permission suppression or new scanner.

14. **Retain Windows ownership.** Kill-on-close job, `PROC_THREAD_ATTRIBUTE_JOB_LIST` and explicit handle list associate ownership before `CreateProcessW` executes target code. Retain root/job/pipe handles; observe nonblocking root and job active count, checked termination and joined disposal. No post-launch assignment race, numeric-PID substitute or abandoned wait.

15. **Retain command and exchange semantics.** Unix resolution remains absolute request, executable-directory, parent cwd, then executable PATH candidates before expert child cwd; authored argv0/literal arguments retained. Windows keeps current command lookup/escaping/environment/cwd. Concurrent raw UTF-8 stdin/stdout/stderr caps remain4MiB/4MiB/64KiB. Recognize declined stdin only at write boundary after checking cancellation: Unix inner SocketException native32/EPIPE/Shutdown; Windows HRESULT8007006D/800700E8/800700E9. Other IOException, operation-aborted995, invalid handle6 and message matching are not ignored. Valid exit and declared JSON/output key remain mandatory.

16. **Retain joined failures/precedence.** Track and join all exchange/observer work; on failure cancel its tokens, await work and perform safe checked cleanup under separate five-second deadline. Normal completion also terminates remaining ordinary group/job members before releasing ownership. Cleanup errors remain visible IOException and take precedence over caller cancellation, preserving original exchange/cleanup context through the existing private selector. Successful cleanup permits OCE; timeout/size/ordinary I/O keep failed envelopes; malformed output preserves existing load error. No fallback, unsafe external root reap or fault manufactured by corrupting live identity. Existing controlled reflection cases prove the actual selector, honestly labelled controlled.

17. **Finish decisive pressure correction.** In existing `PressureAsync`, finish **both** output tasks—valid JSON containing1,000,000 `o` characters and stderr exactly64KiB—**before** reading and verifying2,000,000 `i` characters from stdin. The parent must concurrently drain both output streams while writing stdin. A stdin-first sequential parent cannot pass because the large stdout blocks the child before its stdin read. Do not require stderr independently exceed every platform capacity or add capacity probes/knobs. Preserve caps, valid JSON, finite expert/test deadlines and joined cleanup.

18. **Retain environment/workspace/native fixtures.** Held native probe cases continue exercising actual public `PipelineRunner`, expert cwd, literal args/PATH, process-local file alias mappings, runtime directories, authored/inherited FORGE precedence and unchanged relative caller values. The same-workspace live-registry managed test registers a new path after first exec and observes it on subsequent exec without replacing the workspace; caller/checkpoint values remain relative.

19. **Retain ONNX and actual parallel proof.** Existing awaited `Task.Run(..., CancellationToken.None)` owns native load/inference, permitting sibling launch. Native cancellation terminates load/run, then joins before disposing registrations/options/session/results or returning OCE. Numeric semantics remain float `[1,n]`, input `input`, probabilities-or-last output, index1-or0 and threshold. Keep standalone in-flight/no-score test and held actual `PipelineRunner` parallel Loop/sibling trace test: sibling completes while numeric branch remains unfinished, bounded cancellation joins, no numeric completion or cancelled score is published.

20. **Replace normal Linux container gate.** In existing `Test-NativeExec`, retain all-host native publish/run. For normal Linuxx64 GitHub Actions only, use immutable:
    ```
    ghcr.io/katasec/forge-runner@sha256:c0031d451d046d4f28a75ca7f7c7f26169b26e92555de4b601128a005670331c
    ```
    Pull/record image identity and native proof mount. Start a unique container using its normal entrypoint unchanged, no `--init`, no command replacement or provider credentials:
    ```
    docker run --detach --name <unique> \
      --mount type=bind,source=<absolute-native-probe-dir>,target=/proof,readonly \
      <immutable-image>
    docker exec <unique> /proof/ForgeMission.Exec.Probe --verify-init
    ```
    `--verify-init` asserts Linux/non-PID1, actual `/proc/1/comm=tini`, direct dotnet child with Runner DLL cmdline, then executes the public Core cases. Bounded topology observation accommodates ordinary startup; no provider/HTTP server fixture or extra readiness dependency. Record normal image entrypoint, revision label, index/platform identity and topology. Always remove only the owned container.

21. **Separate init and negative observations.** Under the positive image, preserve timeout/caller-cancel early-root child/grandchild proofs, no late sentinel and unrelated managed child exact exit37. After adapter return, separately wait a fixed bounded deadline for descendant `/proc/<pid>` entries to disappear under init; do not claim Core reaped them. Separately run the same mounted native probe as actual bare PID1, explicitly labelled unsupported negative:
    ```
    docker run --rm \
      --mount type=bind,source=<absolute-native-probe-dir>,target=/proof,readonly \
      --entrypoint /proof/ForgeMission.Exec.Probe \
      <immutable-image> --verify-pid1-refusal
    ```
    No `--init`. Negative mode passes only after public failed-envelope/no-child proof; it does not execute the ordinary lifecycle suite. Docker unavailable/probe failure fails required Linuxx64 CI, never skips. No Runner/image edit.

22. **Stabilize then freeze.** Run focused/full managed, Release/package and managed probe first, then commit/push same PR77 and normal final-source canonical/four-host native gates. Preserve failed logs and exact Reason/error evidence; do not invent the historical canonical failure’s cause. The existing branch-package public consumer remains production-source evidence; current retention-only correction does not rebuild unchanged source. Supervisor receives full43-path diff/evidence, conducts sequential complete reviews and owns CI follow-through, merge, normal publication, published/installed acceptance and closure.

## 4. Verification

Every gate records exact current source, commands, exit codes, raw logs, warnings and unique scratch provenance. Earlier PASS results do not close revised-source gates.

| Gate | Required route/observation |
|---|---|
| Focused eight families | `env -u MCL_API_KEY dotnet test tests/ForgeMission.Mcl.Tests/ForgeMission.Mcl.Tests.csproj -c Debug -warnaserror --filter "FullyQualifiedName~DurableMissionPackageValidatorTests|FullyQualifiedName~ForgeTomlReaderTests|FullyQualifiedName~ExpertLoaderTests|FullyQualifiedName~AgentToolPipelineTests|FullyQualifiedName~PipelineTraceTests|FullyQualifiedName~ExecExpertRunnerTests|FullyQualifiedName~OnnxExpertRunnerTests|FullyQualifiedName~ForgeProjectTests"` |
| Full normal managed | `dotnet build ForgeMission.slnx -c Debug -warnaserror`; `env -u MCL_API_KEY dotnet test ForgeMission.slnx -c Debug -warnaserror --blame-hang-timeout 90s`; unfiltered, no new exclusion |
| Release/package | `dotnet build src/ForgeMission.Core/ForgeMission.Core.csproj -c Release -warnaserror`; `make verify-core-package`; retain documented full-Release reflection-loader caveat without substituting for Debug |
| Managed probe | Normal built executable/apphost, all ordinary host cases; exact positive JSON/environment/workspace/lifecycle results |
| Canonical native | Existing macOS14 PR route `make cli-script-test cli-verify verify-terminal-extensions-package`; final SHA, zero raw compiler/linker/trim/ILC warnings, no minimum-OS workaround |
| Four native hosts | Existing release PR matrix `make cli-script-test cli-package`, osx-arm64/linux-x64/linux-arm64/win-arm64; normal probe publish/run in each, not help/version alone |
| Exact Linux container | Mandatory normal Linuxx64 CI positive unchanged-entrypoint image plus separate bare-PID1 negative commands above; image/source/topology/behavior logs, no availability skip |
| Linux ONNX | Existing normal Ubuntu Core publication adapter slice runs numeric, actual in-flight cancellation and real pipeline sibling tests without skip |
| Branch consumer | Fresh unique local package/version/cache; package references only; exact nupkg/source/hash and API/JSON/exec/trace/numeric/cancellation observations |
| Actual Client ABI | Real selected published Client0.9.3 DLL through current composition, Create/Open/Reconnect, recorded DLL/hash, no rebuild |
| Normal publication | Merged source, immutable Core0.1.8/core-v0.1.8 route, package ownership/private visibility, README/license/nuspec source and Parser/Scout0.1.0 dependencies |
| Published default package | Supervisor independently restores normal0.1.8 into another fresh cache and exercises public APIs; no substituted DLL/local cache contamination |
| Installed Project/Chat | Clean merged-main normal `make install`, complete native payload; disposable `forge project create`, actual piped Chat, normal login/API and absent build/endpoint overrides |
| Installed local regression | Same normal provider/config: `forge init`/`forge run`, real Hands Write→Read independently verified, outside sentinel unchanged, tool-free execution and cancelled-session cleanup |

Required focused positive/negative coverage:

- Full/distribution TOML, repeated sections, table/array/header/multiline behavior; discarded provider values never evaluated/transferred; selected environment expressions fail.
- Parameterless/multi-expert/nested/parallel/loop packages, all existing kinds/profiles; invalid kind/environment/name/asset/model/size/hash fail before execution.
- Omitted/null/empty assets retain old hash; executable bit/bytes changes alter identity; generated JSON round trips preserve metadata.
- Root-required/reachable-optional inputs, empty literals, named-over-let and `token_count`; reject undeclared/duplicate/reserved/unreachable widening.
- Supplied `goal:string` succeeds; `goal:double` reports existing MCL012 with immutable source location; absent supplied source cannot fall back to disk.
- Changed command/args/timeout/model/endpoint/bindings/schema/admitted names refuse resume; fresh scratch alone does not; completed side effects occur once.
- Missing/null consumed current checkpoint shapes return `InvalidContinuation` before provider/exec invocation; older inner formats fail.
- Exact trace keys and same-workspace later registration; only process copies get absolute paths/runtime values.
- Fast unread stdin small/near-cap, explicit close/wait, actual platform mappings, cancellation and genuine other I/O failure.
- Decisive concurrent pressure, exact/over caps, parent-sleeps and parent-exits-first descendants, no late sentinel, unrelated managed child exit ownership.
- Bare Linux PID1 refusal before child launch; under exact published init image, stopped execution and bounded orphan-entry disappearance are separately observed.
- macOS fast no-descendant, live descendant and already-exited descendant root-only completion before reap; controlled cleanup precedence remains distinct from actual kernel fault claims.
- Numeric thresholds, missing model/features, actual unfinished native cancellation, sibling launch and no cancelled score/write.

Unchanged fixtures:

| Fixture | Bytes | SHA256 |
|---|---:|---|
| Identity | 130 | `691a2d6476544d32195258db2e889967b1b25964ba89c209363b40bc9593425a` |
| Loop | 473 | `8d27a5864193f0d90a225ef7b0ab2e39677b771a082c975878268ba6e912aa5a` |

Keep Python3.13/onnx1.19.0 test-only generation provenance; runtime tests need only normal native ONNX package.

Read-only readiness facts: Core inventory returned0.1.7 through0.1.0, with0.1.8 absent; supervisor independently observed release tag404. Recheck immediately before mutation/publication. Current production source remains unchanged at0d0a338f; exact tree identity is recorded. Prior managed/native observations and hosting acceptance retain only their named source/layer; failed macOS matrix and normal publication/default gates remain open.

The producer default is normal published Core plus installed CLI regressions. Exact-image native verification is controlled component proof; later cloud content/protocol/client tasks remain open.

## 5. Principles that changed a decision

| Rule | Concrete choice |
|---|---|
| 1 No NIH | Reuse verified tini, existing parser/diagnostics/pipes/native ONNX/build/publication owners |
| 2 No duplicate paths | Remove Core orphan reaper; keep one generic adapter and exact existing image entrypoint |
| 3 Minimum needed | Complete43-path producer inventory; only README/workflow delta, no consumer/runtime image/protocol/deployment work |
| 4 No speculative abstractions | Direct private PID1 guard; no process framework, factory, capacity knob or serialization reader |
| 5 Stay in scope | Apply locked init split and resolved pressure obligation; deviations return before edits |
| 6 Verified means done | Require final-source native, published-package and installed-default observations |
| 7 Outline first | Prepare/start/exchange/cleanup/result and native proof flow visible first |
| 8 Small functions | Keep coherent spawn, observation, cleanup, parsing and probe scenarios |
| 9 Top-down order | Public Run/Stream before helpers; entrypoints before native details |
| 10 Explicit errors | Cleanup IOException precedence, joined work and no unowned fallback |
| 11 Shallow nesting | Early PID1/shape guards and bounded straightforward observation loops |
| 12 Separate side effects | Existing native/process/file/build boundaries remain named |
| 13 Zero warnings | Managed stabilization before unchanged canonical/all-host raw warning gates |
| 14 Extract for reason | Existing lifetime owner supplies atomic identity; remove obsolete scan helpers |
| 15 Complexity≤15 | Retain cohesive parser refactor and review actual classic decisions, not wrapper scores |

## 6. Open questions and assumptions

No unresolved architecture or pressure-test question. Supervisor resolved pressure as1,000,000-byte stdout and64KiB stderr completed before2,000,000-byte stdin; independently exceeding every stderr pipe capacity is not required.

The verified published image has exact immutable index above, source `17080b73a84ad0b0e42a891024090e8f997194ed`, amd64 manifest `sha256:cf2a7e14f639519af223dd1efa0a2adf4d40cf1cac5f1b7d2017c5860ca1078b` and arm64 manifest `sha256:ff3eab0400405722fc0ff4059624bd7617aa64600f4eaa3e5473c16cf43087c9`. Normal unchanged-entrypoint startup without provider credentials has already been demonstrated by hosting verification; Core CI must observe its own exact-image run.

Ordinary descendants are the lifecycle guarantee. Deliberate group/job escape, sandboxing, permissions and egress remain outside scope. Init reaps adopted orphans; Core retains and reaps its own direct root. Required Docker/native/ABI/immutable-version failures return visibly and cannot be replaced by an override or inherited PASS.

The supervisor header records current implementation approval. Merge, publication and completion remain gated.
