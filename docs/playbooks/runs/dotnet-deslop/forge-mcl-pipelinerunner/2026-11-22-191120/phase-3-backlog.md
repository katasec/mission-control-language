# forge-mcl PipelineRunner — Phase 3 backlog (tests)

Run: `2026-11-22-191120` · repo `forge-mcl` · scope: the pipeline tests
`tests/ForgeMission.Mcl.Tests/Runtime/{PipelineRunnerTests,AgentToolPipelineTests,PipelineStreamingTests,PipelineTraceTests,MissionCompositionTests}.cs`
against `PipelineRunner.cs`, `WhenGuards.cs`, `PipelineToolCallConversion.cs`, `PipelineContinuationIdentity.cs` ·
base `main` `d8598be` (v0.50.0) · branch `adeen/deslop-pipelinerunner-p3` · lenses: `test-anti-patterns`
(AP), `test-smell-detection` (TS), `test-gap-analysis` (GA, static only: no mutation run); fixes with
`code-testing` · baseline: build 0 warnings, tests 673 passed / 3 skipped / 0 failed.

One Discover scan. Merged and deduplicated; ordered by severity, then file. Phase 2 carried three
rows in (`RunnerFor`, `WriteToolArgumentValue`, B1); they are #19, #22 and #24. GA rows are
unverified candidates: a new test that fails on today's code is a real defect, so the carve stops and
records it rather than changing production code.

Scope chosen by the operator (2026-11-22): rows 1–18, then cut to 1–7 after review. Rows 8–25 are not done. Every carve was checked by a temporary src mutation that the corrected test catches and the old test missed; no production defect found. Suite after #7: 676 passed / 3 skipped / 0 failed.

## Carves — fix tests that prove less than they claim

| # | Sev | What's wrong | Fix | Found by |
|---|---|---|---|---|
| 1 ✅ `88d899a` | High | `SubMission_ParentContextDoesNotLeak` cannot fail: `Inner` is the first step, so the parent `output` is still `""` | Put a `Seed` step first that writes `output` and `parent_only`; assert both are absent in the child | AP, TS |
| 2 ✅ `c6bac66` | High | Cancellation tests pass even if the runner ignores the token (the stub checks it itself) | Assert no step ran; add a mid-run cancel (cancel inside step 1, assert one call) | AP |
| 3 ✅ `3143aed` | High | Exception asserts check only `InvalidOperationException` (no when match, unknown profile, step failure, provider failure) | Assert the exact message | AP, TS |
| 4 ✅ `50c58e7` | High | Non-success tool result checked with `Contains(status)` | Assert the exact `ERROR [status]: …` text, including null content and Succeeded-with-null | AP, GA |
| 5 ✅ `23e3d83` | High | Parallel tests assume a fixed call order; `StubExpertRunner.Calls` and `historiesPresent` are unlocked lists written by concurrent branches | Order-independent middle assertion; thread-safe `Calls`; lock `historiesPresent` | AP, TS |
| 6 ✅ `1c67df8` | Med | `MaxLoopsVariable_InjectedEachAttempt` runs one attempt | Fail every attempt; assert five `"5"`s | AP, TS |
| 7 ✅ `2493853` | Med | Assertions that pass on an empty collection (history flags, streaming deltas) | Pin exact contents before `All`/`DoesNotContain` | AP, TS |
| 8 | Med | "changed tool name" case rewrites every `Read` in the payload, so another clause rejects it first | Change only the pending call's name | AP, TS, GA |
| 9 | Med | Weak oracles: two-levels test checks only that experts ran; `SingleStep_…ReturnsOutput` never checks output; routing tests use `Contains` pairs | Assert exact sequences and values; fix the wrong comment | AP |
| 10 | Med | Eager tests (provider failure + cancellation, unsupported + multiple calls, wrong call id + changed definition, old format + admitted set, pause shape + secrecy + resume, two missions in one list) | One test per behaviour; theory for the two missions | AP, TS |

## Carves — add missing tests (gaps)

| # | Sev | Gap (a change no test catches) | Test to add | Found by |
|---|---|---|---|---|
| 11 | High | Continuation integrity: a fresh runner accepts a duplicate; an ordinal ahead of current is accepted; a resume that never reaches the paused agent returns Pass; an extra `rootInputs` key is accepted | One test each, expecting the `PipelineFailure` and no provider call | GA |
| 12 | High | Parallel: in-order mode does not stop after a failing branch; a failing concurrent branch no longer cancels siblings (`"a parallel step was cancelled"`) | One test each | GA, AP |
| 13 | Med | B1 (`89c8af9`): mission-first and the cycle stop in `CanReachAgent` are unpinned | A name that is both a non-agent expert and an agent mission; a self-calling mission in a pausable parallel block | GA (Phase 2 carry) |
| 14 | Med | Sequence-step error wrapping: a non-`InvalidOperationException` escaping raw, or in-flight cancellation being wrapped, both survive | Stub throws `HttpRequestException` → wrapped with inner; stub cancels mid-step → `OperationCanceledException` | GA |
| 15 | Med | Routing: `anyGuardedStepMatched \|=` → `=` survives; numeric operator boundaries; int/long guard values | A guarded → unguarded → `when(else)` test; boundary `InlineData`; int and long cases | GA, AP |
| 16 | Med | `WriteToolArgumentValue` (CRAP 41): no test reads `pause.ToolCall.Arguments` | Pause on a call with string, null, bool, long, double and JsonElement arguments; assert exact JSON; an unsupported type → message | GA, AP (Phase 2 carry) |
| 17 | Med | `NormalizeToolTurn` call-only fallback unpinned | Call-only agent; assert the resumed turn holds the assistant call before its result | GA |
| 18 | Med | `RunnerFor` (CRAP 43): unknown kind uncovered; the rule arm is reached but its result unasserted | Unknown kind → message; a failing rule expert → `[WordCheck]` fail reason | GA, CA (Phase 2 carry) |
| 19 | Low | Small error paths: null reason → `step failed`; non-`AIFunction` root tool; unknown mission; a single-run mission gets no `history` | One test each | GA |

## Carves — tidy (low)

| # | What's wrong | Fix | Found by |
|---|---|---|---|
| 20 | `switch` default arm silently maps unknown theory values | Default arm throws | AP, TS |
| 21 | Magic counts and format versions (`2`, `formatVersion = 2/3`, `callCount <= 2`) | Capture-and-compare; `CheckpointVersion - 1`; count Judge calls only | AP, TS |
| 22 | Fixed env var names (`MCLPHASE46ENVTEST`, `MCLPHASE62DIVERGE`, `MISSING`) | `UniqueEnvName` | AP, TS |
| 23 | Gate `TaskCompletionSource`s with no timeout or `RunContinuationsAsynchronously` | Both, plus release in `finally` | AP, TS |
| 24 | Regex on the serialized checkpoint (`"number":\s*0.9`) | Parse and assert the typed entry | AP, TS |
| 25 | Duplication and naming: `SingleRunnerConstructor_WrapsAsDefault` repeats two tests; misnamed `RejectsUnsupported…`; Respond-only experts copied 9 times; `Expert`/`Experts` helpers in three classes; mixed name styles | Delete the duplicate; rename; shared fixtures; one naming style | AP, TS |

## Not findings (lens notes)

Stub lambdas that branch on expert name; `NonLlmLoop_DoesNotInjectHistory`; the `/bin/sh` skip; the
long commented Janus trace test; `OverlapRunner`; the checkpoint field-damage theory.
