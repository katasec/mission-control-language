# Phase 76.4 — implementer plan, round 3

**Status:** Complete round 3 plan passed full simplicity and ownership reviews; supervisor
**PLAN APPROVED** 2026-10-09 22:07:32 UTC. Implementation is open.
[Task/design](phase-76.4-core-cloud-primitives.md).
This records the complete proposed implementation and verification; approval is bounded to this Core task.

## Files

All product paths below are relative to `/Users/ameerdeen/progs/forge-mcl`.

| File | Change |
|---|---|
| `src/ForgeMission.Core/Manifest/ForgeManifest.cs` | PackageConfig and default-empty Package |
| `src/ForgeMission.Core/Manifest/ForgeTomlReader.cs` | Distribution selection through existing parser before environment evaluation |
| `src/ForgeMission.Core/Experts/ExpertLoader.cs` | Immutable diagnostic source, no file fallback; mission parameters are string keys |
| `src/ForgeMission.Core/Runtime/DurableMissionPackageValidator.cs` | DTOs, canonical hash, pure construction, common validation, actual JSON size |
| `src/ForgeMission.Core/Runtime/DurableMissionInputPolicy.cs` (new) | One shared exact reserved-name/reachable-name policy |
| `src/ForgeMission.Core/Runtime/PipelineDefinitionFingerprint.cs` (new) | Deterministic full semantic checkpoint identity |
| `src/ForgeMission.Core/Runtime/PipelineExecutionWorkspace.cs` (new) | Runtime workspace and deterministic output-directory calculation |
| `src/ForgeMission.Core/Runtime/PipelineRunOptions.cs` | Optional ExecutionWorkspace |
| `src/ForgeMission.Core/Runtime/PipelineRunner.cs` | Derived admitted inputs, replay, workspace inheritance, StepKey/attempt threading |
| `src/ForgeMission.Core/Runtime/PipelineToolPause.cs` | Admitted names, inner checkpoint 3, unchanged envelope 1, old-format refusal |
| `src/ForgeMission.Core/Runtime/PipelineTraceEvent.cs` | Inherited init StepKey without positional constructor changes |
| `src/ForgeMission.Core/Adapters/ExecExpertRunner.cs` | Workspace process copies and bounded concurrent joined I/O |
| `src/ForgeMission.Core/Adapters/OnnxExpertRunner.cs` | Await owned native work, cancel load/inference, join/dispose |
| `src/ForgeMission.Core/ForgeMission.Core.csproj` | Core 0.1.8; no new dependency |
| `src/ForgeMission.Core/README.md` | Current package/input/workspace/replay/trace/adapter contracts |
| `tests/ForgeMission.Mcl.Tests/Manifest/ForgeTomlReaderTests.cs` | Distribution and full-reader regressions |
| `tests/ForgeMission.Mcl.Tests/Experts/ExpertLoaderTests.cs` | In-memory locations, no fallback, parameter typing |
| `tests/ForgeMission.Mcl.Tests/Runtime/DurableMissionPackageValidatorTests.cs` | Hash/size/path/name/env/kind/profile/model boundaries; single-profile assertions updated |
| `tests/ForgeMission.Mcl.Tests/Runtime/AgentToolPipelineTests.cs` | Admitted inputs, format/semantic refusal, fresh-workspace replay, effects once |
| `tests/ForgeMission.Mcl.Tests/Runtime/PipelineTraceTests.cs` | Exact sequential/nested/loop/parallel/delta/pause keys |
| `tests/ForgeMission.Mcl.Tests/Adapters/ExecExpertRunnerTests.cs` | Real mapping/pipe pressure/limits/cancel/timeout/join/local regression |
| `tests/ForgeMission.Mcl.Tests/Adapters/OnnxExpertRunnerTests.cs` (new) | Real numeric/in-flight cancellation |
| `tests/ForgeMission.Mcl.Tests/Fixtures/onnx/{identity.onnx,cancellable-loop.onnx,README.md}` (new) | Tiny graphs, source/provenance/hash; existing fixture copying |
| `tests/ForgeMission.Mcl.Tests/Cli/ForgeProjectTests.cs` | Actual published Client 0.9.3 Project/chat creation, open and reconnect regression |
| `.github/workflows/publish-core-package.yml` | Existing publication 0.1.8/core-v0.1.8, warning-free managed checks, native facts in current adapter filter |
| `eng/verify-core-package.sh` | Version/contents/provenance/Parser-Scout dependencies |
| `Makefile` | Align existing Core version declaration with 0.1.8 |

## Exact API changes

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
public static bool TryValidateInputNames(ValidatedDurableMissionPackage package,
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
// ExpertLoader.Validate final optional parameter:
IReadOnlyDictionary<string, string>? expertMarkdownByName = null
```

Existing public `ExecExpertRunner(string defaultTimeout = "30s")` stays. An internal constructor
adds workspace/StepKey/attempt to the same adapter; no second local execution implementation.
Workspace output calculation is pure and shared with future Runner collection, with no directory I/O.

The explicit six-argument overload preserves the actual CLR member called by published Client
0.9.3; it delegates to the semantic constructor. `[method: JsonConstructor]` selects that primary
constructor for generated deserialization. No public serialization API/context or custom reader is
added. Package serialization and test/consumer round trips use source-generated metadata explicitly.

## Retained binary and serialization sequence

Before the semantic work below, add source-generated missing/null/no-assets round trips preserving
the old hash, and nonempty-assets round trips preserving bytes, metadata and Executable=true.
Validate every round trip; changed bytes or execution bits fail unless consistently rehashed.

Extend the existing configuration-aware built-CLI loader in ForgeProjectTests, loading the actual
CLI-selected published ForgeMission.Application.dll. Invoke public ApplicationComposition.Create
with existing typed transport DTOs, empty capability policy, event sink and a closed HTTP factory.
Call MissionConversations.CreateChatProjectAsync in a unique disposable directory, then
Projects.OpenChatAsync with ProjectOpenRequest(home, "Chat", DurableConversation), then
MissionConversations.ReconnectAsync with that session. CreateMissionConversation replies with the
exact received launch and deterministic conversation ID; only matching ListMissionConversations
and GetConversation responses are supplied. Unexpected routes fail. Assert successful
creation/open/reconnection, matching identities, portable declaration, no authoring assets or
capabilities and joined disposal. Record selected DLL identity/hash. Do not rebuild Client or add a
sibling reference. Actual retained Client/Contracts/Conversations.Contracts/Hands metadata found
no other changed-member reference justifying a shim.

Current CLI package pins remain Client0.9.3, Client.Contracts0.2.1,
Conversations.Contracts0.7.0 and Hands0.1.0. Future consumer upgrades have separate tasks.

## Reuse and sequence

1. Reconfirm clean intended base/unused 0.1.8; create isolated branch before writing, record times
   and unique verification directory. Read-only baseline clean main
   `8d28dc1ff8f127facfd708b1c89369179e98cf18`; inventory ends at 0.1.7.
2. Reuse existing TOML section/literal/array/multiline parser through a private read mode.
   Distribution values exclude provider/execution/capability rows before ResolveValue and reject
   selected env expressions. Full local TryRead retains behavior and also reads literal assets.
   Distribution result contains expert locators/assets and empty provider configuration.
3. Reuse ExpertLoader.ParseContent/SplitFrontmatter/FindKeyInBlock. Supplied package markdown
   replaces File.Exists/ReadAllLines diagnostics and never falls back to disk. Seed each mission's
   Params as strings without replacing runtime keys; preserve optional-input/binding/nested-call
   diagnostic policies. One internal input policy replaces overlapping package step walking and
   pipeline filtering; exact ordinal reserved names, distinct ordinal-sorted reachable names,
   runtime directory declarations solely runtime-bound. No caller-widenable list.
4. Keep DurableMissionPackageValidator the sole pure public constructor/validator. Select first
   mission/first parameter (empty when parameterless), remove old two-expert/inline restrictions,
   reject unknown kinds/environment AST/reserved bindings and collect actual reachable LLM profiles
   without fixed allowlist/single-profile restriction. Reuse Parser/ExpertLoader/hashing.
   Validate canonical slash paths, hashes, case-insensitive duplicate/staged metadata/expert-file
   collisions and relative ONNX model-to-admitted-asset resolution. Preserve no-assets format-1
   prefix/hash; nonempty assets append locked tagged section. Verify bytes before hashing.
   Source-generated camelCase/null-omitting JSON measures actual 4 MiB UTF-8 cap including
   base64/escaping; cheap early shape guards do not replace that measurement.
5. Replace incomplete record/string fingerprint with explicit length-prefixed semantic encoding:
   AST parameters/bindings/loops/steps/guards/profiles/output declarations and all execution-affecting
   expert fields including typed dictionaries, invariant numbers and ordered dictionaries.
   Exclude spans/ExpertDirectory/workspace. Store/check internally derived admitted names in inner
   checkpoint 3, reject 2, retain envelope 1. Preserve token_count/parameterless expert inputs and
   replay's completed step log; fresh scratch is compatible. Child options inherit workspace.
6. Keep RunnerFor/IExpertRunner; forward existing internal key/attempt and populate every
   start/delta/completed/tool/checkpoint fact. No factory or alternate interpreter.
7. Keep exec literal args, expert cwd, declared-input JSON, outputKey/status/reason/judge behavior
   and timeout parser. Prepare process-local copies: validate absolute workspace root, shared output
   path, convert declared values matching verified artifact keys to absolute scratch paths, then
   supply runtime directory bindings after authored values. JSON remains declared-input-only.
   Replace runtime directory environment values and inherited input aliases with current
   FORGE_WORK_DIR/INPUT_DIR/OUTPUT_DIR and verified FORGE_INPUT_<name>. FORGE_SOURCE_FILE exists
   only for an actual verified declared source_file. Keep remaining inherited authority/environment.
   Start stdin/stdout/stderr concurrently, enforcing UTF-8 caps 4 MiB/4 MiB/64 KiB. Kill ordinary
   process tree and join process/I/O tasks on cancellation/timeout/I/O failure before disposal.
   Caller cancellation propagates; timeout/limit/I/O return failed envelopes. Preserve existing
   malformed JSON/missing outputKey exceptions and nonzero/start-failure behavior. Private named
   preparation/start/bounded-I/O/termination/result steps; no process framework.
8. Keep ONNX 1.27.0 features/tensor/probability/threshold semantics. Await owned native work via
   Task.Run(..., CancellationToken.None) so sibling branches launch. Register native load/inference
   cancellation through observed SessionOptions.SetLoadCancellationFlag/RunOptions.Terminate/
   Run(...,RunOptions); await termination and dispose registration/options/session/results.
   Cancellation-related native failure maps to cancellation; context score writes occur only after
   successful joined work. No early-return cancellation or unobserved task.
9. Extend existing focused suites/fixture copying with tiny native graph source/provenance/SHA.
   Keep restored-package consumer disposable; no new maintained probe project, dependency or
   cloud code. Preserve existing local exec/provider behavior.
10. Stabilize managed checks, then final current-source canonical Native AOT. Update existing
    version/publication/verifier/README owners. Supervisor owns independent reviews, integration,
    normal publication, acceptance and closure.

No in-repo production caller uses removed validated ProviderProfile; validator tests do. Compile
and test all project-reference consumers. Future external consumers remain pinned until reviewed
tasks: Runner 0.1.7; Host and Client Application/Tests 0.1.1; ClientRuntime/Desktop/ForgeUI 0.1.0.
No sibling repository references or consumer upgrades here.

## Verification

| Layer | Required command / observation |
|---|---|
| Focused | `dotnet test tests/ForgeMission.Mcl.Tests/ForgeMission.Mcl.Tests.csproj -c Debug -warnaserror --filter "FullyQualifiedName~DurableMissionPackageValidatorTests\|FullyQualifiedName~ForgeTomlReaderTests\|FullyQualifiedName~ExpertLoaderTests\|FullyQualifiedName~AgentToolPipelineTests\|FullyQualifiedName~PipelineTraceTests\|FullyQualifiedName~ExecExpertRunnerTests\|FullyQualifiedName~OnnxExpertRunnerTests\|FullyQualifiedName~ForgeProjectTests"` (literal filter separators are pipe characters) |
| Full | `dotnet build ForgeMission.slnx -c Debug -warnaserror`; `dotnet test ForgeMission.slnx -c Debug -warnaserror`, no new exclusions/suppression; historical Release reflection-loader caveat recorded |
| Publication slice | `make verify-core-package` plus workflow Release build/tests; explicit Release failures cannot be replaced by Debug |
| Canonical Native AOT | Unchanged `make cli-script-test cli-verify verify-terminal-extensions-package`, macOS-14 PR job; existing four-host release PR matrix additional proof; final source SHA/logs/raw warning counts, no min-OS/build-script workaround |
| Linux native | Regular non-skipped ONNX numeric/cancellation facts in Ubuntu publication adapter slice |
| Metadata | 0.1.8 nupkg contents/README/license/merged provenance/Parser-Scout0.1.0; normal immutable/private-repository checks |
| Fresh API consumer | Unique `/private/tmp/phase76-core-primitives-<UTC>/consumer`, fresh isolated cache, package refs only, retained source/commands; create/validate, arbitrary packaged exec alias/file/runtime dirs, exact keys, numeric/cancelled ONNX; branch package is lower-layer proof |
| Published default | Supervisor independently restores published0.1.8 into another fresh cache using normal feeds/credentials; nupkg SHA/nuspec commit/dependency versions/API results, no sibling DLL/source substitution |
| Retained Client ABI | Actual Client0.9.3 DLL creation/open/reconnection against current Core without recompilation; selected package/DLL identity/hash |
| Installed Project/chat | Supervisor clean merged-main normal `make install`; disposable `forge project create` followed by real piped plain Chat using produced declaration, normal saved login/API route and no endpoint override; verify IDs, assistant reply, terminal completion |
| Installed regression | Supervisor clean merged-main `make install`, RID/RELEASE_TAG/CLI_OUTPUT absent; normal-provider init/run Hands Write→Read, independent bytes/outside sentinel, tool-free run and cancelled-session cleanup; no fake provider/endpoint override |

Required focused observations:

- Distribution: missing manifest null, literal/multiline assets, missing local provider keys do not
  affect distribution, excluded values absent, selected env expressions fail, full local reader preserved.
- Packages: parameterless/nested/parallel/loop/>2 experts/all existing kinds/multiple profiles accepted
  when valid. Reject changed hashes/bytes/execute bits, actual JSON excess including escaping/base64,
  invalid/case-colliding/reserved paths, unknown kinds/env/missing ONNX model asset.
- Inputs: required/duplicate/undeclared/reserved failure, empty literals/token_count accepted,
  optional expert inputs stay optional, named input overrides literal let, unreachable experts do not widen.
- Diagnostics: supplied goal:string accepted; goal:double MCL012 at immutable source location;
  missing supplied source never falls back to disk; local file diagnostics preserved.
- Replay: changed args/timeout/model/endpoint/inputs/typed keys/bindings/admitted names refused;
  fresh workspace accepted, relative inputs/completed writes retained, effects execute once.
- Trace/workspace: repeated/nested/loop/parallel/delta/pause exact keys; runtime absolutes absent
  from root/checkpoint values, opaque authored output unchanged.
- Exec: duplex pipe pressure, exact/over limits, cancellation/timeout/I/O leave no ordinary child
  or continuing sentinel write after return; local cwd/FORGE forwarding/output/status/reason/judge/
  malformed-output regressions preserved.
- ONNX: real numeric score/threshold, missing feature/model explicit; long-running tiny Loop model
  witnesses bounded in-flight termination/join and no cancelled context write. Source/hash/provenance
  recorded. Identity fixture SHA256
  `691a2d6476544d32195258db2e889967b1b25964ba89c209363b40bc9593425a`.

Core/library evidence cannot close later real Runner-image/cloud acceptance.

## Principles and open facts

Implementer rules that changed choices: reuse parser/diagnostics/adapters/publication (1); one
input policy/output path (2); disposable consumer/no cloud or new dependency (3); no factory,
framework or compatibility reader (4); supervisor resolved precise gaps (5); actual process/native,
published/default evidence (6); outline/named small steps/top-down (7–9); joined explicit errors,
early exits and separated effects (10–12); unchanged zero-warning gates (13); real semantic seams
for extraction (14); classic McCabe≤15, prefer≤10 without trivial-wrapper gaming (15).

No unresolved design question. UI/visual gates N/A. Version availability rechecked before
mutation; collision returns to supervisor. Cancellation fixture final bytes/hash and Linux behavior
are future evidence. Implementer does not approve, merge, publish, accept or mark completion.
No product files/branches changed during planning. Round3 start2026-10-09 22:00:20/end22:00:54 UTC (34s). Earlier rounds are recorded in the sibling completion evidence.
