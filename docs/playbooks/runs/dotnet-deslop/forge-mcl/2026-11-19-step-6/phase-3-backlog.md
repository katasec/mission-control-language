# forge-mcl — Phase 3 backlog (tests)

Run: Phase 79 step 7 · repo `forge-mcl` · scope `tests/ForgeMission.Mcl.Tests` · base `main`
`a621fe1` (v0.45.0) · branch `adeen/phase-79-p3-deslop` · lenses: `test-anti-patterns`,
`test-smell-detection`; fixes with `code-testing` · baseline: build 0 warnings, tests 655 passed /
3 skipped / 0 failed.

One Discover scan (2026-11-20). Merged and deduplicated; ordered by severity, then file. No rescan.
`AP` = `test-anti-patterns`, `TS` = `test-smell-detection`.

## Carves

| # | What's wrong | Fix | Found by | Why fix it | Time |
|---|---|---|---|---|---|
| 1 ✅ `496b67d` | Parallel-steps test relies on two 60 ms sleeps overlapping; counter update is not thread-safe | Barrier (TCS completes when 2nd call enters, 10 s timeout) + `Interlocked` | AP, TS | Fails at random on a slow CI box | 10 min |
| 2 ✅ `f874259` | Exec cancel/timeout test sleeps 2.2 s and hopes the child is dead; one body runs two paths | Poll the child pid until gone (deadline); split into cancel test + timeout test | TS | Can pass when the kill is broken | 15 min |
| 3 ✅ `54f0bae` | Runner env test reads real `MCL_PROVIDER/MODEL/ENDPOINT` from the dev shell and sets `MCL_API_KEY` for the whole process | Unique per-test variable names; restore in `finally` | AP | Result depends on whose machine runs it; leaks into other tests | 10 min |
| 4 ✅ `a1560d4` | ONNX missing-model test accepts any exception, checks no message | Assert the real exception type and the file name in the message; own test | AP | Passes on unrelated crashes | 5 min |
| 5 ✅ `bf87594` | CLI test harness does not clear `MCL_*` variables before starting `forge` | Also drop `MCL_*` keys in `IsolateEnvironment` | AP | Dev shell settings leak into CLI tests (the cause of the 4 McpCommandTests failures with `MCL_API_KEY`) | 5 min |
| 6 ✅ `0f02124` | Resolver test writes to the real `~/.forge` | Internal `home` parameter on `ResolveAll` (like `ToLockPath`); temp home | AP, TS | Touches the developer's real Forge folder | 10 min |
| 7 ✅ `fdd6106` | Paid live-API tests run whenever a key happens to be set | Skip unless `FORGE_RUN_INTEGRATION=1` | AP | Local runs can spend money and hit the network without asking | 5 min |
| 8 ✅ `561b1ac` | Project test calls a private method by reflection and pins assembly version `0.19.0.0` | Make `RunCoreAsync` internal (InternalsVisibleTo); drop or derive the version check | AP | Rename breaks at run time; every client bump breaks the test | 10 min |
| 9 ✅ `92953b1` | Chat-client tests only assert "not null", which can never fail | Assert the provider name from client metadata | AP | Test cannot catch a wrong provider | 5 min |
| 10 ✅ `910b7be` | Chat-client throw test sleeps 200 ms and runs a listener that can never get a request | Remove sleep, listener and its check; keep throw + message asserts | AP, TS | Dead wait and dead check | 3 min |
| 11 ✅ `c451e6f` | TOML test sets `FORGE_TEST_KEY` with no `finally` | Save and restore in `finally` | AP, TS | A failed assert leaves the variable set for the run | 3 min |
| 12 ✅ `1b10b87` | Temp folders leak (TOML reader: 27 made, 4 deleted; resolver: 9 made, 2 deleted) | Classes become `IDisposable`, delete their temp root | AP, TS | Every run leaves junk in temp | 10 min |
| 13 ✅ `a88e0ed` | Runner theory picks its asserts with `if/else` on a parameter | Pass expected/other path as data; no branch | TS | Hard to see what each case checks | 5 min |
| 14 ✅ `a6d28a7` | Package-validator theory runs two different bodies on `withAssets` | Split into two tests | TS | Same | 5 min |
| 15 ✅ `cbe5c64` | Agent-tool theory runs throw path or pause path on `fail` | Split into two tests with shared setup | TS | Same | 5 min |
| 16 ✅ `388d17a` | Agent-tool tests loop over variants inside one test | Move variants to `[InlineData]` / `[MemberData]` | TS | A failure does not say which variant broke | 10 min |
| 17 ✅ `2647a05` | Project test picks expected text with a ternary | Pass expected text as data | TS | Same as 13 | 3 min |
| 18 ✅ `fa3a4d7` | ONNX cancel tests use a fixed 300 ms sleep | Wait for the numeric step-started trace instead | TS | Slow, and timing-dependent | 10 min |
| 19 ✅ `6943ac8` | Four near-copy `OnnxExpert_Missing*` tests | One theory with 4 cases | AP | Same test written 4 times | 5 min |
| 20 ✅ `c53619d` | `Assert.Throws` wraps `.GetAwaiter().GetResult()` | `await Assert.ThrowsAsync` | AP | Blocking on async can deadlock and hides the async path | 2 min |

Times re-estimated with the operator before fixing: about 55 min of carves in all.

### Results

- Every row verified resolved (2026-11-20). Final suite: 673 passed / 3 skipped / 0 failed (more
  cases because #16 turned loops into theory cases).
- **#5** also fixes the known local failure: the full suite now passes with `MCL_API_KEY` set.
- **#8, partly:** the CLI is not a compile reference of the test project (tests drive it as a
  separate program), so the `RunCoreAsync` lookup stays by name; it now throws
  `MissingMethodException` naming the method. Version is derived from forge's own reference.
- **#18, partly:** the pipeline test waits on the trace; the direct-runner test keeps its 300 ms
  wait because `OnnxExpertRunner` has no start signal (commented in the test).

## Not findings (lens notes)

Reasoned `SkippableFact` skips; stub lambdas that branch on expert name; `[Collection]` console
capture; culture swaps already in `try/finally`.
