# Phase 1 backlog — Composition · run `2026-10-27-042106` (run 5)

Repo `forge-mcl` · scope `src/ForgeMission.Cli` · branch `dotnet-deslop-stage-2` · base `fd50eef`
Lenses: DI + HttpClient (5) · configuration + options (5) · logging (7) · cancellation (13) → 32 findings
→ **20 rows** + row 0 (6 merged, 9 routed — see `routed.md`; 2 open questions — see `questions.md`).
Stage-1 run STOPs: none in Phase-1 areas.

Files are under `src/ForgeMission.Cli/`. `P` = Program.cs.

## Row 0 — shared test harness

| # | Row | Files |
|---|---|---|
| 0 | Shared process harness: runner, isolated HOME, local HTTP stubs, **non-forwarding proxy** (`HTTP_PROXY`/`HTTPS_PROXY`) forced on every probe/test | tests only |

## Group A — composition foundation (sequential; one implementer)

| # | Sev | Issue | Locations | Practice | Reference | Impact | Files |
|---|---|---|---|---|---|---|---|
| 1 | High | No host: four throwaway `ServiceCollection`s, no logging pipeline | `ForgeChat.RunAsync`, `ForgeProject.CreateAsync`, `McpCommand.ServeAsync`, `CliHttpClients` | One `Host.CreateApplicationBuilder(new() { Args = [] })` at the CLI root; console logger → stderr, Warning minimum; commands resolve from `host.Services` (merges DI root + logging rows 1–2) | dotnet/core/extensions/generic-host; logging/providers | platform: library logs → stderr at ≥Warning | P (+ new Hosting file) |
| 2 | Med | config.json via hand-rolled reader | `ForgeConfig.ReadFile/ReadApiEndpoint`, Services/ForgeConfig.cs; `ForgeChat.RunAsync` | `AddJsonFile(~/.forge/config.json, optional)` on the host + `ForgeSettings` bound via `AddOptions().Bind`; endpoint normalisation in `PostConfigure` (**Q1**) | configuration-providers#json; options | app (Q1 default 1): malformed file fails at host build | ForgeConfig.cs, ForgeChat.cs, P |
| 3 | Med | Theme check hard-coded in static | `ForgeConfig.ReadTheme`/`ValidThemes` | `IValidateOptions<ForgeSettings>` with the current message; no `ValidateOnStart` | options#options-validation | none | ForgeConfig.cs, ForgeChat.cs |
| 4 | Med | Endpoint re-read from disk per access | `ForgeExec.ApiEndpoint`; `ForgeChat.RunAsync`; `ForgeProject.CreateAsync` | Inject `IOptions<ForgeApiSettings>` (split at row 3 — rule beats the row) | options#ioptionst | platform: read once per process | ForgeExec.cs, ForgeChat.cs, ForgeProject.cs |
| 5 | Med | Static locator `CliHttpClients` builds its own container | `CliHttpClients`; `ForgeExec.Http`; `PlatformLogin.Http`; `ProviderClientBuilder.BuildWebSearch` | Named clients `forge-exec`/`forge-login`/`forge-websearch` on the host; inject `IHttpClientFactory`; delete locator | httpclient-factory; DI guidelines | platform: factory disposed with host | CliHttpClients.cs, ForgeExec.cs, PlatformLogin.cs, ProviderClientBuilder.cs, P |
| 6 | Med | `conversation-host` registered twice in throwaway containers | `ForgeChat.RunAsync`, `ForgeProject.CreateAsync` | Register once on the host with `AddHttpClient(name, (sp, c) => …)` reading `IOptions<ForgeApiSettings>` | httpclient-factory#named-clients | platform: one handler pool | ForgeChat.cs, ForgeProject.cs, P |

## Group B — options + client settings (after A; one implementer)

| # | Sev | Issue | Locations | Practice | Reference | Impact | Files |
|---|---|---|---|---|---|---|---|
| 7 | Med | Login settings via ad-hoc env reads | `PlatformLogin.Authority/ClientId/Scope/PlatformEndpoint` | `PlatformLoginOptions` + `[ConfigurationKeyName]`, `Bind`, `PostConfigure` TrimEnd | configuration-providers#env; options | platform: also from other sources; read once | PlatformLogin.cs, P |
| 8 | Low | Web-search key via ad-hoc env reads | `ProviderClientBuilder.BuildWebSearch` | `WebSearchOptions` configured from `IConfiguration` | options#use-di-services-to-configure-options | platform: read once | ProviderClientBuilder.cs, P |
| 9 | Med | Long-held `forge-websearch` client never rotates (trivial) | `TryBuildMissionServerAsync` P; registration | `UseSocketsHttpHandler(PooledConnectionLifetime = 2 min).SetHandlerLifetime(Infinite)` | httpclient-factory-troubleshooting | platform: connections recycled | P |
| 10 | Low | Health probe `new HttpClient()` (trivial) | `WaitUntilHealthyAsync` P | Named client `forge-health` from the factory | httpclient-factory | none | P |
| 11 | Med | Aux-client build failure swallowed silently | `TryBuildMissionServerAsync` P | `[LoggerMessage]` Warning with the exception; keep fallback | logging/source-generation | platform: Warning on stderr | P |

## Group C — MCP server (after A; one implementer)

| # | Sev | Issue | Locations | Practice | Reference | Impact | Files |
|---|---|---|---|---|---|---|---|
| 12 | High | MCP stdio server on a hand-built container, `RunAsync(None)` | `McpCommand.ServeAsync` | `AddMcpServer().WithStdioServerTransport()` on the one host, `host.RunAsync()`; `SuppressStatusMessages`; termination timeout null for `mcp` (host owns shutdown) (merges DI #3, logging #2, cancel #8) | ai/quickstarts/build-mcp-server; generic-host#host-shutdown | platform: graceful host shutdown; logs stderr only | McpCommand.cs, P |
| 13 | Med | Tool-call handler ignores request token (trivial) | `WithCallToolHandler` lambda | pass `ct` to `PipelineRunner.RunAsync`; `catch when (!ct.IsCancellationRequested)` | task-cancellation | platform: client cancel stops run | McpCommand.cs |

## Group D — cancellation, leaf files (disjoint from B/C → parallel worktree)

| # | Sev | Issue | Locations | Practice | Reference | Impact | Files |
|---|---|---|---|---|---|---|---|
| 14 | Med | Catches treat any cancellation as timeout/transport failure | `ForgeExec.RunAsync`, `ForgeExec.DownloadUrlInputAsync`, `ForgeChat.RunAsync` | `when (!cancellationToken.IsCancellationRequested)` filters | httpclient#http-error-handling | none | ForgeExec.cs, ForgeChat.cs |
| 15 | Low | Download timeout CTS not linked (trivial) | `ForgeExec.DownloadUrlInputAsync` | `CreateLinkedTokenSource(ct)` + `CancelAfter` | how-to-listen-for-multiple-cancellation-requests | none | ForgeExec.cs |
| 16 → 18 | Med | (folded into 18 — STOP, see run-log) Line-mode chat installs its own `CancelKeyPress` per turn | `ForgeChat.ChatAsync` | use the action token; drop handler + CTS | commandline/how-to-parse-and-invoke#actions | none | ForgeChat.cs |
| 17 | Med | TUI session CTS not linked to command token | `ChatTui.RunAsync` | `CreateLinkedTokenSource(cancellationToken)` | how-to-listen-for-multiple-cancellation-requests | platform: SIGTERM = Ctrl-D path | Tui/ChatTui.cs, ForgeChat.cs |

## Group E — cancellation, roots (after A–D; one implementer)

| # | Sev | Issue | Locations | Practice | Reference | Impact | Files |
|---|---|---|---|---|---|---|---|
| 18 | High | Actions use token-less overload; `CancellationToken.None` passed | all `SetAction` lambdas (P, ForgeChat, ForgeProject, ConnectCommand, WebuiCommand, AgentCommand, CleanCommand); `None` sites in ForgeChat, ForgeProject, P, MacChatWindow | `SetAction(async (parseResult, ct) => …)`, thread `ct`; top-level OCE catch returns **130** so the exit code is retained | commandline/how-to-parse-and-invoke#actions | platform: in-flight work cancelled at once; exit code 130 retained | P, ForgeChat.cs, ForgeProject.cs, ConnectCommand.cs, WebuiCommand.cs, AgentCommand.cs, CleanCommand.cs, MacChatWindow.cs |
| 19 | Low | Hand-rolled `WhenAny` + `Delay` login timeout (trivial) | `PlatformLogin.WaitForCallbackAsync` | linked CTS `CancelAfter(5 min)` + `GetContextAsync().WaitAsync(token)` | task.waitasync | none | PlatformLogin.cs |
| 20 | Med | One 2 s termination policy for long-running servers; two signal owners | `InvocationConfiguration` P; `serve` `app.RunAsync()` P; `ConnectCommand.BuildVsCodeCommand` | `ProcessTerminationTimeout = null` for `serve` and `connect vscode`; host `ConsoleLifetime` sole owner (enabler after its dependents, #18) | invocationconfiguration.processterminationtimeout; generic-host#host-shutdown | platform: host `ShutdownTimeout` grace | P, ConnectCommand.cs |

## Order and parallelism

`0 → A(1–6) → { B(7–11) → C(12–13) }  ‖  D(14–17) → E(18–20)`

- A, B, C, E all touch Program.cs → sequential.
- D touches only ForgeExec.cs / ForgeChat.cs / ChatTui.cs; A also touches ForgeChat.cs and ForgeExec.cs,
  so D starts **after A**, in its own worktree, in parallel with B→C; merged (cherry-picked in order) before E.
