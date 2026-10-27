# Run log — Stage 2 · forge-mcl / src/ForgeMission.Cli — run 2026-10-27-015904 (run 4)

Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `dotnet-deslop-stage-2` · Base: `fd50eef`
Stage-1 run: `docs/playbooks/runs/dotnet-deslop/forge-mcl-cli/2026-10-25-084621`
Lenses (Phase 1): DI + HttpClient · configuration + options · logging · cancellation
Playbook changes since run 3: one Host as composition root; one config root first; D2 decision gate;
Check step = simplicity-reviewer (no net complexity); D3 split by owner; D4 enabler-after-dependents;
partial-failure retry; races recorded not pinned; lenses check effective build settings.

## Baseline (run start)

`dotnet build -warnaserror`: 0 warnings, 0 errors. `dotnet test`: 1082 passed / 3 failed / 10 skipped (1m37s).

Failing at baseline:
- `PipelineRunnerTests.MissingApiKey_ThrowsClearly` (env-dependent)
- `TuiColourLiteralTests.Only_RawStdout_writes_to_stdout` (Stage-1 #8 regression)
- `TuiColourLiteralTests.Colour_literals_appear_only_in_ForgeTheme` (same)

Env-dependent live tests (`ForgeClaudeLauncherTests`, `ClaudeCodeTests`) don't count as new failures.

One record appended per carve. Newest at the bottom.

---

#1 one host as composition root · 73dd03b · carve 12m00s + correction 5m13s · 4/5
  Practice: Host.CreateApplicationBuilder(Args = []); console → stderr; named clients on the host; CliHttpClients resolves from it
  Behavior: platform — host default config sources (appsettings/env in cwd), host disposal
  Verify: 0 warnings · 1088 passed / 4 failed (= baseline + env-dependent) / 9 skipped · pinned unchanged (6 new, process-level)
  Check: FAIL (pipeline with no consumer; only effect was IHttpClientFactory info noise on stderr) → corrected
         (SetMinimumLevel(Warning) + accurate comment) → PASS
  INCIDENT: carve agent's probe sent one real request to api.forge.katasec.com with the operator's stored credentials
            (MissionNotFound; nothing created). → Playbook: carve prompt now forbids live services / real credentials.
  Finding: The Check step caught on row 1 what run 3 let through, and answered open question Q2 by itself.
           Gap it named: pins didn't exercise an HTTP command, so the noise fix is unproven by tests.

---

#2 (Q1) config.json via host JSON provider · ce7901b · 18m39s · 4/5
  Practice: builder.Configuration.AddJsonFile(path, optional: true); ForgeApiOptions/ForgeChatOptions bound; errors mapped to today's text
  Behavior: platform — provider parsing (comments, case-insensitive keys, string values), other host sources can supply keys;
            Q1-assumed — broken config fails every command at host build, command prefixes lost
  Verify: 0 warnings · 1106 passed / 3 failed (= baseline) / 9 skipped · pinned unchanged (process-level, isolated HOME + non-forwarding proxy)
  Check: PASS (notes: defaults stated twice; host default sources now feed these options)
  Note: row #1's process tests don't isolate HOME — a malformed real ~/.forge/config.json would now break them.

---

#3 consumers take IHttpClientFactory · 01529bb · carve 11m18s + correction 6m19s · 4/5
  Practice: delete static locator; ForgeExec/PlatformLogin/BuildWebSearch take IHttpClientFactory; command tree built after the host
  Behavior: none (one HttpClient per command run instead of per request; same pooled handler)
  Verify: 0 warnings · 1113 passed / 3 failed (= baseline) / 9 skipped · pinned unchanged (7 new; assertions untouched by the correction)
  Check: FAIL (tests: third copy of the process harness) → corrected (shared ForgeProcess helper; also isolates HOME for row #1's tests) → PASS
  Playbook: carve prompt now says "reuse the shared test harness — extend it, never copy it".

---

#4 conversation-host once on the host · f352059 · carve 8m24s + correction 4m37s · 4/5
  Practice: AddHttpClient(ConversationHost, …) in AddCliHttpClients; chat/project take IHttpClientFactory; sign-in check first
  Behavior: platform — handlers from the host's pooled factory; key read per CreateClient
  Verify: 0 warnings · 1117 passed / 5 failed (= baseline + 2 env-dependent live) / 7 skipped · pinned unchanged (4 new, shared harness)
  Check: FAIL (second way to read the endpoint — pre-empted row #6's IOptions move) → corrected → PASS
  Finding: E8 — carves pre-empt later rows; the Check caught the scope bleed.

---

#5 forge mcp on the one host · ac16b18 · 12m13s · 4/5
  Practice: AddMcpServer().WithStdioServerTransport() on the app host; host.RunAsync(); mission handed to handlers via a ServedMissionSlot singleton
  Behavior: platform — host-owned graceful shutdown; SDK logs (Warning+) on stderr; host disposal
  Verify: 0 warnings · 1119 passed / 3 failed (= baseline) / 9 skipped · pinned unchanged (2 new; 5/5 repeated runs)
  Check: PASS (notes: ServedMissionSlot is the cost of "host built before the command picks the mission"; one pin tests an untouched lock check)
  INCIDENT: a baseline probe sent the mission's placeholder key "stub" to OpenAI (401) — second live-call breach despite the prompt rule → E9.
  Note: lane list in the prompt (E8) worked — no bleed into #6/#10/#21.

---

#6 (Q1) endpoint via IOptions · d1e3042 · carve 10m46s + correction 4m23s · 4/5
  Practice: IOptions<ForgeApiOptions>; normalization in PostConfigure; static accessors deleted
  Behavior: platform — options computed once, lazily, cached
  Verify: 0 warnings · 1119 passed / 4 failed (= baseline + env-dependent) / 9 skipped · pinned unchanged
  Check: FAIL (tests: a theory re-proving covered rules + a near-copy test) → corrected → PASS
  Note: no live calls this row (non-forwarding proxy used for every default-host probe). E10 (pin budget) added to the prompt.

---

#7 (Q1) theme via IOptions + [OptionsValidator] · 7e19008 · 8m35s · 4/5
  Practice: [AllowedValues] + source-generated validator; lazy; IOptions<ForgeChatOptions> into chat
  Behavior: platform — OptionsValidationException at the same boundary; same text/exit
  Verify: 0 warnings · 1121 passed / 4 failed (= baseline + env-dependent) / 9 skipped · pinned unchanged (2 cases added to existing tests)
  Check: PASS first time (note: IConfiguration threaded into chat only to rebuild the old message — cost of keeping exact text)
  Finding: E10 (pin budget) worked — extended existing tests instead of new files. Compare run 3 row 8 (3/5, net complexity):
           here the same practice landed flat and passed.

---
