# forge-mcl — Phase 2 backlog (redundancy & complexity)

Run: Phase 79 step 7 · repo `forge-mcl` · scope `src/` (Parser `Generated/` excluded) · base `main`
`4f71c88` (v0.44.0) · lenses: `csharp-refactoring`, `crap-score` (complexity gate >15),
`simplicity-reviewer` · baseline: build 0 warnings, tests 655 passed / 3 skipped / 0 failed.

One Discover scan (2026-11-20). Merged and deduplicated; ordered by dependency, then size. Every row
is behaviour-preserving. **(D)** = deletes code with no caller in forge-mcl, forge-runner or
forge-client.

## Carves

| # | Issue | Locations | Lens | Justification |
|---|---|---|---|---|
| 1 ✅ `5c544d0` | One Core atomic-write helper (temp file, replace/move, cleanup; optional Unix mode) for `LockFile` and `CredentialStore` | Core/Resolution/LockFile.cs:45-67; CredentialStore.cs:132-155 | `csharp-refactoring`, `simplicity-reviewer`, `crap-score` | 2 copies of the same file-write logic can drift apart, and a fix to one misses the other. |
| 2 ✅ `0ff32c8` | `OciExpertPuller`: parse the reference once; split `PullVerifiedAsync` into `VerifyCachedEntry` + `WriteCacheAtomic` (MissionRegistry has no Core reference, so its write stays local) | MissionRegistry/OciExpertPuller.cs:8-50 | `csharp-refactoring`, `simplicity-reviewer`, `crap-score` | Complexity 10 at 7% coverage (CRAP 91) mixes cache check and file write in one method, so neither can be read or changed on its own. |
| 3 ✅ `2fa373f` | One `Missions` list and by-name lookup on the AST, used instead of 13 `Declarations.OfType<MissionDeclaration>()` rebuilds and the second `FindMission` | Parser/Ast.cs; MissionValidator.cs:20-53; DurableMissionPackageValidator.cs:28,117,197; PipelineDefinitionFingerprint.cs:19; DurableMissionInputPolicy.cs:12,22; PipelineRunner.cs:791-792; McpCommand.cs:63 | `csharp-refactoring`, `simplicity-reviewer` | The same filter is written 13 times and mission lookup twice; one name says what it means and is the one place to change. |
| 4 ✅ `2fe3bb8` | Inline `RootDefinitionFingerprint` (one-line forwarder, 3 callers) | PipelineRunner.cs:804-805 | `csharp-refactoring`, `simplicity-reviewer` | A method that only forwards adds a hop to read through and does nothing. |
| 5 ✅ `db756b9` | Move the context keys in `PipelineRuntimeInstructions` and `PipelineToolContinuationInstructions` into `ContextKeys` (keep `ChatHistory.ContextKey` as an alias; forge-runner uses it) | ContextKeys.cs; PipelineRuntimeInstructions.cs:9-14; PipelineToolContinuationInstructions.cs:3-12 | `simplicity-reviewer` | `ContextKeys` claims to own all reserved keys but 3 other files define them, so a new key can clash unseen. |
| 6 ✅ `1076b3e` | `PipelineRunner`: bundle the 8–10 per-attempt parameters passed through ~9 private methods into one private record | PipelineRunner.cs:190-201, 273-283, 308-317, 344+, 409+, 462+, 517+, 641+, 956-965 | `csharp-refactoring` | Adding one step-level value today means editing about 9 method signatures. |
| 7 ✅ `0d5f3c7` | Delete the stale comment naming forge-runner's `RunnerToolTurnMapper` (type no longer exists) | PipelineRunner.cs:868-870 | `simplicity-reviewer` | The comment points readers to code that no longer exists. |
| 8 ✅ `e7136bf` | `DurableMissionPackageValidator`: split `ValidateSemantics` (CC 36) and `ValidateAssets` (CC 24) into named steps | DurableMissionPackageValidator.cs:196-233 | `crap-score` | Complexity 36 and 24 in the package admission check, hard to change safely. |
| 9 ✅ `97e57fa` | `MissionValidator.Validate` (CC 18): extract `FindMissingExperts` + `ReportContractIssues`; drop unread `MissionReferenceEdge.FromMission` | MissionValidator.cs:20-64, 126, 256 | `crap-score`, `simplicity-reviewer` | Complexity 18 with two jobs in one method, plus a field that is set and never read. |
| 10 ✅ `d260a81` | `ExpertResolver.Resolve` (CC 24): extract `WarnIfShadowing` + `VerifyLockHash` | ExpertResolver.cs:46-80 | `crap-score` | Complexity 24 mixes lookup, warning and hash check, so the lock-hash rule is hard to find. |
| 11 ✅ `9144a47` | `CapabilityDispatcher.EditFileAsync` (CC 16): pure `ApplyEdit(content, request)` | CapabilityDispatcher.cs:95-129 | `crap-score` | Complexity 16 mixes file I/O with the edit rule; a pure function can be read and tested alone. |
| 12 ✅ `41b5d9e` | Small splits just over the gate (CC 16–18): `ForgeTomlReader.ResolveValue` (`ResolveEnvCall`), `ResolveModelPath`, `ExecExpertRunner.AddWorkspaceEnvironment`, `WorkspaceGuard.RealPath`, `JsonExtractExpertRunner.RunAsync`, `DirectExpertRunner.BuildMessages`, `OciMissionPuller.ExtractBundleAsync` (`AdmitEntry`) | ForgeTomlReader.cs:182-210; DurableMissionPackageValidator.cs:236-248; ExecExpertRunner.cs:112-125; WorkspaceGuard.cs:73-93; JsonExtractExpertRunner.cs:13-46; DirectExpertRunner.cs:121-142; OciMissionPuller.cs:37-62 | `crap-score` | 7 methods over the complexity gate of 15; each gets one named step to bring it under. |
| 13 ✅ `0b8e6f8` | CLI: one `ResolveLockedExperts` in `CliHelpers` for `forge mcp` and `forge validate` | McpCommand.cs:51-58; MissionDevCommands.cs:139-146; CliHelpers.cs:48-53 | `csharp-refactoring`, `simplicity-reviewer` | 2 copies of the same load-and-report block; Phase 1 already saw these catches drift. |
| 14 ✅ `f0fcdf4` | CLI: one factory each for the `<mission>` argument (3 copies) and `--experts` option (2 copies) | MissionDevCommands.cs:21,105,154,174; McpCommand.cs:31-36 | `csharp-refactoring`, `simplicity-reviewer` | 5 copies of the same declarations; a changed default or help text must be made in every one. |
| 15 ✅ `dfde36d` | CLI: one "Not signed in" check; drop its redundant `IsNullOrEmpty(platform.Key)` | ForgeDurableRun.cs:17-22; PlatformLogin.cs:69-74 | `simplicity-reviewer` | 2 copies of the sign-in guard, each with a test the store already does. |
| 16 ✅ `86e4bb3` | `PlatformLogin`: one bearer send/read helper for `FetchMeAsync` and `IssuePlatformKeyAsync` | PlatformLogin.cs:99-147 | `csharp-refactoring` | 2 copies of the same request/status/error/JSON handling, so an error-handling fix must be made twice. |
| 17 ✅ `bb0a5cf` | `McpCommand`: drop the single-caller `buildRunners` seam and the extra `manifest` parameter | McpCommand.cs:66-67, 77-87, 119-126, 140 | `simplicity-reviewer` | A function parameter with one caller and no test use is an extra layer to read through. |
| 18 ✅ `ff3eefa` | One input-name rule: CLI `RunInputs` uses Core's `DurableMissionInputPolicy` | RunInputs.cs:90-91; DurableMissionInputPolicy.cs:37-38 | `csharp-refactoring` | 2 copies of the input-name rule; if one changes, the CLI accepts names the runtime rejects. |
| 19 ✅ `0e398f9` | `ExecExpertRunner.BuildInputJson`: collapse the ternary with two identical arms | ExecExpertRunner.cs:158-176 | `simplicity-reviewer` | A branch whose two arms are the same makes the reader look for a difference that is not there. |
| 20 ✅ `4bbcda7` | **(D)** Unused members: single-root `WorkspaceGuard.TryResolve`, `ExecutionConfig.Backend` (validation stays), the 6-argument `DurableMissionPackageInput` constructor | WorkspaceGuard.cs:14-15; ForgeManifest.cs:28-29; ForgeTomlReader.cs:165-167; DurableMissionPackageValidator.cs:263-271 | `simplicity-reviewer` | 3 members nothing calls still have to be read, kept compiling and kept in the public API. |

### Checks before fixing (results)

- **#18:** passed. Both rules accept the same names (non-empty, ASCII letter first, then ASCII
  letters, digits or `_`); `DurableMissionInputPolicy.IsIdentifier` became public for the CLI.
- **#20:** passed for the constructor and `TryResolve`: no member reference in the published
  Katasec.Forge.Client 0.19.0, Katasec.Forge.Hands or Katasec.Forge.Runner binaries (metadata scan);
  only forge-client's own test build calls the 6-argument constructor, which binds to the primary
  constructor on recompile. `ExecutionConfig.Backend` was **kept**: 3 forge-mcl tests assert it, and
  Phase 2 does not edit tests.

Gate per item: incremental `dotnet build -warnaserror` (0 warnings); `dotnet test -warnaserror` after
each group: 655 passed / 3 skipped / 0 failed. Verify (no rescan): every row checked against the code.

## Not in Phase 2 (recorded verbatim, not scoped here)

- `IWorkspace` + `WorkspaceFileProvider` forwarding layer: removing it needs a forge-client change.
- `DurableMissionAssetInput.Executable`: removing it changes the 4 MiB package-size check.
- Merging the two tool-argument JSON writers: they accept different value types.
- `ReadFrontmatter` vs `SplitFrontmatter`: they differ on a missing opening `---` and `\r`.
- CRAP rows whose fix is "add tests" (CredentialStore, RegistryCredentialStore, WriteValue, RunnerFor): Phase 3.
- `ForgeMission.Cli` has no coverage data (tests run it out of process); no CRAP score for Cli.
