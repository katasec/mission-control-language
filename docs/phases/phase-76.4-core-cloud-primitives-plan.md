# Phase 76.4 — Complete implementer correction plan, round 12

**Implemented and accepted.** PR77 merged as `1ecef9ae`; Core0.1.8 and installed defaults pass.
This is the retained approved plan, not a new implementation assignment. See
[completion evidence](phase-76.4-core-cloud-primitives_completed.md#installed-default-acceptance-and-core-closure).

**PLAN APPROVED2026-10-10 04:00:12 UTC.** Both complete current plan reviews PASS; supervisor
independently checked source/hash, exact native ABI, failure/parent-state/public/default gates.
Reviewed pre-approval SHA25655314806A4C23EF0724236B4DB90BD7DAA1883CD33313E3EE2D6BE964E50448A.
Approval authorizes only the five correction paths; no merge/publication approval. Same implementer observed2026-10-10
03:49:28–03:52:28 UTC; supervisor assignment03:48:49, result available03:54:39 UTC.
Supervisor transcribes the complete returned plan: the current43-path inventory, retained exact
contracts/sequence/gates below and this correction form one complete artifact. Prior r11 plan is
[historical evidence](phase-76.4-core-cloud-primitives_completed.md#superseded-complete-correction-plan-r11).
Baselinec4176ba5598b77538829350e8bb7c8fc986bf8b0, base8d28dc1, same branch/PR77.
Locked correction design r12 passed both complete design reviews. Only31,32,37,38,42 may change
after fresh complete plan reviews and explicit supervisor approval. No new path or dependency.

## Current correction r12 — macOS caught-disposition defaults

### 1. Files and scope

All43 retained paths are listed below. New edits only:

| Inventory | File | Correction |
|---|---|---|
| 31 | src/ForgeMission.Core/Adapters/ExecProcess.Posix.cs | Checked caught-default attributes before existing atomic macOS spawn |
| 32 | src/ForgeMission.Core/Adapters/ExecProcess.PosixNative.cs | Exact private Darwin layout/constants and two libc imports |
| 37 | tests/ForgeMission.Exec.Probe/Program.cs | macOS public-adapter disposition scenario in normal probe flow |
| 38 | tests/ForgeMission.Exec.Probe/ExecProbeChild.cs | Embedded native C witness/build and scoped test-parent helpers |
| 42 | scripts/README.md | Matched crash cause, regression and narrow retention reassessment |

No new public API/configuration/consumer/image/infra/workflow/version/permission policy. UI/browser
N/A. Runtime/security/engineering/default gates apply; no tier/store/identity/credential/authority
change. Arbitrary user-vetted code and inherited executable authority remain, with no isolation claim.

### 2. Reuse and exact native additions

Existing ConfigureSpawn, allocated attributes, PosixNative.Check and normal probe scratch/dispatcher
remain owners. No alternative launcher/signal policy/production callback/helper framework. Native C
witness uses SDK clang already required by macOS native gates; no library or unsafe project change.
Existing probe imports duplicate only test-observation ABI, without exposing production internals.
Parent callback uses statically rooted explicitly Cdecl three-argument delegate and generic BCL
Marshal.GetFunctionPointerForDelegate. Existing parser/diagnostics/admission/replay/live workspace,
anonymous pipes/retained roots/atomic Windows jobs/ONNX1.27/build/publication owners stay exact below.

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct MacSignalAction
{
    internal IntPtr Handler;
    internal uint Mask;
    internal int Flags;
}
[DllImport("libc", EntryPoint = "sigaction", SetLastError = true)]
internal static extern int QuerySignalAction(
    int signal, IntPtr action, out MacSignalAction previous);
[DllImport("libc", EntryPoint = "posix_spawnattr_setsigdefault")]
internal static extern int AttributeSignalDefaults(IntPtr attributes, ref uint signals);
```

Verified public Darwin struct (not kernel __sigaction):16bytes, offsets0/8/12; sigset_t unsigned32.
Named private constants NSIG32, SIGKILL9, SIGSTOP17, SIG_DFL0, SIG_IGN1, SETSIGDEF0x4; existing
group0x2/CLOEXEC0x4000. Test SIGUSR2=31, SIGURG=16, SA_SIGINFO0x40, SA_RESTART0x2.
sigaction query null new-action returns0/-1; immediately capture Marshal.GetLastPInvokeError on
failure. Attribute setter returns direct errno and uses existing Check, never stale errno.

### 3. New correction sequence

1. After approval recheck baseline/branch/base/inventory/version absence, preserve actual failed
   reports/matched symbols/r13 evidence and create a unique evidence directory. No rebase/other repo.
2. Existing macOS ConfigureSpawn calls one private ConfigureMacSignalDefaults(attributes) below its
   caller. Query1..31 except9/17; include only handler neither0 nor1, with1u<<(signal-1). Check every
   query; failures throw named Win32Exception with captured errno before launch. Check set-default
   attribute, then checked flags0x4006; continue existing group/dup/cwd/atomic spawn. Linux flags2 and
   Windows unchanged. No signal delivery, parent mutation or mask setter. Existing failed-envelope
   and setup-resource disposal remain. No runtime-specific signal list/reset-all/retry/JSON rewrite.
3. Embed small C witness source in existing ExecProbeChild.cs. Compile in unique probe scratch with
   /usr/bin/xcrun clang -std=c11 -Wall -Wextra -Werror. Static assert SDK sizes4/16 and offsets0/8/12.
   main immediately queries SIGUSR2/SIGURG with checked errno before any runtime installs handlers;
   emit existing valid {"result":"..."} containing numeric handler/flags. Compiler helper uses literal
   ProcessStartInfo args, concurrent stdout/stderr drains, ten-second bounded joined lifetime and
   owned cleanup; timeout/nonzero/compiler warning fails, no retry. No environment dump/payload.
4. Program normal flow invokes VerifyMacSignalDefaultsAsync only on macOS. Save both complete
   original actions; install no-op rooted Cdecl three-argument callback on SIGUSR2 with flags0x42
   and empty mask, SIGURG ignored1/flags0/empty mask. Check installs and installed snapshots. Never
   replace SIGUSR1/runtime activation handlers or send a signal. Launch compiled witness directly
   through public ExecExpertRunner.RunAsync with existing expert/cwd/JSON/deadline conventions.
   Require pass, caught handler0/flags0 and ignored handler1/configured flags unchanged. Query parent
   afterward and compare handler/mask/flags to installed snapshots. Finally restore each saved/changed
   signal independently even if another restore fails, check readbacks and preserve original plus
   restoration errors. Static field roots callback throughout/afterward. No skip if clang/witness fails.
5. Update scripts README with exact matched cause/regression without premature CI acceptance.
   Retain existing narrow collector/upload unchanged while corrected-source checks are outstanding;
   supervisor reassesses value after acceptance. No removal/expansion in this correction.
6. Preserve every producer behavior below: exact Client ABI/generated JSON, pure common admission,
   actual4MiB/assets/hash, checkpoint3/semantic replay, live registry/StepKey, literal exec bindings,
   PID1 guard/init owner, atomic group/job/root/error precedence, decisive unchanged pressure/caps/
   deadlines, BCL pipe cancellation and joined native ONNX/parallel sibling. Do not redo implemented
   behavior or broaden this correction.
7. Because production changes, run fresh managed/focused/full/Release/package/managed probe, actual
   published Client ABI and final-source local package in a new empty isolated consumer cache before AOT.
   Freeze/commit/push same PR77, full43-path binary diff/inventory/hash and exact five-path delta.
   Fresh complete sequential code reviews and normal final-source canonical/all-four-host CI follow;
   supervisor owns merge/publication/published/default acceptance/closure.

### 4. Verification additions and retained gates

No earlier PASS closes revised-source gates. Managed size/offset assertions16/0/8/12 and C static
assertions prove the exact ABI. Real C witness through public adapter proves caught reset/ignored
preservation/unchanged parent/exact restore before managed child startup. Setup/restore/compiler
errors fail visibly. Signal regression is mandatory on macOS, with no new cross-host semantics.
Run fresh focused eight families, full unfiltered Debug build/tests, Core Release and package checks,
30existing script/parser/hygiene checks, actual Client0.9.3 DLL regression, distinct branch-consumer
cache and all ordinary managed probe scenarios. Final normal macOS14 canonical and all-four-host
native routes retain zero compiler/linker/trim/ILC warnings and original payload/deadline checks.
Mandatory exact Runner0.20.6 image/init positive and bare-PID1 negative stay unchanged. Linux ONNX
publication slice remains real numeric/in-flight/parallel proof. All exact commands/default facts
are retained below. Recheck immutable0.1.8/tag absence immediately before normal merged-main
workflow_dispatch publication; tag trigger exists but is not required. Fresh remote published package
consumer and installed Project/Chat/Hands/default/cancellation observations remain supervisor gates.

### 5. Principles that changed decisions

| Implementer rule | Choice |
|---|---|
| 1 No NIH | Spawn attributes/libc/SDK clang/existing public probe and gates |
| 2 One path | Existing macOS launch correction, no alternate launcher/collector |
| 3 Minimum | Five edits within43 paths; no mask reset/runtime patch/consumer/infra |
| 4 No speculative abstraction | One native configuration boundary and concrete test support |
| 5 Scope | Locked caught-only correction; deviations return before edits |
| 6 Verified | Before-runtime witness/current native/published defaults |
| 7 Outline | Caught defaults then existing atomic launch visible |
| 8 Small steps | Query/configure/compiler/scoped state boundaries |
| 9 Top-down | Callers and scenario before details |
| 10 Explicit errors | Query errno/direct attribute error distinct; original/restore errors preserved |
| 11 Shallow | Early platform/uncatchable/default/ignored guards and one loop |
| 12 Side effects | Named native/config/compiler/scoped state boundaries |
| 13 Warnings | Managed then normal AOT; C warning-as-error |
| 14 Extraction | Real native/scoped lifetime boundaries, not metric-only |
| 15 Complexity | Straight traversal; classic decisions measured at review |

### 6. Open questions and assumptions

None unresolved. Actual report/register/disassembly/source/repro supports cause; no crash-time saved
handler memory snapshot. macOS native gates remain failed until corrected source passes. Missing
compiler/failed witness/restoration error fails. Existing Runner exact source/digests, ordinary
descendant guarantee/init ownership and all default facts remain below. Deliberate escape/sandbox/
permissions/egress remain outside scope. Implementation/merge/publication/completion separately gated.

## Complete retained producer contract

The following full inventory/APIs/sequence/gates remain part of current r12. Implemented behavior is
retained; only five paths above change. Historical no-unchanged-rebuild/retention-only permissions
do not apply to this production correction. Current-source verification is required.

## 1. Files

Continue current cleanc4176ba5 on `adeen/phase-76-core-primitives`, base8d28dc1, same draftPR77.
All43 paths below are relative to `/Users/ameerdeen/progs/forge-mcl`. This is the complete current
inventory. Only31,32,37,38,42 change after current r12 plan approval.

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
| 31 | `src/ForgeMission.Core/Adapters/ExecProcess.Posix.cs` | Retained atomic group/root plus macOS caught-disposition defaults |
| 32 | `src/ForgeMission.Core/Adapters/ExecProcess.PosixNative.cs` | Retained narrow imports plus private Darwin sigaction/default-set ABI |
| 33 | `src/ForgeMission.Core/Adapters/ExecProcess.Windows.cs` | Preexecution job ownership and joined handles |
| 34 | `src/ForgeMission.Core/Adapters/ExecProcess.WindowsNative.cs` | Existing narrow Win32 declarations |
| 35 | `src/ForgeMission.Core/Adapters/ExecProcessArguments.cs` | Preserved Unix resolution and literal Windows argv |
| 36 | `tests/ForgeMission.Exec.Probe/ForgeMission.Exec.Probe.csproj` | Existing maintained public-Core-API native probe |
| 37 | `tests/ForgeMission.Exec.Probe/Program.cs` | All-host assertions/init/PID1 proof plus macOS public-adapter signal regression |
| 38 | `tests/ForgeMission.Exec.Probe/ExecProbeChild.cs` | Decisive pressure/child modes plus native C disposition witness/scoped test state |
| 39 | `tests/ForgeMission.Mcl.Tests/ForgeMission.Mcl.Tests.csproj` | Existing probe build reference, `ReferenceOutputAssembly=false` |
| 40 | `ForgeMission.slnx` | Probe included in normal managed compilation |
| 41 | `scripts/build.ps1` | Normal native gates; exact published-image proof and separate PID1 negative |
| 42 | `scripts/README.md` | Native commands/image/scoped diagnostics plus matched cause and disposition regression |
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

## 3. Retained complete producer sequence

1. **Approval and source boundary.** After fresh full plan reviews and explicit approval, recheck branch/SHA and the five authorized correction paths. Recheck Core0.1.8 availability and preserve all earlier evidence. Continue the same unfinished branch without rebasing or touching another repository. Record unique evidence directory, source, commands, warnings and stage times.

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

22. **Stabilize then freeze.** Apply the r12 correction sequence above, run fresh focused/full managed, Release/package, actual Client ABI, managed probe and a new distinct branch-package public consumer, then freeze/commit/push same PR77. Preserve actual failed reports and qualified causal evidence. Normal final-source canonical/four-host native gates and fresh complete sequential reviews follow. Supervisor owns merge, normal publication, published/installed acceptance and closure.

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
| Normal publication | Merged source, normal workflow_dispatch on main, immutable Core0.1.8 (tag trigger is optional), package ownership/private visibility, README/license/nuspec source and Parser/Scout0.1.0 dependencies |
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

Read-only readiness facts: Core inventory returned0.1.7 through0.1.0, with0.1.8 absent; supervisor independently observed release tag404. Recheck immediately before mutation/publication. Baselinec4176ba5 production tree is recorded; r12 changes production and requires fresh evidence. Prior managed/native observations and hosting acceptance retain only their named source/layer; failed macOS matrix and normal publication/default gates remain open.

The producer default is normal published Core plus installed CLI regressions. Exact-image native verification is controlled component proof; later cloud content/protocol/client tasks remain open.

## 5. Principles that changed a decision

| Rule | Concrete choice |
|---|---|
| 1 No NIH | Reuse verified tini, existing parser/diagnostics/pipes/native ONNX/build/publication owners |
| 2 No duplicate paths | Remove Core orphan reaper; keep one generic adapter and exact existing image entrypoint |
| 3 Minimum needed | Complete43-path producer inventory; five private-launch/probe/README edits, no consumer/runtime image/protocol/deployment work |
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

The supervisor header records current approval state; no edit until explicit PLAN APPROVED. Merge, publication and completion remain gated.
