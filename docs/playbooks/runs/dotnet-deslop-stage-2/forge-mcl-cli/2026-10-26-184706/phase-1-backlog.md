# Stage 2 · Phase 1 — Composition — backlog

Run: `2026-10-26-184706` · Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `dotnet-deslop-stage-2`
Stage-1 run: `docs/playbooks/runs/dotnet-deslop/forge-mcl-cli/2026-10-25-084621`
Lenses (D1, parallel, one per area, each `csharp-expert` + `microsoft-docs`): DI + `HttpClient` registration · configuration + options · logging · cancellation
Processed: D2 merge+dedupe+route · D3 split · D4 sequence. Routed findings → [routed.md](routed.md).

Only rows whose behavior impact is **none** are here (logging may add stderr diagnostics —
see the Stage-2 core rules). 28 lens rows → 11 backlog rows, 17 routed.

## Sequence (D4)

- **A — Composition root** (enabler): 1
- **B — Registrations** (ride on A): 2–4
- **C — Configuration + options**: 5–6
- **D — Logging**: 10–11
- **E — Cancellation**: 7–9

## A — Composition root

| # | Issue | Locations | Practice to adopt | Reference | Sev | Status |
|---|---|---|---|---|---|---|
| 1 | Static service-locator `CliHttpClients` builds its own container in a static initializer, never disposed; one of four ad-hoc containers (= Stage-1 STOP #1) | Services/CliHttpClients.cs:10-27; consumers Commands/ForgeExec.cs:22, Commands/PlatformLogin.cs:40, Services/ProviderClientBuilder.cs:15 | One composition root in Program.cs for the short-lived CLI: a single `ServiceCollection` registering named clients `forge-exec` (infinite), `forge-login` (30s), `forge-websearch` (3m); `await using` provider; pass `IHttpClientFactory` into actions, `CreateClient(name)` per operation | [DI basics](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/basics) · [IHttpClientFactory](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory) | Med | pending |

## B — Registrations

| # | Issue | Locations | Practice to adopt | Reference | Sev | Status |
|---|---|---|---|---|---|---|
| 2 | `conversation-host` named client registered twice, each in a throwaway container | Commands/ForgeChat.cs:102-111; Commands/ForgeProject.cs:74-82 | Register once in the root (#1); bearer from credential in the `AddHttpClient(name, (sp, c) => …)` delegate; credential check stays before the first `CreateClient` | [Named clients](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory) | Low | pending |
| 3 | `webui` health probe uses a hand-rolled `new HttpClient()` | Program.cs:687-710 (called :560) | Named client `forge-health` in the root; `CreateClient` in `WaitUntilHealthyAsync`; keep default 100s timeout, no BaseAddress | [IHttpClientFactory](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory) | Low | pending |
| 4 | Long-running `forge serve` captures one factory client for its lifetime (stale DNS) | Program.cs:821 → Services/ProviderClientBuilder.cs:15 | `forge-websearch` gets `ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) })` | [Factory troubleshooting](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory-troubleshooting) | Med | pending |

## C — Configuration + options

| # | Issue | Locations | Practice to adopt | Reference | Sev | Status |
|---|---|---|---|---|---|---|
| 5 | Config re-read on every property access (`ForgeExec.ApiEndpoint` up to 4×/run; chat parses twice) | Commands/ForgeExec.cs:20, 64, 77, 269, 323; Commands/ForgeProject.cs:77; Commands/ForgeChat.cs:69, 105 | Load once at the start of the action; pass as `IOptions<ForgeCliOptions>` (`Options.Create(bound)`) | [Options pattern](https://learn.microsoft.com/dotnet/core/extensions/options) | Med | pending |
| 6 | Login/web-search settings pulled straight from `Environment.GetEnvironmentVariable` with inline defaults, re-read per access | Commands/PlatformLogin.cs:18-38; Services/ProviderClientBuilder.cs:13-14 | `AddEnvironmentVariables()` provider; bind `PlatformLoginOptions` keeping names via `[ConfigurationKeyName("FORGE_AUTH_AUTHORITY")]` etc.; defaults as initializers; binding source generator (AOT) | [Env-var provider](https://learn.microsoft.com/dotnet/core/extensions/configuration-providers#environment-variable-configuration-provider) · [Binding generator](https://learn.microsoft.com/dotnet/core/extensions/configuration-generator) | Low | pending |

## D — Logging

| # | Issue | Locations | Practice to adopt | Reference | Sev | Status |
|---|---|---|---|---|---|---|
| 10 | `forge mcp` stdio server has no logging providers; MCP SDK logs go to `NullLoggerFactory` | Commands/McpCommand.cs:40-89 | `services.AddLogging(b => b.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace).SetMinimumLevel(LogLevel.Warning))` on the same collection — stdout stays the JSON-RPC channel | [Logging overview](https://learn.microsoft.com/dotnet/core/extensions/logging/overview) · [LogToStandardErrorThreshold](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.console.consoleloggeroptions.logtostandarderrorthreshold) | Med | pending |
| 11 | MCP tool-call handler turns every exception into `ex.Message` with no log | Commands/McpCommand.cs:81-82 | Inject `ILogger`; `[LoggerMessage(Level = LogLevel.Error)]` source-generated method with `ex`; same `CallToolResult` returned. Rides on #10 | [Logging source generation](https://learn.microsoft.com/dotnet/core/extensions/logging/source-generation) | Med | pending |

## E — Cancellation

| # | Issue | Locations | Practice to adopt | Reference | Sev | Status |
|---|---|---|---|---|---|---|
| 7 | Sync and async actions mixed; sync actions get no termination token | Program.cs:328, 351, 1053; Commands/CleanCommand.cs:18 | Async `SetAction((result, ct) => …)` returning `Task<int>`/`Task` | [Async actions](https://learn.microsoft.com/dotnet/standard/commandline/how-to-parse-and-invoke#asynchronous-actions) | Low | pending |
| 8 | `forge exec`: 30s download timeout uses an unlinked CTS; any `TaskCanceledException` reported as network failure | Commands/ForgeExec.cs:69-78, 182-224 | `ct` parameter; `CreateLinkedTokenSource(ct)` + `CancelAfter`; catches filtered `when (!ct.IsCancellationRequested)` | [Coalesce tokens from timeouts](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/coalesce-cancellation-tokens-from-timeouts) | Med | pending |
| 9 | Sign-in wait: `Task.WhenAny(GetContextAsync(), Task.Delay(5 min))`, timer never cancelled | Commands/PlatformLogin.cs:193-201 | Linked CTS + `CancelAfter(5 min)`; `GetContextAsync().WaitAsync(token)`; same timeout message | [Coalesce tokens from timeouts](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/coalesce-cancellation-tokens-from-timeouts) | Low | pending |

---

## Playbook observations (run 2)

1. **Focus fixed.** Every finding is inside `{scope}` and inside its lens's area. Run 1 had
   6 off-target items.
2. **Workload rule worked.** The DI lens prescribed `ServiceCollection` for the CLI root and
   the Generic Host for `forge mcp` on first pass — no either/or.
3. **Grounding worked.** Every row cites a Microsoft Learn page.
4. **Strict behavior retention routed logging away entirely** (adding `ILogger` adds output).
   Resolved: logging is a canonical capability, so the rule now allows new stderr diagnostics
   (never stdout, never changing existing output). 2 of 4 logging rows now qualify. 12 of 15
   cancellation rows remain routed (flowing the token changes exit codes / shutdown timing).
5. **Dependency cascades.** Several rows are behavior-neutral on their own but depend on a
   routed row (e.g. flowing `ct` needs the OCE→130 mapping). The playbook should state:
   a row that depends on a routed row is routed too.
6. **Lenses found real bugs** (TUI `_stopping` CTS stuck cancelled; `forge claude` orphans
   claude and leaks containers on Ctrl-C; teardown cut off at 2s). They need a stage to own them.
7. **Stage 1 missed dead code** (`FORGE_API_ENDPOINT` plumbing) — routed back to Stage 1.
8. Still no installed specialist for console DI/config/logging/cancellation; every lens used
   `csharp-expert`'s fallback, grounded by `microsoft-docs` (which used `npx @microsoft/learn-cli`
   when the Learn MCP tools weren't available).

D1 durations (parallel): DI 3m09s · configuration 3m29s · logging 3m25s · cancellation 6m00s (≈6m wall-clock).
