# Phase 76.4 — implementer correction plan, round 6

**Status: PLAN APPROVED** 2026-10-09 23:13:40 UTC after fresh full simplicity and ownership reviews. Approval covers only this complete r6 Core correction plan; merge/publication/acceptance remain gated.
[Task/design](phase-76.4-core-cloud-primitives.md). Prior approvals and review findings are recorded in [completion evidence](phase-76.4-core-cloud-primitives_completed.md).

## 1. Files

Complete Phase 76.4 correction plan, round 6. Assignment start: **2026-10-09 23:10:31 UTC**. Supervisor approval is recorded above; implementation requires the same implementer's explicit bounded handoff. Product source remains clean at `770778d2d20a8c202552d7c3f3dd1e2cbdc7425c`, branch `adeen/phase-76-core-primitives`, draft [PR 77](https://github.com/katasec/forge-mcl/pull/77). Continue that same unfinished branch only after supervisor approval. Governing documents are the complete Phase 76.4 plan/task and locked Phase 76.2 contracts. UI and browser visual gates are N/A; runtime/default-path acceptance applies.

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

## 2. Reuse

### Public producer and compatibility contracts

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
| Native/PID1 verification | Reuse existing build.ps1 Invoke-Checked, source identity and raw warning gates. A small executable probe can spawn itself on all four native hosts and run as PID 1; it avoids an assumed installed Python/shell wrapper. Existing release workflow already calls the script. |

### Exact internal launch/lifetime seam

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

Windows creates a kill-on-close Job Object, sets STARTUPINFOEX `PROC_THREAD_ATTRIBUTE_JOB_LIST` and `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`, then calls CreateProcessW with that association **before target execution**. No post-launch AssignProcessToJobObject race. Keep the exact owned root/job handles through I/O and cleanup. Observe root with zero-timeout WaitForSingleObject plus cancellable delay; terminate with checked TerminateJobObject and observe job active-process count until zero before closing handles. WAIT_FAILED/query/termination failures are visible. No blocking abandoned waiter or PID-based Windows ownership.

## 3. Sequence

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

11. Keep ownership until cleanup finishes, even when the root exits before descendants holding stdout/stderr. The exchange tracks the three I/O tasks and root observer, cancels/awaits all started tasks on failure, and checks termination before root reaping. Apply the same group/job cleanup on normal completion before releasing ownership, so closing pipes cannot hide a continuing ordinary descendant. No post-launch setpgid/job assignment and no descendant snapshot chooses kill targets. POSIX kills the held group; Windows terminates the held job. A fixed private five-second cleanup budget with 10 ms delays bounds root/job/adoption observation; it is not a public timeout/config knob. Budget expiry is visible cleanup IOException, never presumed completion.

12. Implement the supervisor-locked Linux adopted-child drain only **after checked group termination and observed root exit**, retaining the root unreaped. Enumerate `/proc/self/task/*/children`, deduplicate exact child PIDs, exclude root, verify each candidate's getpgid equals the held root group, and reap only those exact candidates with waitpid(candidate,WNOHANG). Pending intermediate parents require another bounded observation pass; after each reap reread children to catch newly adopted deeper descendants. Root exit observation precedes scanning so kernel reparenting has occurred. ECHILD/ESRCH stale candidates trigger a fresh pass; a disappearing task directory triggers retry, not a silently empty result. Persistent /proc/native errors or budget expiry are cleanup IOException. Complete only after a full pass has no owned pending/adopted descendant, then reap root last. Never set a global subreaper, call waitpid(-1), reap unrelated children, kill from this scan or use Process.HasExited to classify orphan zombies as executing. This is the recorded private Type-2 choice; replace/remove it only when an authoritative supported primitive provides equivalent root-excluding adopted reaping, retaining the same tests and public boundary.

13. Make failure cleanup itself joinable. The root observer uses only nonblocking native calls and awaited cancellable delay; Windows root/job observation does likewise. Cancel the exchange observer/I/O tokens and await all already-started work, then perform bounded checked cleanup observation/reaping with a separate private deadline, not the cancelled caller token. Reuse .NET 10 PipeStream cancellation (Unix socket async; Windows async-over-sync CancelSynchronousIo) instead of adding blocked unmanaged I/O tasks. If Terminate fails, record it, stop/join observers and I/O, and perform only safe nonblocking/bounded observation; never block indefinitely in waitid/waitpid/WaitForSingleObject, abandon a task, or claim that a process was removed after OS cleanup failed. Do not reap/release the POSIX identity before any permitted remaining group operation. Native observation/termination/reap failures enter the cleanup failure list, taking precedence over caller cancellation. `ThrowExchangeFailure` throws ExecProcessCleanupException preserving cleanup operation/error and original failure context; only successful cleanup permits normal caller OCE. Ordinary timeout/size/I/O keep failed envelopes, malformed stdout/missing outputKey keep existing ExpertLoadException, and start/nonzero errors retain current visible behavior. Native ownership/setup failures never fall back to an unowned launch.

14. Prove that error selection safely through the actual private `ThrowExchangeFailure` member via the existing reflection test style: cancelled caller plus original OCE plus controlled cleanup IOException must throw IOException with both contexts, not OCE; ordinary I/O plus cleanup failure preserves both; cancelled caller with no cleanup failure gives OCE; uncancelled ordinary failure retains its original exception/stack. Test cases represent a controlled private boundary, not a real kernel termination/reap fault. Do not externally reap the held root, reuse its PID, sabotage native handles or create a wrong-target-kill window to manufacture a fault. Native behavior tests separately exercise real successful cleanup and native setup errors.

15. Keep existing ONNX features, float[1,n] input named input, probabilities-or-last output selection, index1-or0 and threshold semantics. Own the synchronous load/inference inside an awaited Task.Run(...,CancellationToken.None), allowing sibling branches to launch. Cancellation registration uses SessionOptions.SetLoadCancellationFlag/RunOptions.Terminate and the Run overload. Await native termination before disposing registration/options/session/results; never return on cancellation before native exit, and never write a cancelled score to context. The 130-byte identity fixture SHA256 is `691a2d6476544d32195258db2e889967b1b25964ba89c209363b40bc9593425a`; the 473-byte Loop fixture SHA256 is `8d27a5864193f0d90a225ef7b0ab2e39677b771a082c975878268ba6e912aa5a`. Preserve Python3.13/onnx1.19.0 test-only generation provenance. Native inference uses no generation dependency.

16. Extend the existing test build with a small self-spawning probe. Its child modes exercise unread-stdin closure, duplex pressure, root-exits-first ordinary child/grandchild retention, timeout/caller cancellation, literal argv and cwd/env mapping. Probe children communicate readiness/PIDs in unique scratch files, print valid JSON and inherit stdout/stderr naturally; bounded fixture lifetime and finally cleanup prevent a hung test leaving work. Assert no subsequent sentinel write and no live owned descendant. On Linux PID1 additionally require owned child/grandchild process entries gone after reap, distinguishing execution stopped from zombies. An unrelated .NET Process child must remain alive/waitable by its original owner and later return its requested nonzero exit code. No fixed shared temporary paths.

17. Use existing build.ps1 verify/package owners to publish the probe with normal Release/RID/PublishAot/warnaserror settings into a separate verification subdirectory, run it and preserve raw output; never add it to Write-Package's CLI payload or normal installed product. Every normal four-host package gate runs its native process behavior. In normal GitHub Actions Linux x64 verification, also run the same published native probe as container PID1 with no --init, using the Runner's actual `mcr.microsoft.com/dotnet/aspnet:10.0` runtime image. The existing CI fact GITHUB_ACTIONS plus linux-x64 selects this additional test, with no new user setting or workflow bypass. Docker/image/probe failure fails that gate; it is not skipped. No Runner/Dockerfile edit. Run the full unchanged canonical macOS managed/AOT route at corrected final SHA as well as all four native hosts.

18. Stabilize warning-free managed/focused/full/Release-package checks before the final native gate. Retain normal Core 0.1.8 publication changes and fresh disposable package-consumer verification. Commit/push and update the existing draft PR only as already authorized once stable; attach the PR if newly created. Supervisor receives the full frozen diff/evidence, runs new sequential complete code reviews, owns required CI follow-through, merges, normal publication, installed/published default acceptance and closure. No implementer merge/publication/acceptance/done claim.

## 4. Verification

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
- ONNX numeric score/threshold, missing feature/model, real in-flight termination/join/disposal and no cancelled context write are observed with exact fixture hashes/provenance.

Read-only observations supporting this plan, not corrected-source acceptance:

- Frozen round1 managed/consumer evidence remains at `/private/tmp/phase76-core-primitives-20261009T220732Z`, including EVIDENCE.md and complete.diff. The actual selected Client DLL hash is `cf472b56aec324111750d6f03a7695e5e134a050bcb11c2a51076aac9cda23ca`.
- Canonical old-source run37999080680 failed ForwardsForgeEnvironmentVariables before AOT (1030 passed/10 skipped/1 failed). Its existing log does not expose envelope Reason; do not assert its exact cause. The disposable public-API reproduction at `/private/tmp/phase76-core-r4-probes/fast-stdin-r3.log` observed 30/30 pass with 1 and 16,384-byte values, 30/30 broken-pipe failures with 262,144-byte values. Separate pipe observation was IOException HResult80131620, inner SocketException native32/Shutdown. This establishes a real supported-input defect and concrete mapping; final tests must also diagnose/reproduce the canonical behavior.
- Old-source release run37999080639 completed all four native hosts successfully. Those help/version builds contain neither corrected launch nor the required new lifecycle/PID1 facts.
- `/private/tmp/phase76-posix-launch-probe/probe-r3.log` observed atomic macOS group launch, parent exit while a descendant held pipes, group kill/join, root reap and no late sentinel. `/private/tmp/phase76-posix-r5-probe/probe.log` additionally observed fixed-root WNOHANG cancellation/join, resumed observation of the same unreaped root, descendant pipe closure and sentinel=false. Root getpgid=-1 after exit is not evidence of group absence; the child-reported group matched the held root PID. This remains disposable managed macOS mechanism evidence, not final native or Linux proof.
- `docker version --format '{{.Server.Version}}'` failed locally because `/Users/ameerdeen/.docker/run/docker.sock` is absent. The exact first PID1 verification is the mandatory normal Linux x64 CI command above. No local Linux/Windows or PID1 PASS is claimed.

Core/library evidence does not close later Runner-image/cloud acceptance. No merge/publication until full corrected code reviews and required current-source gates pass; no task closure until normal published/installed default acceptance passes.

## 5. Principles that changed a decision

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

## 6. Open questions and assumptions

No unresolved material design question. ArtifactPaths is the live caller/Runner-owned ConcurrentDictionary reference exposed through IReadOnlyDictionary; the existing frozen implementation already retains it correctly. This revision corrects the plan wording and adds the specified existing-file public-behavior regression, without a new product mechanism. This plan selects atomic POSIX groups with retained root and atomic Windows job association; implementation and native/PID1 verification remain pending; current full plan reviews passed and supervisor approval is recorded above. The Linux adopted-child drain is the supervisor-recorded private Type-2 choice, with the reversal/removal condition stated above. Ordinary descendants are the lifecycle guarantee; code deliberately escaping its group/job, security isolation, permissions and egress policy are outside this task. Remaining inherited authority is explicit.

Current-source verification, exact platform error observations and actual PID1 proof are readiness gates, not assumed PASS results. Unexpected ABI/platform behavior, unavailable required primitive, version collision or required file/scope expansion returns to supervisor. There is no minimum-OS/toolchain workaround in this plan.

Read AGENTS/default-path/code-style, the owning forge-mcl/Core and scripts READMEs, current task/plan/locked parent contracts and the actual Runner entrypoint for the scoped runtime fact. No product or repository documentation edits, branch changes, submissions, deployments, merge/publication or completion claim occurred in this planning stage. Only disposable probes and this authorized scratch plan were written.

Stage start: **2026-10-09 23:10:31 UTC**. Stage end: **2026-10-09 23:11:13 UTC**. The complete six-section plan is saved at `/private/tmp/phase76-core-r6-plan.md` for supervisor transcription.
