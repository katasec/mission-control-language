# Run log — forge-mcl PipelineRunner

Run: `2026-11-22-191120` · repo `forge-mcl` · scope `src/ForgeMission.Core/Runtime/PipelineRunner.cs` ·
branch `adeen/deslop-pipelinerunner` · base `main` `ca2e762` · baseline: build 0 warnings, tests
673 passed / 3 skipped / 0 failed.

## Phase 1 — structure & idiom

| # | Issue | Owner chosen | Commit | Verify |
|---|---|---|---|---|
| 1 | `when()` guard evaluation in the interpreter | new `WhenGuards` | `f97e895` | build 0 warnings |
| 2 | Tool-call → `PipelineToolCall` conversion in the interpreter | new `PipelineToolCallConversion` | `4f1d875` | build 0 warnings |
| 3 | Continuation identity split across codec and runner | new `PipelineContinuationIdentity` | `ed3b3d7` | build 0 warnings |
| 4 | Dead `FindMission(root)` resume check | `ResumeAsync` | `0c6acac` | tests 673 passed / 3 skipped |
| 5 | Branches cancel siblings through a passed-in CTS | `ExecuteParallelAsync` | `3de8b18` | build 0 warnings |
| 6 | Positional deconstruction of `MissionAttempt` | `PauseAsync` | `1e4de2f` | build 0 warnings |
| 7 | `RunState.Replay` hides state change and throw | `AdvanceReplay` | `9da45a3` | build 0 warnings |
| 8 | `history` vs `ChatHistory` | `transcript` | `9ed51a5` | tests 673 passed / 3 skipped |
| 9 | `"default"` written twice | `DefaultProfile` | `0f20911` | build 0 warnings |
| 10 | Unsealed class | `sealed` | `58308fc` | build 0 warnings |
| 11 | Mutable experts type internally (public kept) | `IReadOnlyDictionary` | `3c58824` | build 0 warnings |
| 12 | Helpers far from callers | reorder in place | `9a56fdd` | build 0 warnings; line set unchanged |
| 13 | Comment wraps, restating comment, using order | in place | `d0d7055` | tests 673 passed / 3 skipped |

No behaviour-change commits.

Phase 1 delivery: forge-mcl PR [#115](https://github.com/katasec/forge-mcl/pull/115) merged as `7ce5b2f`
(merge commit; PR CI 4 builds + test passed, though the merge landed before the Windows build
finished because `main` is unprotected). Release CLI published `v0.49.0`; installed
`~/.local/bin/forge` prints `0.49.0+7ce5b2f…`. Real run: `forge run --verbose ./mission.mcl` in
`~/forge-hands-test` (default endpoint) → conversation `60b87b6c-f05e-5bda-9a50-ddd00d3f4438`:
Write succeeded, out-of-workspace Read refused, summary returned. That run executes on the hosted
runner, which uses its own Core package, so it proves the CLI release, not the changed
`PipelineRunner`; the local suite (673 passed) covers that.

## Phase 2 — redundancy & complexity

| # | Issue | Owner chosen | Commit | Verify |
|---|---|---|---|---|
| 1 | List-shaped `From` for one call | `PipelineToolCallConversion.From(call)` | `e2be8e9` | build 0 warnings |
| 2 | Tool-in-scope rule at pause and resume | `PipelineContinuationIdentity.Declares` | `58b112b` | build 0 warnings |
| 3 | Two fail-reason formatters | `FailReason` | `e2d3707` | build 0 warnings |
| 4 | Sub-mission step written twice | `RunSubMissionAsync` → `SubMissionOutcome` | `1a7bfd2` | tests 673 passed / 3 skipped |
| 5 | `ExecuteParallelAsync` complexity 20 | `MergeBranches` | `9bb83da` | build 0 warnings |
| 6 | One-line forwarder `IsPlainTextLlm` | `StreamsStep` | `b5d628c` | build 0 warnings |
| 7 | `PauseAsync` renaming locals | `attempt.X` | `b865c91` | tests 673 passed / 3 skipped |
| B1 | `CanReachAgent` own recursion, expert-first, no cycle stop | `ReachableSteps`, mission first | `89c8af9` (behaviour change) | tests 673 passed / 3 skipped |
