# Stage 2 · Phase 1 — Composition — backlog

Run: `2026-10-26-213840` · Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `dotnet-deslop-stage-2` (base `fd50eef`)
Stage-1 run: `docs/playbooks/runs/dotnet-deslop/forge-mcl-cli/2026-10-25-084621`
Lenses (D1, parallel, `csharp-expert` + `microsoft-docs`): DI + HttpClient · configuration + options · logging · cancellation
Processed: D2 merge+dedupe+reconcile+route · D3 split · D4 sequence. Routed → [routed.md](routed.md).

27 lens rows → 23 backlog rows, 6 routed. Behavior rule: retain the app's own behavior; platform
semantics are adopted and recorded.

## Sequence (D4)

- **A — Composition root**: 1
- **B — Registrations**: 2–4
- **C — Configuration + options**: 5–10
- **D — Logging**: 11
- **E — Cancellation**: 12–23

## A — Composition root

| # | Issue | Locations (symbol, file) | Practice to adopt | Reference | Sev |
|---|---|---|---|---|---|
| 1 | Static service locator builds a hidden, never-disposed container (= Stage-1 STOP #1) | `CliHttpClients.BuildFactory/Factory/Create` (Services/CliHttpClients.cs); consumers `ForgeExec.Http`, `PlatformLogin.Http`, `ProviderClientBuilder.BuildWebSearch` | One composition root in Program.cs for the short-lived CLI: a single `ServiceCollection` (`await using` provider) registering the named clients; pass `IHttpClientFactory` into the commands | [httpclient-factory](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory) · [DI guidelines](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/guidelines) | Med |

## B — Registrations

| # | Issue | Locations | Practice to adopt | Reference | Sev |
|---|---|---|---|---|---|
| 2 | `conversation-host` registered twice in throwaway containers | `ForgeChat.RunAsync` (Commands/ForgeChat.cs); `ForgeProject.CreateAsync` (Commands/ForgeProject.cs) | Register once on the root via `AddHttpClient(name, (sp, c) => …)`; credential from a registered service; sign-in check stays first | [httpclient-factory](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory) | Med |
| 3 | Bare `new HttpClient()` in the health poll | `WaitUntilHealthyAsync` (Program.cs) | Named client `forge-health` on the root; `CreateClient` | [httpclient guidelines](https://learn.microsoft.com/dotnet/fundamentals/networking/http/httpclient-guidelines#recommended-use) | Low |
| 4 | Long-running stdio MCP server composed by hand: bare container, never disposed, no lifetime, no logging | `McpCommand.ServeAsync` (Commands/McpCommand.cs) | Generic Host for this long-running command: `Host.CreateApplicationBuilder()`, `builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)`, `AddMcpServer().WithStdioServerTransport()…`, `RunAsync` | [build-mcp-server](https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-server) · [generic-host](https://learn.microsoft.com/dotnet/core/extensions/generic-host) | Med |

## C — Configuration + options

| # | Issue | Locations | Practice to adopt | Reference | Sev |
|---|---|---|---|---|---|
| 5 | Binding about to be added to a `PublishAot` app without the source-generated binder | ForgeMission.Cli.csproj | `<EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>` | [configuration-generator](https://learn.microsoft.com/dotnet/core/extensions/configuration-generator) | Med |
| 6 | `~/.forge/config.json` read by hand (`File.ReadAllText` + deserialize, defaults in code) | `ForgeConfig.ReadFile/ReadApiEndpoint/ReadTheme` (Services/ForgeConfig.cs) | `ConfigurationBuilder().AddJsonFile(path, optional: true)` bound to a `ForgeSettings` options class with defaults as initializers | [JSON provider](https://learn.microsoft.com/dotnet/core/extensions/configuration-providers#json-configuration-provider) · [options](https://learn.microsoft.com/dotnet/core/extensions/options) | Med |
| 7 | Config re-read on every access (`ApiEndpoint` per request) | `ForgeExec.ApiEndpoint` (Commands/ForgeExec.cs); `ForgeChat`, `ForgeProject` conversation-host registration | Build once per process, bind once, read `IOptions<ForgeSettings>.Value` | [options](https://learn.microsoft.com/dotnet/core/extensions/options) | Low |
| 8 | Theme validation is a hand-written switch + custom exception | `ForgeConfig.ReadTheme`, `ValidThemes` (Services/ForgeConfig.cs); catch in `ForgeChat.RunAsync` | `IValidateOptions<ForgeSettings>` with today's message text; catch `OptionsValidationException` | [options validation](https://learn.microsoft.com/dotnet/core/extensions/options#options-validation) | Low |
| 9 | Login env overrides hand-rolled (`GetEnvironmentVariable ?? default`) (= run-2 STOP #6) | `PlatformLogin.Authority/ClientId/Scope/PlatformEndpoint` (Commands/PlatformLogin.cs) | `AddEnvironmentVariables("FORGE_")` → `PlatformLoginOptions` with `[ConfigurationKeyName]` keeping names; defaults as initializers | [env provider](https://learn.microsoft.com/dotnet/core/extensions/configuration-providers#environment-variable-configuration-provider) | Med |
| 10 | Web-search key read with hand-rolled alias `XAI_API_KEY ?? GROK_API_KEY` | `ProviderClientBuilder.BuildWebSearch` (Services/ProviderClientBuilder.cs) | Read both from the shared env-backed `IConfiguration`; keep the whitespace check | [env provider](https://learn.microsoft.com/dotnet/core/extensions/configuration-providers#environment-variable-configuration-provider) | Low |

## D — Logging

| # | Issue | Locations | Practice to adopt | Reference | Sev |
|---|---|---|---|---|---|
| 11 | MCP tool-call failures returned as `ex.Message` with no log (rides on #4) | `McpCommand.ServeAsync` → `WithCallToolHandler` | `[LoggerMessage(Level = LogLevel.Error)]` with the `Exception` parameter; same `CallToolResult` | [source generation](https://learn.microsoft.com/dotnet/core/extensions/logging/source-generation) | Med |

## E — Cancellation

| # | Issue | Locations | Practice to adopt | Reference | Sev |
|---|---|---|---|---|---|
| 12 | Default 2s `ProcessTerminationTimeout` cuts off cleanup | top-level `InvocationConfiguration` (Program.cs) | Set it explicitly to `HostOptions.ShutdownTimeout`'s default (30s) | [ProcessTerminationTimeout](https://learn.microsoft.com/dotnet/api/system.commandline.invocationconfiguration.processterminationtimeout) | Med |
| 13 | Program.cs async actions use the token-less `SetAction` overload | `BuildInitCommand`, `RunInitAsync`, `PullOciExpertAsync`, `BuildValidateCommand`, `BuildExpertCommand`, `BuildPublishCommand`, `BuildProviderScaffoldCommand`, `TryReadFile` (Program.cs) | `SetAction((result, ct) => …)`; pass `ct` to every async call inside Program.cs | [async actions](https://learn.microsoft.com/dotnet/standard/commandline/how-to-parse-and-invoke#asynchronous-actions) | Med |
| 14 | `forge exec`/`run` take no token; transport catch treats user cancel as "Could not reach" | `BuildExecCommand`, `BuildRunCommand` (Program.cs); `ForgeExec.RunAsync`, `UploadLocalInputAsync`, `DownloadArtifactAsync` (Commands/ForgeExec.cs) | Action `ct` → `RunAsync(ct)` → every async call; transport catch `when !ct.IsCancellationRequested` | [async actions](https://learn.microsoft.com/dotnet/standard/commandline/how-to-parse-and-invoke#asynchronous-actions) | Med |
| 15 | Download timeout CTS not linked to command token | `ForgeExec.DownloadUrlInputAsync` | `CreateLinkedTokenSource(ct)` + `CancelAfter`; timeout message only `when !ct.IsCancellationRequested` | [cancel after period](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/cancel-async-tasks-after-a-period-of-time) | Low |
| 16 | `forge login`/`whoami` take no token | `BuildLoginCommand`, `BuildWhoamiCommand` (Program.cs); `PlatformLogin.RunAsync`, `WhoAmIAsync`, `FetchMeAsync`, `IssuePlatformKeyAsync`, `ExchangeCodeAsync` | Action `ct` flowed through every async call | [async actions](https://learn.microsoft.com/dotnet/standard/commandline/how-to-parse-and-invoke#asynchronous-actions) | Med |
| 17 | Sign-in wait: `Task.WhenAny` + `Task.Delay(5 min)`, timer never cancelled | `PlatformLogin.WaitForCallbackAsync` | One linked CTS + `CancelAfter(5 min)`; `GetContextAsync().WaitAsync(token)` | [cancel after period](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/cancel-async-tasks-after-a-period-of-time) | Low |
| 18 | `forge project` takes no token | `ForgeProject.BuildCommand`, `CreateAsync` | Action `ct` flowed to `CreateChatProjectAsync` | [async actions](https://learn.microsoft.com/dotnet/standard/commandline/how-to-parse-and-invoke#asynchronous-actions) | Low |
| 19 | Chat line mode: per-turn `CancelKeyPress` + CTS beside System.CommandLine's handler; `CancellationToken.None` elsewhere | `ForgeChat.BuildCommand/RunAsync/ChatAsync/…` (Commands/ForgeChat.cs); `MacChatWindow.RunCommandAsync` | Action `ct` through to the conversation calls; remove per-turn handler; keep the cancel branch `when ct.IsCancellationRequested` | [async actions](https://learn.microsoft.com/dotnet/standard/commandline/how-to-parse-and-invoke#asynchronous-actions) | Med |
| 20 | `--print-env` waits with a hand-built TCS + never-unsubscribed `CancelKeyPress` | `WaitForCtrlCAsync` (Program.cs) | `await Task.Delay(Timeout.InfiniteTimeSpan, ct)`, catch OCE, fall into teardown | [async actions](https://learn.microsoft.com/dotnet/standard/commandline/how-to-parse-and-invoke#asynchronous-actions) | Low |
| 21 | Health poll: hand-rolled `DateTime` deadline, no token | `WaitUntilHealthyAsync` (Program.cs) | Linked CTS + `CancelAfter(timeoutMs)` to `SendAsync` and `Task.Delay` | [cancel after period](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/cancel-async-tasks-after-a-period-of-time) | Low |
| 22 | `serve`/`connect`: Host `ConsoleLifetime` and System.CommandLine both own signals | `BuildServeCommand` (Program.cs); `ConnectCommand.Build` | Host owns shutdown: these commands invoke with `ProcessTerminationTimeout = null` | [host shutdown](https://learn.microsoft.com/dotnet/core/extensions/generic-host#host-shutdown) | Med |
| 23 | `forge mcp` ignores tokens (server and tool handler) (rides on #4) | `McpCommand.ServeAsync`; `BuildMcpCommand` | Host owns the server lifetime; tool handler passes its `ct` to `PipelineRunner.RunAsync`, catch excludes cancellation | [cancellation](https://learn.microsoft.com/dotnet/standard/threading/cancellation-in-managed-threads) | Med |

---

## Playbook observations (run 3)

1. **Lenses conflicted on composition shape.** The DI lens prescribed `Host.CreateApplicationBuilder`
   for the short-lived CLI root; the config lens prescribed `ConfigurationBuilder` with no host
   for the same commands. Run 2's DI lens prescribed `ServiceCollection`. Reconciled at D2 to the
   workload fit (CLI root: `ServiceCollection`; `forge mcp`: Host). **D2 needs a rule:** one
   composition decision per workload, applied across lenses.
2. **Impact labels need supervisor reclassification against the rule.** Config #6 was labelled
   `app:` for the JSON parser's error text and string binding — both are platform semantics →
   kept. Logging #11 was labelled `app:` for adding a stderr line — covered by the logging rule → kept.
3. **The broadened rule doubled the backlog**: 23 rows vs 11 in run 2. Cancellation is now mostly
   in Stage 2 (12 rows).
4. **Run-to-run variance:** run 2 reported the sync actions (#7 there); run 3's cancellation lens
   judged them out of scope ("no termination handling applies"). Run 3 found new rows run 2
   missed (binding generator, `ProcessTerminationTimeout`, serve/connect shutdown ownership).
5. **Dedupe across lenses:** DI #4 (MCP host) absorbed the logging lens's "MCP has no logging" row.

D1 durations (parallel): DI 2m32s · configuration 2m56s · logging 3m39s · cancellation 5m35s (≈5.5m wall-clock).
