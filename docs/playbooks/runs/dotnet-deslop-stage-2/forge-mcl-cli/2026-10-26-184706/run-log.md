# Run log — Stage 2 · forge-mcl / src/ForgeMission.Cli — run 2026-10-26-184706

Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `dotnet-deslop-stage-2` · Base: `fd50eef`
Stage-1 run: `docs/playbooks/runs/dotnet-deslop/forge-mcl-cli/2026-10-25-084621`
Mode: carve prompt verbatim (agent does not commit); supervisor verifies, checks the diff, commits locally.
One record appended per carve. Newest at the bottom.

---

Phase:        1
Issue:        #1 Static service-locator CliHttpClients (own container, never disposed)
Locations:    Services/CliHttpClients.cs; Commands/ForgeExec.cs; Commands/PlatformLogin.cs; Services/ProviderClientBuilder.cs
Practice:     One ServiceCollection composition root in Program.cs; named clients; IHttpClientFactory passed into actions
Reference:    learn.microsoft.com/dotnet/core/extensions/httpclient-factory · .../dependency-injection/basics
Commits:      1cfc247
Behavior:     retained
Verify:       build 0 warnings · tests 1090 passed / 3 failed (same 3 fail on baseline) / 10 skipped · pinned unchanged (8 new characterization tests)
Duration:     6m41s
Rating:       4/5 — clean single root, pinned before swap; no AOT publish run.
Finding:      2 of the 3 "baseline" failures (TuiColourLiteralTests) were CAUSED BY STAGE 1 #8 (folder layout
              moved ForgeConfig.cs to Services/; test hard-codes the old path). Stage-1 verify accepted them as
              "pre-existing" — no carve compared against the pre-run baseline. → Stage 1 regression.

---

Phase:        1
Issue:        #2 conversation-host named client registered twice in throwaway containers
Locations:    Commands/ForgeChat.cs; Commands/ForgeProject.cs
Practice:     Register once in the composition root via AddHttpClient(name, (sp, c) => …); credential singleton
Reference:    learn.microsoft.com/dotnet/core/extensions/httpclient-factory
Commits:      1a2e87c
Behavior:     retained (residual: key read twice ms apart — a credentials file deleted in that gap now throws "Not signed in" from the registration)
Verify:       build 0 warnings · tests 1093 passed / 5 failed (3 baseline + 2 live integration not re-run on baseline) / 7 skipped · pinned unchanged (2 new)
Duration:     11m33s
Rating:       4/5 — good behavior-preservation reasoning (singleton credential keeps capture-once); tiny residual race.

---

Phase:        1
Issue:        #3 webui health probe uses a hand-rolled new HttpClient()
Locations:    Program.cs (WaitUntilHealthyAsync)
Practice:     Named client forge-health on the composition root; CreateClient in WaitUntilHealthyAsync
Reference:    learn.microsoft.com/dotnet/core/extensions/httpclient-factory
Commits:      e70fede
Behavior:     retained
Verify:       build 0 warnings · tests 1097 passed / 3 failed (same 3 baseline) / 9 skipped · pinned unchanged (3 new + 1 post-swap)
Duration:     6m28s
Rating:       4/5 — clean; pinned tests call the top-level local function by reflection (brittle).

---

Phase:        1
Issue:        #4 Long-running forge serve captures one web-search client for its lifetime (stale DNS)
Locations:    Services/CliHttpClients.cs (forge-websearch registration)
Practice:     ConfigurePrimaryHttpMessageHandler → SocketsHttpHandler { PooledConnectionLifetime = 2 min }
Reference:    learn.microsoft.com/dotnet/core/extensions/httpclient-factory ("…together with SocketsHttpHandler")
Commits:      7f4e791
Behavior:     retained
Verify:       build 0 warnings · tests 1101 passed / 3 failed (same 3 baseline) / 9 skipped · pinned unchanged (4 new: redirect, compression, cookies, no base address)
Duration:     9m00s
Rating:       5/5 — minimal, exact prescription; pinned exactly the behaviors a handler swap could change.
Finding:      The lens's Learn reference was the wrong page (troubleshooting has no SocketsHttpHandler guidance);
              the carve's `microsoft-docs` check caught it and found the right section. Grounding at carve time works.

---

Phase:        1
Issue:        #5 Config re-read on every property access (ApiEndpoint up to 4×/run; chat parses twice)
Locations:    Commands/ForgeExec.cs; Commands/ForgeChat.cs; Services/ForgeConfig.cs; Services/CliHttpClients.cs; Program.cs
Practice:     IOptions<ForgeCliOptions> registered once via AddOptions<T>().Configure (deferred, cached)
Reference:    learn.microsoft.com/dotnet/core/extensions/options
Commits:      f6dd973
Behavior:     retained (exec/chat RunAsync public → internal; only internal callers)
Verify:       build 0 warnings · tests 1109 passed / 4 failed (baseline set) / 9 skipped · pinned unchanged (9 new)
Duration:     13m06s
Rating:       5/5 — correct deviation: the backlog's eager Options.Create would have changed error ordering
              on a malformed config.json; used MS's deferred AddOptions<T>().Configure and pinned the ordering.
Finding:      Lens prescriptions can themselves break behavior; the carve's behavior-preservation rule must win
              over the backlog's prescription. Playbook should say so explicitly ("the rule beats the row").

---

Phase:        1
Issue:        #6 Login/web-search settings read straight from Environment.GetEnvironmentVariable
Locations:    Commands/PlatformLogin.cs; Services/ProviderClientBuilder.cs
Practice:     AddEnvironmentVariables provider + options binding (ConfigurationKeyName, binding source generator)
Reference:    learn.microsoft.com/dotnet/core/extensions/configuration-providers · .../configuration-generator
Commits:      none — STOP (work stashed in forge-mcl: "S2P1 #6" stash)
Behavior:     STOP: "AddEnvironmentVariables makes keys case-insensitive, so differently-cased variable names now override the settings on Unix."
Verify:       (stashed work) build 0 warnings · AOT publish clean · tests 1120 passed / 4 failed (baseline set) · pinned unchanged (10 new)
Duration:     19m53s
Rating:       4/5 work quality; outcome STOP by supervisor.
Finding:      The agent did NOT stop — it flagged the edge case and handed the call to the supervisor. The playbook
              rule says STOP; agents treat "inherent platform semantics in an unexercised edge case" as a gray zone.
              Open playbook question: does a facility's inherent semantics (case-insensitive keys) count as a
              behavior change? Same shape as the logging question.

---

Phase:        1
Issue:        #10 forge mcp has no logging providers; MCP SDK logs go to NullLoggerFactory
Locations:    Commands/McpCommand.cs; ForgeMission.Cli.csproj (+ Microsoft.Extensions.Logging.Console 10.0.9)
Practice:     AddLogging + console logger, LogToStandardErrorThreshold = Trace, min level Warning
Reference:    learn.microsoft.com/dotnet/core/extensions/logging/overview · ConsoleLoggerOptions.LogToStandardErrorThreshold
Commits:      fbddcef
Behavior:     retained (logging exception: new stderr diagnostics only; stdout byte-identical)
Verify:       build 0 warnings · tests 1115 passed / 5 failed (3 baseline + 2 live-API 401) / 7 skipped · pinned unchanged (5 new)
Duration:     7m27s
Rating:       5/5 — exact and minimal; proved stdout byte-identical and stderr-only routing at Trace.
Note:         Skipped `csharp-expert`/`csharp-refactoring` (5-line registration) — prompt says invoke; harmless here.
              AOT publish not run for the new package.

---

Phase:        1
Issue:        #11 MCP tool-call handler turns every exception into ex.Message with no log
Locations:    Commands/McpCommand.cs (both catch sites)
Practice:     [LoggerMessage(Level = Error)] source-generated method, exception as the Exception parameter
Reference:    learn.microsoft.com/dotnet/core/extensions/logging/source-generation
Commits:      571357b
Behavior:     retained (logging exception: one new stderr `fail:` line per failed call)
Verify:       build 0 warnings · tests 1118 passed / 4 failed (3 baseline + 1 live-API) / 8 skipped · pinned unchanged (2 new + 1 post-swap)
Duration:     10m09s
Rating:       5/5 — clean helper, NullLogger fallback guarantees the result can't change.

---

Phase:        1
Issue:        #7 Sync and async actions mixed; sync actions get no termination token
Locations:    Program.cs (registry login, logout, provider list); Commands/CleanCommand.cs
Practice:     Async SetAction((result, ct) => …) returning Task / Task<int>
Reference:    learn.microsoft.com/dotnet/standard/commandline/how-to-parse-and-invoke#asynchronous-actions
Commits:      none — STOP (work stashed in forge-mcl: "S2P1 #7" stash)
Behavior:     STOP: "Before, a sync action registered no termination handler, so Ctrl-C mid-action killed the process at once (exit 130). Now Ctrl-C cancels the token and the action gets the default 2-second window to finish." (action completes, exits with its own code)
Verify:       (stashed work) build 0 warnings · tests 1125 passed / 5 failed (baseline set) · pinned unchanged (7 new)
Duration:     7m07s
Rating:       4/5 work quality; outcome STOP by supervisor.
Finding:      Second agent in a row flagged an edge case for review instead of STOPping. And the backlog row
              itself was mis-classified at discovery: the lens rated it "behavior impact: none", but adopting
              the termination token inherently changes Ctrl-C handling. Discovery's impact column can be wrong.

---

Phase:        1
Issue:        #8 forge exec 30s download timeout uses an unlinked CTS; any TaskCanceledException reported as network failure
Locations:    Commands/ForgeExec.cs
Practice:     ct parameter (default); CreateLinkedTokenSource(ct) + CancelAfter; catches filtered when (!ct.IsCancellationRequested)
Reference:    learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/coalesce-cancellation-tokens-from-timeouts
Commits:      baf0986
Behavior:     retained — call sites still pass no token. Agent's partial STOP: wiring the action token changes the
              Ctrl-C exit code (130 → 1 via Program.cs's top-level OCE catch); same as the #7 STOP.
Verify:       build 0 warnings · tests 1123 passed / 5 failed (not confirmed on untouched baseline — see incident) · pinned unchanged (4 new)
Duration:     8m01s
Rating:       3/5 — correct, behavior-identical adoption; marked down for the stash incident.
INCIDENT:     The agent used `git stash` to isolate its change; a malformed push stashed nothing and the following
              `pop` applied and dropped the supervisor's "S2P1 #7" stash. It recovered it by hash (git fsck + stash
              store); supervisor verified the stash stack intact. → Playbook gap: the carve prompt must forbid
              git state operations (stash/checkout/reset/commit) and give agents a safe baseline method.

---

Phase:        1
Issue:        #9 Sign-in wait: Task.WhenAny(GetContextAsync(), Task.Delay(5 min)), timer never cancelled
Locations:    Commands/PlatformLogin.cs
Practice:     One linked CTS + CancelAfter(5 min) for the whole wait; GetContextAsync().WaitAsync(token)
Reference:    learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/coalesce-cancellation-tokens-from-timeouts
Commits:      cdfb077
Behavior:     retained — call sites pass no token yet
Verify:       build 0 warnings · tests 1127 passed / 5 failed (baseline set) / 7 skipped · pinned unchanged (3 new + 1 post-swap); 5-min timeout checked manually at 1s
Duration:     7m50s
Rating:       5/5 — rejected Learn's per-call WaitAsync(TimeSpan) because it would restart the deadline per request
              (behavior change); kept one overall deadline.
SUPERVISOR DEVIATION: added "no state-changing git commands; compare via git show HEAD:<path>" to this carve's prompt
              after the #8 incident. Respected.
Finding:      New characterization tests (ForgeExecDownload*, PlatformLoginCallback*) swap global Console.SetError
              while xUnit runs classes in parallel → stderr can interleave between tests (flaky). Carves need a
              rule: tests that touch global console state go in one non-parallel collection.

---
