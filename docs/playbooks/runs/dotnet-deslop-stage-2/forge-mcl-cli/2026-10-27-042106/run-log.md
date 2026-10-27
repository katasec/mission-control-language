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

---

# Phase 2: App-type idioms & project shape (started 15:06, playbook at hub `d28d487`)
- Base `0be6a51` (end of Phase 1). Row 0 harness already in place from Phase 1.
- Baseline: build 0 warnings · `Category!=Integration`: 1109 passed / 3 failed (`TuiColourLiteralTests` ×2, `PipelineRunnerTests.MissingApiKey_ThrowsClearly`, same as Phase 1) · 15:06–15:08.
- Lenses (parallel, read-only): project layout + build config · CLI/endpoint idioms · serialization · data access · trim/AOT.
- The R16 fix runs in a separate session and worktree (`forge-mcl-fix-key-leak`), so it doesn't touch this branch.

Phase:        2 · row 1
Issue:        TryReadFile takes no token; catch-all turns cancellation into Die
Practice:     token → File.ReadAllTextAsync; OCE excluded from catch; init/validate/mcp thread action tokens
Reference:    SCL how-to-parse-and-invoke (Asynchronous actions)
Commits:      c85aa90
Behavior:     platform: Ctrl+C during the read → 130 (race, not pinned)
Verify:       build 0 warnings · 1112 passed / 3 baseline · pin MissionReadFailureTests (new, 3 cases)
Check:        PASS
Duration:     ~7 min (15:17–15:18)

Phase:        2 · row 2
Issue:        TryBuildMissionServerAsync + connect seam take no token
Practice:     token param on both; action tokens passed from serve/claude/connect vscode
Commits:      f928397
Behavior:     platform: Ctrl+C during the read → 130 (race, not pinned)
Verify:       build 0 warnings · 1112 passed / 3 baseline · pins ForgeServeTests unchanged (claude path needs the real CLI: not pinned)
Check:        PASS
Duration:     ~6 min (to 15:21)

Phase:        2 · row 3
Issue:        Health poll hand-rolls a DateTime deadline, ignores the token (R7)
Practice:     linked CTS + CancelAfter(60 s); token to SendAsync/Task.Delay; expiry → false
Commits:      1b429fa
Behavior:     platform: Ctrl+C cancels the poll; in-flight probe cancelled at the deadline (race)
Verify:       build 0 warnings · 1113 passed / 3 baseline · new pin (print-env healthy path; 60 s give-up not pinned)
Check:        PASS (2 optional nits not taken)
Duration:     ~8 min (to 15:26)

Phase:        2 · row 14 (lane 2, branch stage2-p2-lane2)
Issue:        forge exe packable by default
Practice:     IsPackable=false
Commits:      08dd943 (lane 2; cherry-picked at merge)
Behavior:     platform: dotnet pack skips the project (was NU5026 failure)
Verify:       build 0 warnings · 1109 passed / 3 baseline · AOT publish green · pin: property true → false
Check:        PASS (inline: one property + comment; no subagent for a trivial row)
Duration:     ~11 min (publish-dominated)

Phase:        2 · row 4
Issue:        --print-env hand-rolls a Ctrl+C wait (R6)
Practice:     Task.Delay(Infinite, actionToken) + ConfigureAwaitOptions.SuppressThrowing; WaitForCtrlCAsync deleted
Commits:      8decff2
Behavior:     platform: SIGTERM ends --print-env like Ctrl+C (exit 0, teardown runs; was SCL forced exit). Ctrl+C exit 0 pinned, unchanged
Verify:       build 0 warnings · 1113 passed / 3 baseline · pin extended (exit 0 after SIGINT, run on HEAD first)
Check:        PASS
Duration:     ~6 min (to 15:29)

Phase:        2 · rows 5, 7, 8 (trivial, batched in one agent turn; committed separately by hunk)
Commits:      713ed09 (5: claude server DisposeAsync) · 6f55190 (7: list experts non-async + ListExpertsTests) · 2235bdf (8: provider-name CompletionSources + test)
Behavior:     5 none · 7 none · 8 platform: tab completion
Verify:       build 0 warnings · 1116 passed / 3 baseline
Check:        PASS ×3 (nit not taken: the scaffold description lists provider names; changing it changes help output)
Rule slip:    3 offline probes run outside the harness (no network, no credentials) → P9
Duration:     ~10 min for the three (to 15:34)

Phase:        2 · row 6
Issue:        Port-in-use detected by message text
Practice:     catch IOException when inner is AddressInUseException (serve, connect vscode)
Commits:      d027b1b
Behavior:     none (a localized bind message is now still recognized)
Verify:       build 0 warnings · 1118 passed / 3 baseline · new pin (taken port → exit 1 + message), run on HEAD first
Check:        PASS (comment nit fixed by the supervisor)
Duration:     ~8 min (to 15:38)

Phase:        2 · row 9
Issue:        Defaults hand-rolled with ?? in actions; help shows none
Practice:     DefaultValueFactory on 9 options/arguments; ResolveMission (now a no-op) deleted
Commits:      a1a5adf
Behavior:     platform: help shows [default: …]. --experts kept (rule beats the row: its relative default is printed to stdout)
Verify:       build 0 warnings · 1125 passed / 3 baseline · pin DefaultPathTests (7 rows), run on HEAD first
Check:        FAIL (ResolveMission left as a no-op) → corrected → PASS (inline re-check). Stale mirror test MissionFileResolutionTests → R35 (Phase 3)
Duration:     ~14 min (to 15:46)

Phase:        2 · row 10
Issue:        Deliberate exits travel as ForgeExitException instead of the action's int
Behavior:     STOP: "Converting Die/ForgeExitException to int returns changes the app's own output on two paths (MCP per-call BuildRunners failure text; expert diagnostics stop at the first error). Keeping it identical needs a second, throwing error path." Proposed split: (1) agent/webui (2) leaf commands + sentinel TryParse/TryLoadExperts + stop flag (3) init/EnsureInitializedAsync (4) serve/claude/connect (5) mcp, which needs an author decision (6) delete ForgeExitException.
Routed:       R36 (row), R37 (MCP per-call failure), R38 (validate exit code, unverified)
Probe:        one harness probe (temporary test, deleted); tree clean
Duration:     ~9 min (to 15:48)

Phase:        2 · row 11
Issue:        Login HTTP calls take no token (R13, R15); whoami discards it
Practice:     action token → SendAsync/PostAsync/ReadAsStringAsync (CA2016)
Commits:      785d26b
Behavior:     platform: Ctrl+C aborts the request at once (130, same output; timing only)
Verify:       build 0 warnings · 1128 passed / 3 baseline · pins: whoami row + login Ctrl+C theory (3 wait points), run on HEAD first
Check:        PASS
Duration:     ~8 min (to 15:54)

Phase:        2 · row 15 (lane 2)
Issue:        Native linker args via a hand-written target on ILC-internal LinkNative, host-OS gated; machine-specific args
Practice:     LinkerArg ItemGroup on $(RuntimeIdentifier.StartsWith('osx')); each arg proved by a publish without it → kept -L/opt/homebrew/lib only
Commits:      2555f65 (lane 2)
Behavior:     platform: args follow the target RID, not the host OS
Verify:       build 0 warnings · 1109 passed / 3 baseline · AOT publish green · published binary --version/--help identical; otool -L unchanged
Check:        PASS (inline: csproj −10/+4)
Note:         the published binary was run by hand in an isolated env (empty env, scratch HOME, dead proxy). The harness runs the dll, not the AOT binary → P9
Routed:       R39
Duration:     ~28 min (5 AOT publishes)

Phase:        2 · row 12
Issue:        Success body buffered as a string, then deserialized
Practice:     ReadAsStringAsync on failure only; ReadFromJsonAsync(JsonTypeInfo, ct) on success
Commits:      95650b0
Behavior:     platform: strict UTF-8 on success bodies
Verify:       build 0 warnings · 1132 passed / 3 baseline · pins: exact whoami stdout + 4-row bad-answer theory, run on HEAD first
Check:        PASS
Duration:     ~9 min (to 15:58)

Phase:        2 · row 16 (lane 2)
Issue:        P/Invoke via DllImport
Practice:     [LibraryImport] on a partial class
Commits:      7aa1b00 (lane 2)
Behavior:     none
Verify:       build 0 warnings · 1110 passed / 3 baseline · pin TypeAheadTests (new), run on HEAD first · publish deferred to row 17 (P10)
Check:        PASS (inline: 4-line swap + comment; the test reaches Discard via the repo's existing ForgeText.Type helper)
Duration:     ~8 min

Phase:        2 · row 13
Issue:        settings.json rewrite truncates before writing
Practice:     temp file in .vscode/ + File.Move(overwrite: true) (row said File.Replace; the Check's smaller form needs no exists-branch)
Commits:      b4b8bb7
Behavior:     platform: rename-over (default perms, a symlink becomes a file, the dir must be writable); JSON bytes unchanged
Verify:       build 0 warnings · 1134 passed / 3 baseline · new pin (2 rows), run on HEAD first
Check:        FAIL (two helpers + exists-branch) → corrected (one File.Move block) → PASS (inline)
Duration:     ~14 min (to 16:05)

Lane 1 is finished (rows 1–13: 12 committed, row 10 routed). Group E (rows 18–21) moves from lane 2 to the main checkout, which is now idle. Lane 2 keeps Group D (row 17). Their file sets are disjoint.

Phase:        2 · row 18
Issue:        MCP schema hand-built with StringBuilder; EscapeJson incomplete; JsonDocument undisposed
Practice:     Utf8JsonWriter → ArrayBufferWriter; using JsonDocument + Clone
Commits:      1da994e
Behavior:     platform: full escaping; bytes unchanged
Verify:       build 0 warnings · 1135 passed / 3 baseline · pin (exact inputSchema), run on HEAD first
Check:        PASS (nit not taken: the handshake is repeated in 2 tests → Phase 3)
Duration:     ~9 min (to 16:10)

Phase:        2 · row 19
Issue:        ForgeExec awaits I/O without the token it holds (R13)
Practice:     token to every awaited I/O call in ForgeExec.cs (stream read, download, upload, hash)
Commits:      2aad1bf
Behavior:     platform: Ctrl+C → 130 at once
Verify:       build 0 warnings · 1136 passed / 3 baseline · new pin (stream read), run on HEAD first; harness gains HoldOpen
Check:        PASS
Scope:        the row named only the stream read; the supervisor widened it twice (download, upload) to cover the whole unit → P12
Duration:     ~16 min (to 16:17)

Phase:        2 · rows 20, 21 (batched, committed separately by file)
Commits:      a488a88 (20: EditFile.Save temp + File.Move) · 8c727d0 (21: EnumerateFiles count)
Behavior:     20 platform: rename-over (default perms, a symlink becomes a file, the folder must be writable); bytes unchanged · 21 none
Verify:       build 0 warnings · 1137 passed / 3 baseline · pins: FileEditorTests extended; CleanCommandTests (new)
Check:        PASS ×2 (nit not taken: the dead `?? ""`)
Duration:     ~10 min for both (to 16:23)

Phase:        2 · row 17 (lane 2)
Issue:        IL3050/IL2104/IL3053 suppressed project-wide
Behavior:     STOP: "All surfaced trim/AOT warnings come from assemblies outside src/ForgeMission.Cli: 4 × IL3050 in ForgeMission.Core (its field-level UnconditionalSuppressMessage does not cover the ..cctor that ILC reports) and IL2104/IL3053 summarising 36 YamlDotNet warnings. None can be fixed inside the Cli scope, and the ILCompiler 10.0.12 targets offer nothing documented narrower than today's NoWarn." → R42. Partial carve in progress: delete the dead IlcSuppressWarnings and correct the comment.
Publish:      HEAD (with row 16's LibraryImport) publishes green, 0 IL warnings → row 16 covered
Duration:     ~21 min (3 publishes)

Phase:        2 · row 17 partial (lane 2)
Commits:      db32fe6 (lane 2)
Practice:     delete the dead IlcSuppressWarnings; the comment names the real sources; NoWarn unchanged
Verify:       build 0 warnings · NoWarn effective value identical before/after · full suite runs after the merge (csproj-only, no code change)
Check:        PASS (inline: −3/+2 lines)
Rest:         STOP → R42
Duration:     ~4 min
Group D done: 14 ✅ 15 ✅ 16 ✅ 17 partial ✅ + routed. Lane 2 to be cherry-picked after row 22 commits.

Phase:        2 · row 22
Issue:        Actions write to Console instead of SCL's invocation writers
Behavior:     partial carve (actions only; Die/ReportDiagnostic STOPped as plumbing)
Check:        FAIL: "a second way to write output, and nothing uses it" (no code sets non-default writers; the harness reads process streams) → smallest fix: route the row. Carve discarded by the supervisor; rows 22–25 → R43. Lesson P13.
Duration:     ~12 min (to 16:29)

Merge:        lane 2 cherry-picked onto the branch: 14 cba3bd2 · 15 1376b77 · 16 a014f23 · 17 partial 5c05bc9 (lane shas 08dd943, 2555f65, 7aa1b00, db32fe6). No conflicts. Full suite running.

Phase:        2 · row 26
Issue:        Namespaces don't match folders; two global types
Practice:     file-scoped folder namespaces; IDE0130 enforced for this project (.editorconfig + EnforceCodeStyleInBuild)
Commits:      7724552
Behavior:     none (logger category via ILogger<Program>, unchanged; JSON contexts moved with their DTOs)
Verify:       build 0 warnings · 1138 passed / 3 baseline · AOT publish green · enforcement proven by a deliberate wrong namespace (reverted)
Check:        PASS (nit not taken: the redundant default line in .editorconfig documents intent)
Duration:     ~17 min (to 16:46)

Phase:        2 · rows 27, 28, 29 (from re-discover; batched, committed separately)
Commits:      40b9aba (27) · 3b70a4b (28) · 6554ddf (29)
Verify:       build 0 warnings · 1141 passed / 3 baseline (before the Check fix); after it, build 0 warnings and the 39 affected tests pass
Check:        27 FAIL (FileStreamOptions blocks spelled out File.Create's defaults) → supervisor applied File.Create(path, 4096, FileOptions.Asynchronous) → PASS · 28 PASS · 29 PASS (assert made OS-neutral)
Duration:     carve ~4 min, verify and check ~3 min, fix and commit ~1 min (18:24–18:32)
