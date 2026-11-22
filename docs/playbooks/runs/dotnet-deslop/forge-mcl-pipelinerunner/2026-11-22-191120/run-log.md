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
