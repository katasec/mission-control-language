# forge-mcl PipelineRunner — Phase 2 backlog (redundancy & complexity)

Run: `2026-11-22-191120` · repo `forge-mcl` · scope `src/ForgeMission.Core/Runtime/PipelineRunner.cs` plus the
files Phase 1 split out of it (`WhenGuards.cs`, `PipelineToolCallConversion.cs`,
`PipelineContinuationIdentity.cs`) · base `main` `7ce5b2f` · branch `adeen/deslop-pipelinerunner-p2` · lenses:
`find-duplicate-methods`, `coverage-analysis` (complexity gate >15, CRAP >30), `simplicity-reviewer` ·
baseline: build 0 warnings, tests 673 passed / 3 skipped / 0 failed.

One Discover scan. Merged and deduplicated; ordered smallest-blast-radius first. `DM` =
`find-duplicate-methods`, `CA` = `coverage-analysis`, `SR` = `simplicity-reviewer`.

## Carves

| # | Issue | Locations | Lenses |
|---|---|---|---|
| 1 ✅ `e2be8e9` | `PipelineToolCallConversion.From` takes a list, but its one caller checks `Count != 1` then `.Single()`: take one call | PipelineToolCallConversion.cs:14-18; PipelineRunner.cs:664-665 | SR |
| 2 ✅ `58b112b` | The "tool name is in the declared scope" rule is written at pause and at resume: one helper | PipelineRunner.cs:666; PipelineContinuationIdentity.cs:28 | DM |
| 3 ✅ `e2d3707` | `StepFailReason` and `SubMissionFailReason` build the same `[Name] reason ?? default` string: one formatter | PipelineRunner.cs:467-473 | DM |
| 4 ✅ `1a7bfd2` | The sub-mission step (run, `Halts`, fail reason) is written for sequence and for parallel: one helper | PipelineRunner.cs:279-291 vs 394-399 | DM+SR |
| 5 ✅ `9bb83da` | `ExecuteParallelAsync` complexity 20 (gate 15): extract the result merge (halt check, outputs, fail reason) | PipelineRunner.cs:317-345 | CA |
| 6 ✅ `b5d628c` (partly) | One-line forwarders with one caller: inline `IsPlainTextLlm` into `StreamsStep` (the README's one streaming helper) and `ChildPrefix` | PipelineRunner.cs:615-619, 690-693 | SR |
| 7 ✅ `b865c91` (partly) | `PauseAsync` locals that only rename `attempt.X` (added by Phase 1 #6); `InvokeExpertAsync`'s `expertName` duplicates `step.ExpertName` | PipelineRunner.cs:657-661, 589-596 | SR |

### Results

- Gate per item: incremental `dotnet build -warnaserror` (0 warnings). `dotnet test -warnaserror` after #4, #7 and B1: 673 passed / 3 skipped / 0 failed each time. Verify (no rescan): every row checked against the code.
- **#6, partly:** `IsPlainTextLlm` inlined into `StreamsStep`. `ChildPrefix` kept: with `StepKey` beside it, it is the one place that defines the step-key format.
- **#7, partly:** `PauseAsync` locals dropped. `InvokeExpertAsync`'s `expertName` kept: passing `step` swaps one parameter for another, and `expert.Name` need not equal the step name.
- **#5:** complexity not re-measured after the split.
- #1–#3, #5–#7 and B1 made by the supervisor directly; #4 by a carve subagent.

## Behaviour changes (own commit each, flagged)

| # | Issue | Locations | Lenses |
|---|---|---|---|
| B1 ✅ `89c8af9` | `CanReachAgent` re-walks sub-missions with no visited set (a mission cycle recurses forever) and checks expert-before-mission, the opposite of how `ExecuteParallelStepAsync` runs a step. Reuse `DurableMissionInputPolicy.ReachableSteps` with mission-first. Differs only for a cyclic mission or a name that is both an expert and a mission | PipelineRunner.cs:347-351; DurableMissionInputPolicy.cs:20-35 | DM+SR |

## For Phase 3 (add tests)

- `RunnerFor`: CRAP 43.25 (complexity 34 is the compiler's string-switch table; 3 of 15 lines uncovered, likely the rule, onnx and unknown-kind arms). (CA)
- `WriteToolArgumentValue`: CRAP 41.31, 41% covered; send `null`, `bool`, `long`, `double` and an unsupported type through the conversion. (CA)

## Not in Phase 2 (recorded verbatim, not scoped here)

- **Tool-scope fingerprint compares the checkpoint with itself** (`IsResumable` hashes `checkpoint.ToolDeclarations` against `checkpoint.RootToolScopeFingerprint`; the comment says it checks the runner's composition). Removing the field changes the checkpoint format. (SR)
- **Stored `AdmittedInputNames`** is derivable from the definition fingerprint. Removing it changes the checkpoint format. (SR)
- **Hand-written tool-argument serializer** vs `AIJsonUtilities` (used by the checkpoint): switching accepts more value types. Same family as the Phase 1 numeric-types item. (SR)
- **`NormalizeToolTurn` call-only fallback** serves only test fakes; removing it means editing those fakes. (SR)
- **Context values to text in two cultures**: `value?.ToString()` (current culture) in guards, feedback, output and templates vs `Convert.ToString(…, InvariantCulture)` in bindings. Unifying changes output on comma-decimal cultures. (DM)
- **Numeric comparison written twice** (`when()` guards: double, 1e-10 equality, no `!=`; rules: int, exact, `!=`, throws). Two language surfaces; unifying changes one. (DM)
- **SHA-256 fingerprints in five places, four formats**; `ScopeFingerprint`'s `\u001f`/`\n` join is ambiguous where `PipelineDefinitionFingerprint` length-prefixes. Changing either invalidates stored values. (DM)
- **Tool-turn messages removed by both `DirectExpertRunner` and `DetachTools`**: choosing the owner changes when the key leaves the context during a run. (DM)
- **Sequence vs parallel expert step written twice** (streaming, error wrapping, `RecordHistory`): rides on the Phase 1 `inParallel` item. (DM+SR)
- **Compact-JSON buffer helpers in Core and ChatClients**: they return different shapes (string vs element); not worth a shared helper. (DM)
- `RunCoreAsync` complexity 16 is compiler async code at 100% coverage: no change. (CA)

## Also for Phase 3

- B1 has no test of its own: a parallel block whose branch is a name that is both an expert and a mission, and a self-calling mission, under a pausable run.
