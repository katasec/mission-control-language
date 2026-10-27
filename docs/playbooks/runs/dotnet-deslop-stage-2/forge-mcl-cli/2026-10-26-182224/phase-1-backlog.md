# Stage 2 · Phase 1 — Composition — backlog

Run: `2026-10-26-182224` · Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `dotnet-deslop-stage-2`
Stage-1 run: `docs/playbooks/runs/dotnet-deslop/forge-mcl-cli/2026-10-25-084621`
Lenses (D1, parallel): `csharp-expert` · `dotnet-aot-compat`
Processed: D2 merge+dedupe (+ Stage-1 STOP #1) · D3 split · D4 sequence (foundation-first).

## Decision needed before Address

**Composition root shape.** `csharp-expert` prescribes either `Host.CreateApplicationBuilder`
or a single `ServiceCollection`. Recommendation: **`Host.CreateApplicationBuilder`** — one
platform facility gives DI + configuration + logging + lifetime/Ctrl-C, i.e. every Phase-1
area, owned by Microsoft. AOT: MS DI is AOT-safe; config binding uses the source generator.

## Sequence (D4)

- **A — Composition root** (enabler; everything registers through it): 1
- **B — Registrations** (ride on A): 2–7
- **C — Configuration + options** (registered through A): 8–9
- **D — Logging** (registered through A): 10–11
- **E — Cancellation** (token originates at the command action, then flows down): 12–19

## A — Composition root

| # | Issue | Locations | Practice to adopt | Sev | Lens | Status |
|---|---|---|---|---|---|---|
| 1 | No composition root: 4 ad-hoc containers + static classes (= Stage-1 STOP #1) | Services/CliHttpClients.cs:20-27; Commands/ForgeChat.cs:102-108; Commands/ForgeProject.cs:74-81; Commands/McpCommand.cs:40-96 | One container built in Program.cs (see decision); command actions resolve from it; root owns disposal | High | both | pending |

## B — Registrations

| # | Issue | Locations | Practice to adopt | Sev | Lens | Status |
|---|---|---|---|---|---|---|
| 2 | Static service locator `CliHttpClients`; provider never disposed | Services/CliHttpClients.cs:16-26; Commands/ForgeExec.cs:22; Commands/PlatformLogin.cs:40; Services/ProviderClientBuilder.cs:15 | Inject `IHttpClientFactory` from the root; delete the locator | Med | both | pending |
| 3 | `conversation-host` client registered twice; bearer baked into `DefaultRequestHeaders` | Commands/ForgeChat.cs:102-107; Commands/ForgeProject.cs:74-79 | Register once; auth via `DelegatingHandler` | Med | both | pending |
| 4 | Absolute URLs + `Bearer` header built by hand per request | Commands/ForgeExec.cs:64-68, 269-276, 323-329; Commands/PlatformLogin.cs:114-115, 138-139 | Typed client: `BaseAddress` from options, relative URIs, auth handler | Low | csharp-expert | pending |
| 5 | Web-search client newed per call; key from raw env | Services/ProviderClientBuilder.cs:11-16; Commands/McpCommand.cs:85; Program.cs:821 | `AddHttpClient<IWebSearch, GrokWebSearch>`, key from options | Low | csharp-expert | pending |
| 6 | MCP server on bare `ServiceCollection`, no host, `RunAsync(None)` | Commands/McpCommand.cs:40-41, 94-96 | Host + `AddMcpServer().WithStdioServerTransport()`; log to stderr | High | csharp-expert | pending |
| 7 | Raw `new HttpClient()` in health poll | Program.cs:687-711 | Factory client with configured timeout | Med | both | pending |

## C — Configuration + options

| # | Issue | Locations | Practice to adopt | Sev | Lens | Status |
|---|---|---|---|---|---|---|
| 8 | Hand-rolled config: manual `~/.forge/config.json` reader re-read on every `ApiEndpoint` access; scattered env lookups. **Probable bug:** `FORGE_API_ENDPOINT` never takes effect (resolver reads file only) | Services/ForgeConfig.cs:14-58; Commands/PlatformLogin.cs:20-38; Commands/ForgeExec.cs:20; Commands/ForgeChat.cs:97-99; Services/MacChatWindow.cs:11,78-82; Services/ProviderClientBuilder.cs:13-14; Program.cs:917-919, 1172 | One configuration pipeline (JSON file + env provider) bound once to `IOptions<T>`; `EnableConfigurationBindingGenerator` (AOT). Fixing layering = **behavior change** (env now overrides file) | Med | both | pending |
| 9 | Theme validation via hand-rolled exception at read site | Services/ForgeConfig.cs:25-35, 61 | `[OptionsValidator]` source gen + `ValidateOnStart()` | Low | csharp-expert | pending |

## D — Logging

| # | Issue | Locations | Practice to adopt | Sev | Lens | Status |
|---|---|---|---|---|---|---|
| 10 | No `ILogger`: diagnostics/banners via ~100 `Console.Error.WriteLine` | Program.cs:66-70, 465-477; Commands/McpCommand.cs:37-38; Commands/ConnectCommand.cs:60-64 (+ rest per lens) | `AddLogging` + console logger on stderr; `ILogger<T>`; `[LoggerMessage]` source gen. Keep `Console` for user-facing CLI output only | Med | both | pending |
| 11 | Exceptions swallowed/degraded with no log | Program.cs:833-834, 698-707; Commands/PlatformLogin.cs:303; Commands/ForgeExec.cs:104 | Log at Debug/Warning via `ILogger` before degrading | Low | csharp-expert | pending |

## E — Cancellation

| # | Issue | Locations | Practice to adopt | Sev | Lens | Status |
|---|---|---|---|---|---|---|
| 12 | Every `SetAction` discards System.CommandLine's `CancellationToken` | Program.cs:85,176,191,243,277,337,344,376,406,450,514,1074,1117; Commands/ForgeChat.cs:39; Commands/ForgeProject.cs:25; Commands/WebuiCommand.cs:35,109; Commands/AgentCommand.cs:37,112; Commands/ConnectCommand.cs:43 | `SetAction(async (result, ct) => …)`; pass `ct` to the first call | High | both | pending |
| 13 | Hand-rolled Ctrl-C via `Console.CancelKeyPress` (handler never unsubscribed) | Program.cs:724-746; Commands/ForgeChat.cs:283-299 | Use the action's `ct` (`Task.Delay(Infinite, ct)`, linked CTS per turn) | Med | both | pending |
| 14 | `CancellationToken.None` hard-coded through chat/project/mcp chains | Commands/ForgeChat.cs:143,192,218,247,262,265,273,282; Commands/ForgeProject.cs:82-83; Commands/McpCommand.cs:96 | Flow `ct`; keep `None` only for deliberate, commented cleanup | High | both | pending |
| 15 | `ForgeExec` async APIs take no `ct` | Commands/ForgeExec.cs:24-346 | Add `ct` param; pass to every async call | High | csharp-expert | pending |
| 16 | `PlatformLogin` async APIs take no `ct`; `Task.WhenAny`+`Delay` timeout leaks | Commands/PlatformLogin.cs:42-278, 193-205 | `ct` param; linked CTS + `CancelAfter`; `WaitAsync(ct)` | Med | both | pending |
| 17 | MCP tool handler gets `ct` but doesn't pass it on | Commands/McpCommand.cs:55, 85 | Pass `ct` to `PipelineRunner.RunAsync` | Med | both | pending |
| 18 | Timeouts not linked to caller token (download CTS; health-poll deadline loop) | Commands/ForgeExec.cs:75-78, 182, 220-224; Program.cs:687-710 | `CreateLinkedTokenSource(ct)` + `CancelAfter`; filter timeout vs caller cancel | Low | both | pending |
| 19 | TUI session CTS is a standalone root | Tui/ChatTui.cs:100 | Link to the command `ct` | Low | csharp-expert | pending |

## Parked (not Phase 1)

| Item | Why parked |
|---|---|
| Project-wide IL suppression `NoWarn IL3050;IL2104;IL3053` (Cli csproj:46-49) — dotnet-aot-compat, High | Build/AOT config → Phase 2 (project shape, serialization) |
| `[UnconditionalSuppressMessage]` around YamlDotNet in Core `LockFile.cs:26-34` — High | Outside `{scope}` (ForgeMission.Core); Phase 2 serialization |
| `Console.Error` passed as `TextWriter` into `ExpertResolver`/`ExpertLoader` (Program.cs:223, 942, 1140) — Med | Fix lives in Core library APIs, outside `{scope}` |
| `ApplicationComposition.Create` → `AddForgeApplication(...)` (Commands/ForgeChat.cs:138-143; ForgeProject.cs:81-82) — Med | `ApplicationComposition` comes from the `Katasec.Forge.Client` package → cross-repo |
| Stage-1 STOP #3d (JSON schema → STJ source-gen) | Phase 2 (serialization) |
| Stage-1 STOPs #4, #7, #3b | Author territory (cross-repo / product decisions) — not a platform practice |

---

## Playbook observations (from this discovery)

1. **No skill actually prescribes console-app DI/config/logging/cancellation.** `csharp-expert`
   found no installed specialist and used its own fallback guidance; `dotnet-aot-compat` only
   covers trim/AOT warnings (cited "none"). The rule "the skill prescribes" is weak for Phase 1.
2. `{repo}` needs an absolute path in prompts (same gap as Stage 1).
3. Lenses roam outside phase areas (AOT suppressions) and outside `{scope}` (Core) → parked.
4. Lenses offered two prescriptions (Host vs ServiceCollection) → the playbook has no rule for
   who decides when a skill gives options.

D1 durations: `dotnet-aot-compat` 1m49s · `csharp-expert` 2m16s (parallel; ~2m16s wall-clock).
