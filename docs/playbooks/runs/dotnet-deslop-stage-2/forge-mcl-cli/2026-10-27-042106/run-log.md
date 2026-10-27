# Run log — Stage 2 · forge-mcl / src/ForgeMission.Cli — run 2026-10-27-042106 (run 5)

Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `dotnet-deslop-stage-2` · Base: `fd50eef`
Stage-1 run: `docs/playbooks/runs/dotnet-deslop/forge-mcl-cli/2026-10-25-084621`
Lenses (Phase 1): DI + HttpClient · configuration + options · logging · cancellation
Playbook changes since run 4: live-test filter; row 0 shared harness with non-forwarding proxy;
in-carve scoped tests + supervisor full gate; one implementer per group; parallel disjoint groups;
lane list + known-Check-failure list in the carve prompt; console logger at Warning.

## Run start

- `{test-filter}` = `Category!=Integration` (live tests: ForgeClaudeLauncherTests, ClaudeCodeTests, CopilotSdkTests,
  WireCaptureTests, DirectExpertRunnerIntegrationTests carry `[Trait("Category", "Integration")]`).
- Baseline: `dotnet build -warnaserror` 0 warnings · `dotnet test --filter "Category!=Integration"`: 1082 passed /
  3 failed / 0 skipped (1085 total), **1m50s** wall-clock.
- Failing at baseline: `PipelineRunnerTests.MissingApiKey_ThrowsClearly`; `TuiColourLiteralTests.Only_RawStdout_writes_to_stdout`;
  `TuiColourLiteralTests.Colour_literals_appear_only_in_ForgeTheme`.
- Note: the filtered suite is the floor for each supervisor Verify (~2 min).

One record appended per carve. Newest at the bottom.

---
## Discovery

- D1: 4 lenses in parallel, ~8 min wall-clock (cancellation slowest, 8m17s). 32 findings.
- D2–D4: 20 rows + row 0; 6 merged; 9 routed; Q1, Q2 recorded with defaults (non-blocking).
- Groups: A(1–6) → {B(7–11) → C(12–13)} ‖ D(14–17) → E(18–20). D runs in a parallel worktree.

---
Phase:        1 · row 0
Issue:        Shared test harness (runner, isolated HOME, stubs, non-forwarding proxy, credential scrubbing)
Commits:      a0ef9fe
Behavior:     retained (tests only)
Verify:       build 0 warnings · 1086 passed · 3 failed = baseline · pinned unchanged
Check:        FAIL (third runner copy; old runners bypassed proxy) → corrected (migrated both) → PASS
Duration:     ~10:30 (carve 4:28 · check 2:18 · correction 1:59 · re-check 1:02 · verify ~1:45)
Rating:       5/5 — guard now covers every CLI process test

---
Phase:        1 · row 1
Issue:        No host; four throwaway ServiceCollections; no logging pipeline
Practice:     Host.CreateApplicationBuilder(Args = []) root; console → stderr, Warning min
Commits:      79202c3
Behavior:     platform: appsettings*.json + env config sources, default providers, flush on dispose
Verify:       build 0 warnings · 1087 passed · 3 failed = baseline · pinned unchanged
Check:        PASS (first time)
Duration:     ~8:30 (carve 4:49 · verify 1:36 ‖ check ~2:30)   [run 4: ~20:00]
Rating:       5/5

---
Phase:        1 · row 2
Issue:        config.json via hand-rolled reader (Services/ForgeConfig.cs)
Practice:     AddJsonFile on host + AddOptions<ForgeSettings>().Bind().PostConfigure
Commits:      b7bb32e
Behavior:     platform (Q1, Q3 defaults): fail-fast at host build; case-insensitive keys; env/appsettings layering; read once
Verify:       build 0 warnings · 1089 passed · 3 failed = baseline · pins unchanged
Check:        FAIL (near-copy test; stale README link) → corrected → PASS
Duration:     ~9:00 (carve 5:24 · check ~1:30 · correction 0:33 · re-check 0:16 · verify ~1:40)   [run 4: 18:39]
Rating:       4/5 — correct; env-layering semantics open as Q3

---
Phase:        1 · row 3
Issue:        Theme check hard-coded in a static
Practice:     IValidateOptions<ForgeSettings> (hand-written; generator can't give the message); ForgeApiSettings split
Commits:      7ff4f3b
Behavior:     retained (pin: unknown theme doesn't stop forge run; exact chat message)
Verify:       build 0 warnings · 1089 passed · 3 failed = baseline · pins unchanged
Check:        FAIL (validator only tested via reflection) → corrected (process test) → PASS
Duration:     ~5:30 (carve 2:15 · check ~1:30 · correction 0:31 · re-check 0:14 · verify ~1:45)   [run 4: 8:35]
Rating:       5/5

---
Phase:        1 · row 4
Issue:        Endpoint via mutable static bridge / per-access reads
Practice:     IOptions<ForgeApiSettings> injected from host.Services
Commits:      5be62ac
Behavior:     retained
Verify:       build 0 warnings · full suite = baseline (3) · pins unchanged; post-Check pin tightening (test-only) verified scoped (ForgeConfigTests 12/12) — next row's full Verify covers it
Check:        PASS (first time); reviewer's pin-precision note applied before commit
Duration:     ~7:30 (carve 3:29 · verify ‖ check ~2:00 · pin tweak 0:20)   [run 4: 10:46 + 4:23]
Rating:       5/5

---
Phase:        1 · row 5
Issue:        Static CliHttpClients locator with its own container
Practice:     AddHttpClient named clients on the host; IHttpClientFactory injected
Commits:      ad820b6
Behavior:     platform: shared handler pool + logging pipeline
Verify:       build 0 warnings · 1095 passed · 3 failed = baseline · pins unchanged
Check:        PASS (first time)
Safety:       pin with XAI_API_KEY=xai-test-key → CONNECT api.x.ai refused by the non-forwarding proxy; key never left the machine
Duration:     ~7:00 (carve 5:42 · verify ‖ check ~1:50)   [run 4: 11:18 + 6:19]
Rating:       5/5

---
Phase:        1 · row 6
Issue:        conversation-host registered twice in throwaway containers
Practice:     one AddHttpClient(name, (sp, c) => …) on the host
Commits:      1625cd7
Behavior:     platform (Q4 default): key read at client creation — race-only difference, recorded not pinned
Verify:       build 0 warnings · 1095 passed · 3 failed = baseline · pins unchanged
Check:        PASS (first time)
Duration:     ~8:00 (STOP + supervisor decision ~2:30 · carve 3:12 · verify ‖ check ~1:45)   [run 4: 8:24 + 4:37]
Rating:       5/5

Group A complete (rows 1–6): ~45 min total.

---
Phase:        1 · row 14 (Group D, worktree stage2-run5-groupD)
Issue:        Catches treat any cancellation as timeout/transport failure
Practice:     exception filters on the caller's token
Commits:      5b1bc15 (worktree branch; cherry-picked before Group E)
Behavior:     retained (callers pass default); platform: caller OCE propagates
Verify:       build 0 warnings · 1097 passed · 3 failed = baseline
Check:        PASS (first time)
Note:         swap before pin (signature change) → P6
Duration:     ~6:30 (carve 2:58 · verify ‖ check ~2:00) — parallel with row 7
Rating:       4/5 — correct; two of three filters untested (need row 18's token)

---
Phase:        1 · row 7 (Group B)
Issue:        Login settings via ad-hoc env reads
Practice:     PlatformLoginOptions + [ConfigurationKeyName], Bind, PostConfigure
Commits:      d14d0cf
Behavior:     platform: read once; case-insensitive; other sources (Q3)
Verify:       build 0 warnings · 1098 passed · 3 failed = baseline · pins unchanged (pinned on HEAD first)
Check:        PASS (first time)
Safety:       harness now neutralises browser launch (no-op open/xdg-open on PATH)
Duration:     ~7:00 (carve 4:30 · verify ‖ check ~2:00) — parallel with row 14
Rating:       5/5

---
Phase:        1 · row 15 (Group D)
Issue:        Download timeout CTS not linked to caller token
Practice:     CreateLinkedTokenSource + CancelAfter
Commits:      05da88e (worktree branch)
Behavior:     retained (caller token default until row 18)
Verify:       build 0 warnings · 1097 passed · 3 failed = baseline · pin red→green on new path, green on old
Check:        PASS (first time)
Duration:     ~3:30 (carve 1:21 · verify ‖ check ~1:45)
Rating:       5/5

---
Phase:        1 · row 16 (Group D)
Issue:        Line-mode chat installs its own Console.CancelKeyPress per turn
Behavior:     STOP: "Row 16 cannot be adopted before row 18: ChatAsync's per-turn CancelKeyPress → CTS is the only thing that cancels a line-mode turn today … System.CommandLine then cancels a token nobody observes and, after its default 2 s ProcessTerminationTimeout, ends the invocation with 130. The app would lose its own behaviour: the hands in-flight cancel, the server-side conversations.CancelAsync for the turn, and the 'Cancelled.' line. … Fold this row into row 18."
Resolution:   folded into row 18 (sequencing — P7)
Duration:     ~2:00 (analysis only, no code)

---
Phase:        1 · rows 8, 9, 10 (Group B, one batched carve, one commit each)
Issues:       8 web-search key ad-hoc env · 9 long-held client never rotates · 10 health probe new HttpClient()
Practice:     8 WebSearchOptions via Configure<IConfiguration> · 9 UseSocketsHttpHandler(PooledConnectionLifetime 2m)+SetHandlerLifetime(Infinite) · 10 forge-health named client
Commits:      0b56451 · cbf27f4 · 7c3ada2
Behavior:     platform: 8 read once + other sources (Q3); 9 connection recycling; 10 pooled handler
Verify:       build 0 warnings · 1100 passed · 3 failed = baseline (verified on the combined tree; per-row trees reproduced from snapshots, final tree identical)
Check:        PASS ×3 (first time; row 10 "thinnest — consistency, not a fix")
Duration:     ~5:00 for all three (carve 2:49 · verify ‖ check ~1:45)
Rating:       8: 5/5 · 9: 5/5 · 10: 4/5 (consistency only)
Note:         supervisor commit-message slip (heredoc chained with &&) → recommitted with titles; tree unchanged

---
Supervisor note (05:45): at the operator's request every run-5 commit was recreated without the
"Co-Authored-By: Claude" trailer (trees unchanged). Shas above are pre-rewrite; current branch
history is authoritative. Pre-rewrite refs kept locally: run5-pre-trailer-strip, run5-groupD-pre-trailer-strip.

---
Phase:        1 · row 17 (Group D)
Issue:        TUI session CTS not linked to the command token
Practice:     CreateLinkedTokenSource(cancellationToken), threaded from ForgeChat.RunAsync
Commits:      abd5ec5 (worktree branch)
Behavior:     retained (additive; caller token default until row 18)
Verify:       build 0 warnings · 1097 passed · 3 failed = baseline
Check:        PASS (first time)
Duration:     ~6:00 (carve 3:31 · verify ‖ check ~1:45)
Rating:       4/5 — correct; no test of the new path until row 18 (needs real terminal)

Group D complete (14, 15, 17; 16 folded into 18).

---
Phase:        1 · row 11 (Group B)
Issue:        Aux-client build failure swallowed silently
Practice:     [LoggerMessage] Warning on host ILogger<Program>
Commits:      9868a93
Behavior:     platform: one Warning on stderr (logging exception); stdout/exit unchanged
Verify:       build 0 warnings · 1101 passed · 3 failed = baseline · pin green on HEAD
Check:        PASS (first time)
Duration:     ~8:00 (carve 4:02 · verify ‖ check ~1:45)
Rating:       5/5

Group B complete (7–11).

---
Merge:        Group D (14, 15, 17) cherry-picked onto dotnet-deslop-stage-2 after row 11 → 0f4cfdf, b9ca5af, a88ebfc; no conflicts.
Verify:       combined tree build 0 warnings · 1103 passed · 3 failed = baseline (05:47).
Parallel:     B (7–11, ~19 min) ‖ D (14–17, ~19 min) ran concurrently — wall-clock saved ≈ 19 min.

---
Phase:        1 · row 12 (Group C)
Issue:        MCP stdio server on a hand-built container with RunAsync(None)
Practice:     (as carved) McpServer.Create + RunAsync(action token) — Check rejected a second generic host (P8)
Commits:      a812b65
Behavior:     platform: signal cancels via SCL token; signal exit code is a race (recorded)
Verify:       build 0 warnings · 1103 passed · 3 failed = baseline · pins unchanged
Check:        FAIL (second host, re-registered services, mcp-only special case) → corrected → PASS
Duration:     ~10:30 (carve 5:25 · check ~1:30 · correction 1:31 · re-check 0:16 · verify ~1:50)
Rating:       5/5 after correction

---
Phase:        1 · row 13 (Group C)
Issue:        MCP tool-call handler ignores request token; catch turns cancel into error
Practice:     pass ct to PipelineRunner.RunAsync; when (!ct.IsCancellationRequested)
Commits:      cc32422
Behavior:     platform: cancelled request gets no response (protocol)
Verify:       build 0 warnings · 1103 passed · 3 failed = baseline · pins unchanged
Check:        PASS (first time)
Duration:     ~4:30 (carve 1:24 · verify ‖ check ~1:50)
Rating:       4/5 — correct; client-cancel path untested (timing)

Group C complete (12–13).

---
Phase:        1 · row 18 (+ folded 16) (Group E)
Issue:        Actions use token-less overload; CancellationToken.None; per-turn CancelKeyPress
Practice:     SetAction((parseResult, ct) => …) via SetCancellableAction (OCE on own token → 130); token threaded
Commits:      98e57d6
Behavior:     retained Ctrl+C 130 (pinned ×3 via real SIGINT); platform (Q5): SIGTERM 130 (was 143), TUI external signal exit 0; STOP (partial): project create keeps None → R11
Verify:       build 0 warnings · 1106 passed · 3 failed = baseline · pins unchanged (130 forced at 2 s on HEAD → 130 in ~40 ms)
Check:        PASS (first time)
Duration:     ~19:30 (carve 16:22 · verify ‖ check ~2:00)
Rating:       4/5 — correct and well pinned; turn-cancel path itself unpinned; SIGTERM code changed (Q5)

---
Phase:        1 · rows 19, 20 (Group E, one batched carve, one commit each)
Issues:       19 login callback WhenAny/Delay timeout · 20 serve/connect vscode two signal owners
Practice:     19 linked CTS + CancelAfter + WaitAsync · 20 app.RunAsync(cancellationToken) via IHost extension
Commits:      3c6d395 · 0be6a51
Behavior:     19 platform: Ctrl+C exits 130 at once (pinned) · 20 platform: shutdown from command token; STOP (partial): ConsoleLifetime in ForgeMission.Serve still listens → R14
Verify:       build 0 warnings · 1109 passed · 3 failed = baseline (combined tree; row-19 tree built alone 0 warnings; final tree == verified)
Check:        PASS ×2 (first time; row 20 "partial fix, as stated")
Duration:     ~9:30 for both (carve 4:34 · verify ‖ check ~2:00 · split ~3:00)
Rating:       19: 5/5 · 20: 4/5 (second listener remains, out of scope)

Phase 1 backlog complete: rows 0–20 (16 folded into 18). Final gate next.

---

## Final gate: live integration tests (operator approved, run once, 14:11–14:16)
`dotnet test --filter "Category=Integration"` on `0be6a51`: 1 passed / 3 failed / 6 skipped (5 m 4 s).
- Skipped (credentials not set): Grok ×2, DirectExpertRunner ×2, CopilotSdk, ClaudeCode_LiveRoundTrip.
- Failed ×3 (`ClaudeCode_MultiToolTask…`, `ClaudeCode_TwoTurn…`, `ForgeClaude_OneShotPrompt…`): same cause in each. The mission step got `HTTP 401 invalid_api_key` from the OpenAI API, because the key it sent was the claude CLI's `sk-ant-o…` token. These are pre-existing: Stage 1's baseline (2026-10-25) recorded the same 401/500 live failures, and run 5 changed no file outside `src/ForgeMission.Cli` (Serve/Core/ChatClients unchanged since `fd50eef`). Routed as R16.
