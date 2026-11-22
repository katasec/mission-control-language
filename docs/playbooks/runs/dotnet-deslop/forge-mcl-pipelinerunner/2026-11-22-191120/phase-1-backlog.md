# forge-mcl PipelineRunner — Phase 1 backlog (structure & idiom)

Run: `2026-11-22-191120` · repo `forge-mcl` · scope `src/ForgeMission.Core/Runtime/PipelineRunner.cs` ·
base `main` `ca2e762` · branch `adeen/deslop-pipelinerunner` · lenses: `csharp-expert` (idiom),
`ownership-reviewer`, `code-style-reviewer` (`dotnet-webapi` not relevant: no endpoints) ·
baseline: build 0 warnings, tests 673 passed / 3 skipped / 0 failed.

One Discover scan. Merged and deduplicated; ordered dependency first (relocations reshape the file,
so they go before reorder and naming). Every row is behaviour-preserving.

## Carves

| # | Issue | Locations | Lenses |
|---|---|---|---|
| 1 ✅ `f97e895` | Move `when()` guard evaluation (`WhenAdmits`, `IsGuarded`, `HasUnmatchedGuards`, `TryParseDouble`, `EvaluateNumericOp`) into its own Runtime class | PipelineRunner.cs:241-261, 881-904 | ownership+idiom+style |
| 2 ✅ `4f1d875` | Move provider tool-call → `PipelineToolCall` conversion (`ToPipelineToolCalls`, `ToolArgumentsToJsonElement`, `WriteToolArgumentValue`) out of the interpreter, beside `PipelineToolCall` | PipelineRunner.cs:827-879 | ownership+idiom |
| 3 ✅ `ed3b3d7` | Move continuation identity (`ScopeFingerprint`, `CompactJson`, `IsResumable`, `ToDeclaration`, `ResumedTurn`, `ToolResultText`) next to `PipelineCheckpointCodec` | PipelineRunner.cs:104-121, 765-798, 1045 | ownership+idiom |
| 4 ✅ `0c6acac` | Drop the dead `ast.FindMission(root) is null` resume check: the definition fingerprint already covers it (so no ordinal is consumed before it) | PipelineRunner.cs:79-83, 119-120 | style+idiom |
| 5 ✅ `3de8b18` | Parallel branches get a token, not the `CancellationTokenSource`; the coordinator cancels siblings on a failing branch | PipelineRunner.cs:337-340, 390-432 | idiom |
| 6 ✅ `1e4de2f` | `PauseAsync`: named `attempt.X` access instead of positional deconstruction | PipelineRunner.cs:616-617 | idiom |
| 7 ✅ `9da45a3` | `RunState.Replay` name hides that it throws and ends replay; rename to say so | PipelineRunner.cs:1002-1013, 497 | style |
| 8 ✅ `9ed51a5` | Rename the negotiation `history` (a `SpeakerTranscript`) so it does not collide with `ChatHistory` | PipelineRunner.cs:137, 148, 171, 183-184 | style |
| 9 ✅ `0f20911` | One constant for the `"default"` runner profile key | PipelineRunner.cs:42, 724 | idiom |
| 10 ✅ `58308fc` | `sealed` on `PipelineRunner` (no subclasses in any Forge repo) | PipelineRunner.cs:19 | idiom |
| 11 ✅ `3c58824` | Take experts as `IReadOnlyDictionary` (never mutated) inside the runner: `MissionAttempt`, `RunCoreAsync`, `ResolveExpert`, `IsResumable`. Public `RunAsync`/`ResumeAsync` keep `Dictionary` (see below) | PipelineRunner.cs:47, 66, 111, 125, 748, 953 | idiom |
| 12 ✅ `9a56fdd` | Reorder: helpers next to their callers, top-down (`ChatInput`/`IsNegotiationEligible`, `CreateChildOptions`, `Halts`/`Failure`, `CanReachAgent`, `InvokeExpertAsync`, `ResolveRunner` below `RunnerFor`) | PipelineRunner.cs:640-684, 712-759, 906-944 | style |
| 13 ✅ `d0d7055` | Comments and tidy: mid-sentence wraps, restating comments, `using` order, double blank line, fully qualified `SHA256`/`Globalization` | PipelineRunner.cs:9, 139-141, 197, 288-290, 502-503, 580-581, 767-769, 890-891 | style+idiom |

Gate per item: incremental `dotnet build -warnaserror` (0 warnings). `dotnet test -warnaserror` after #4, #8 and #13: 673 passed / 3 skipped / 0 failed each time. Verify (no rescan, 2026-11-22): every row checked against the code. #4, #6–#11 and #13 were small enough that the supervisor made them directly instead of through a carve subagent. `PipelineRunner.cs` went from 1061 to 896 lines.

## Not in Phase 1 (recorded verbatim, not scoped here)

- **Local continuation registry** (`_localContinuations`, `ConsumeLocalContinuation`, `RegisterIssuedContinuation`): forge-runner builds a new `PipelineRunner` per run and resume (`GenericDurableMissionExecutor.cs:27,41`), so `DuplicateContinuation`/`LateContinuation` never fire in hosted use and the maps grow unbounded in a long-lived instance. Removing it or moving it to the durable layer is an ownership design decision.
- **Two failure contracts:** some failures return `MissionResult` with `PipelineFailure`, others throw `InvalidOperationException`; callers wrap in try/catch. Picking one is a public-contract design decision.
- **`inParallel` mode flag:** sequence steps stream and wrap errors with the step name; parallel steps do neither. Removing the flag changes one of those behaviours.
- **`ResumeAsync(observers: PipelineRunOptions)`** silently overwrites `MissionName`, `Vars`, `RootTools`, `MissionPath`; forge-runner still fills two of them. A narrower parameter type is a cross-repo API change.
- **Runner construction:** llm runners and `IWebSearch` are injected, the other kinds are `new`-ed per step; `HttpExpertRunner`'s `HttpClient` is unreachable; a missing `IWebSearch` fails at step time. A runner registry is a DI design decision.
- **CLI advice in Core error text** (`forge.toml`, `forge validate`, "Pass one to the PipelineRunner constructor") reaches hosted users. Changing the text changes observable output.
- **`lastResult!` with `loop(0)`:** `MaxLoops == 0` returns null. What `loop(0)` means is a language decision.
- **Tool hand-off through magic context keys** (`object?` response messages): an `IExpertRunner` seam design.
- **Parallel branch writes dropped** (only `{Expert}.output` merges back; same-name branches collide): changes observable behaviour.
- **Numeric types disagree** (`WriteToolArgumentValue` throws on `int`/`float`/`decimal`; guards accept them; `NumberStyles.Any` in guards): changes observable behaviour.
- **Test-only convenience constructor** (`PipelineRunner(IExpertRunner, IWebSearch?)`): removing it means editing tests.
- **`Contains` over a list in `IsResumable`/`RootInputs`:** lists are a handful of names; not worth a carve.
- **Read-only experts on public `RunAsync`/`ResumeAsync`** (#11, partly): changing the public parameter type breaks callers that pass a target-typed `new() { ... }` (e.g. `ExecExpertRunnerTests.cs:102`). That is a source-breaking public API change, so only the internal members changed.
