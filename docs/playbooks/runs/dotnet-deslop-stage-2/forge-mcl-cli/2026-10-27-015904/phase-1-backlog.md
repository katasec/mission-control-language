# Stage 2 · Phase 1 — Composition — backlog (run 4)

Run: `2026-10-27-015904` · Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `dotnet-deslop-stage-2` (base `fd50eef`)
Lenses (D1, parallel, `csharp-expert` + `microsoft-docs`): DI + HttpClient · configuration + options · logging · cancellation
Processed: D2 merge+dedupe+route (+ open questions, defaults taken) · D3 split by owner · D4 sequence.
Routed → [routed.md](routed.md) · Questions → [questions.md](questions.md)

40 lens rows → 24 backlog rows, 9 routed (+ Stage-1 STOPs). Rows marked *(Q1)* rest on an assumed answer.

## Sequence (D4)

- **A — Foundation**: 1 (one host) → 2 (one configuration root)
- **B — Registrations**: 3–5
- **C — Configuration + options**: 6–9
- **D — Logging**: 10–12
- **E — Cancellation**: 13–23 (actions honor the token before anything relies on it)
- **F — Guard**: 24 (CA2016 last — it would fail the build until every token flows)

| # | Issue | Locations (symbol, file) | Practice to adopt | Sev |
|---|---|---|---|---|
| 1 | No composition root (= Stage-1 STOP #1); no logging pipeline | top-level statements (Program.cs) | `Host.CreateApplicationBuilder(new() { Args = [] })`; console logger to stderr (`LogToStandardErrorThreshold = Trace`); build once; commands resolve services from it | High |
| 2 | *(Q1)* `config.json` read by hand (File + STJ), defaults in code | `ForgeConfig.ReadFile/ReadApiEndpoint/ReadTheme` (Services/ForgeConfig.cs) | `builder.Configuration.AddJsonFile(path, optional: true)` (env providers already on the host); bind `ForgeApiOptions` / `ForgeChatOptions`, defaults as initializers; keep today's error text | Med |
| 3 | Static locator `CliHttpClients` (own container, never disposed); consumers call it statically | `CliHttpClients` (Services/CliHttpClients.cs); `ForgeExec.Http`, `PlatformLogin.Http`, `ProviderClientBuilder.BuildWebSearch` | Named clients registered on the host; consumers take `IHttpClientFactory`; delete the locator | Med |
| 4 | `conversation-host` registered twice in throwaway containers | `ForgeChat.RunAsync`; `ForgeProject.CreateAsync` | Register once on the host (`AddHttpClient(name, (sp, c) => …)`); sign-in check stays first | Med |
| 5 | `forge mcp` on a bare, never-disposed `ServiceCollection`; SDK logs discarded | `McpCommand.ServeAsync` | `AddMcpServer().WithStdioServerTransport()…` on the app's host; `RunAsync`; host owns this long-running command's shutdown | High |
| 6 | *(Q1)* API endpoint re-read from disk on every access | `ForgeExec.ApiEndpoint`; `ForgeChat`, `ForgeProject` BaseAddress | `IOptions<ForgeApiOptions>`; Trim/TrimEnd in `PostConfigure` | Med |
| 7 | *(Q1)* Theme validation hand-coded + custom exception | `ForgeConfig.ReadTheme`, `ValidThemes`; catch in `ForgeChat.RunAsync` | `[AllowedValues]` + source-generated `[OptionsValidator]`; lazy (no `ValidateOnStart`); same message | Low |
| 8 | Login env overrides hand-read with inline defaults | `PlatformLogin.Authority/ClientId/Scope/PlatformEndpoint` | `PlatformAuthOptions` from the host configuration (`[ConfigurationKeyName("FORGE_AUTH_…")]`), defaults as initializers | Med |
| 9 | Web-search keys read straight from env with a hand fallback | `ProviderClientBuilder.BuildWebSearch` | `WebSearchOptions` (`[ConfigurationKeyName("XAI_API_KEY")]`, `"GROK_API_KEY"`); keep the selection logic | Low |
| 10 | MCP tool-call exceptions swallowed (`ex.Message` only) | `McpCommand` → `WithCallToolHandler` | `[LoggerMessage(Level = Error)]` with the `Exception`; same `CallToolResult` | Med |
| 11 | Aux chat client failure swallowed | `TryBuildMissionServerAsync` (Program.cs) | `[LoggerMessage(Level = Warning)]` via the host's logger (stderr); fallback unchanged | Med |
| 12 | Browser-launch failure swallowed by bare `catch {}` | `PlatformLogin.OpenBrowser` | `[LoggerMessage(Level = Debug)]`; URL fallback unchanged | Low |
| 13 | Short commands use the token-less `SetAction` | `BuildInitCommand`, `BuildValidateCommand`, `BuildListCommand`, `BuildExpertCommand`, `BuildPublishCommand`, `BuildProviderScaffoldCommand` (Program.cs) | `SetAction((result, ct) => …)`; `ct` to every awaited call | Med |
| 14 | exec/run ignore the token; stream unbounded; download timeout unlinked; cancel reported as "Could not reach"/"timed out" | `BuildExecCommand`, `BuildRunCommand`; `ForgeExec.RunAsync`, `DownloadUrlInputAsync` | Action `ct` through every call incl. stream reads; linked CTS + `CancelAfter`; catches `when !ct.IsCancellationRequested` | Med |
| 15 | login/whoami ignore the token; callback wait is a `WhenAny`/`Delay` race | `BuildLoginCommand`, `BuildWhoamiCommand`; `PlatformLogin.*Async`, `WaitForCallbackAsync` | Action `ct` through every call; one linked CTS + `CancelAfter(5 min)`; `GetContextAsync().WaitAsync` | Med |
| 16 | `project create` passes `CancellationToken.None` | `ForgeProject.BuildCommand`, `RunAsync` | Action `ct` through to `CreateChatProjectAsync` | Low |
| 17 | Chat line mode: per-turn `CancelKeyPress` + CTS; `None` hard-coded | `ForgeChat.ChatAsync` and the chat call chain | Action `ct` through; remove per-turn handler; keep cancel branch (Cancelled., 130) | Med |
| 18 | `--print-env` waits with a TCS + never-unsubscribed `CancelKeyPress` | `WaitForCtrlCAsync` (Program.cs) | `Task.Delay(Timeout.InfiniteTimeSpan, ct)`, catch OCE, fall into teardown | Med |
| 19 | Health probe: bare `new HttpClient()`; hand-rolled `DateTime` deadline | `WaitUntilHealthyAsync` (Program.cs) | Named client `forge-health` on the host; linked CTS + `CancelAfter` for `SendAsync`/`Delay` | Low |
| 20 | serve/connect: two signal owners; web host runs outside the command's lifetime | `BuildServeCommand`; `ConnectCommand.Build` | Long-running: host owns shutdown (`ProcessTerminationTimeout = null` for these); `RunAsync` | Med |
| 21 | MCP tool handler ignores its request token | `WithCallToolHandler` → `PipelineRunner.RunAsync` | Pass the handler's `ct`; catch excludes OCE | Med |
| 22 | agent/webui actions use the token-less `SetAction` | `AgentCommand.Build/BuildStopCommand`; `WebuiCommand.Build/BuildStopCommand` | `SetAction((result, ct) => …)`; `ct` to awaited calls that accept one | Low |
| 23 | Remaining short commands' grace window: default 2s cuts teardown | top-level `InvocationConfiguration` | Explicit `ProcessTerminationTimeout` = `HostOptions.ShutdownTimeout` default (30s) — after actions honor the token | Med |
| 24 | No build guard for dropped tokens (CA2016 at suggestion) | src/ForgeMission.Cli | `.editorconfig`: `dotnet_diagnostic.CA2016.severity = warning` (fails build under TreatWarningsAsErrors) — last | Low |

## Playbook observations (discovery)

1. **Composition conflict gone.** All four lenses prescribed against the one host.
2. **Effective-settings check worked.** No redundant binding-generator row.
3. **One genuine open question** (Q1); two lens disagreements were settled by existing rules.
4. **D3 by owner** merged 14 cancellation findings into 11 rows by owner (exec, login, chat…), not sub-step.
5. **D4 enabler-after-dependents** applied twice: the grace window (#23) and CA2016 (#24) go last.

D1 durations (parallel): logging 2m52s · DI 2m57s · configuration 4m43s · cancellation 5m25s.
