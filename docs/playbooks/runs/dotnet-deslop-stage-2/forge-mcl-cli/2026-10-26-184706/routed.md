# Routed findings — run `2026-10-26-184706`

Findings that belong outside Stage 2 · Phase 1, each with its destination. Nothing dropped.
"Behavior change" is quoted from the lens.

## → Later stage (author territory): adopting the practice changes behavior

| Lens | Finding | Locations | Behavior change |
|---|---|---|---|
| DI | `forge mcp` (long-running stdio server) on a bare `ServiceCollection` → Generic Host + `AddMcpServer().WithStdioServerTransport()` | Commands/McpCommand.cs:40-96 | Ctrl-C/SIGTERM becomes graceful shutdown, exit 0 instead of a signal code; host may add stderr lines |
| DI | MCP web-search client from the host's factory | Commands/McpCommand.cs:85 | Depends on the row above |
| Logging | Aux chat client failure swallowed | Program.cs:831-835 | New `warn:` line lands on **stdout** — outside the logging exception (stderr routing lives in ForgeMission.Serve, out of scope) |
| Logging | `forge serve` banner via `Console.Error` | Program.cs:465-477 | Changes **existing** output: reformatted, moves to stdout — outside the logging exception |
| Config | Hand-rolled `~/.forge/config.json` loader → `ConfigurationBuilder` + binding generator | Services/ForgeConfig.cs:10-68 | JSON comments/trailing commas accepted; keys case-insensitive; different errors for `[]` and `null` |
| Config | Partial, late validation → `[OptionsValidator]` | Services/ForgeConfig.cs:12, 25-35; Commands/ForgeChat.cs:68-74 | exec/project: clean error instead of stack trace; bad endpoint fails earlier |
| Config | Config under `UserProfile` → `ApplicationData` | Services/ForgeConfig.cs:14-15 | File moves; existing configs ignored; error paths change |
| Config | Backlog #6 (STOPped at carve): env vars via `AddEnvironmentVariables` + options | Commands/PlatformLogin.cs; Services/ProviderClientBuilder.cs | Env keys become case-insensitive: lowercase variable names now override settings on Unix. Work stashed (forge-mcl stash "S2P1 #6") |
| Cancellation | Backlog #7 (STOPped at carve): sync actions → async `SetAction((result, ct) => …)` | Program.cs (registry login, logout, provider list); Commands/CleanCommand.cs | Ctrl-C mid-action no longer kills immediately (exit 130); the action finishes in the 2s window and exits with its own code. Work stashed (forge-mcl stash "S2P1 #7") |
| Cancellation | Implicit 2s `ProcessTerminationTimeout` → explicit 30s | Program.cs:51-56 | Process may live up to 30s after Ctrl-C; teardown completes |
| Cancellation | Top-level OCE→1 → per-action OCE→130 | Program.cs:62-65 | SIGTERM exits 130 instead of 143 |
| Cancellation | `SetAction` without token → with `ct`, flowed down | Program.cs (13 sites); Commands/* | Depends on the OCE→130 row, else Ctrl-C exits 1 |
| Cancellation | Health poll deadline → linked CTS | Program.cs:690-711 | 60s limit enforced strictly (was up to ~100s) |
| Cancellation | `forge claude` `Survive` handler → removed | Program.cs:722-738 | Teardown runs; waits up to 30s instead of 2s |
| Cancellation | `WaitForCtrlCAsync` → `Task.Delay(Infinite, ct)` | Program.cs:741-746 | SIGTERM tears down, exits 0 instead of 143 |
| Cancellation | Chat per-turn CTS + handler → action `ct` | Commands/ForgeChat.cs:282-301 | Depends on the 30s timeout row |
| Cancellation | `CancellationToken.None` in chat/project → flow `ct` | Commands/ForgeChat.cs:126-273; Commands/ForgeProject.cs:82-83 | Depends on the `ct` + OCE→130 rows |
| Cancellation | TUI session CTS → linked to action `ct` | Tui/ChatTui.cs:100 | SIGTERM restores the terminal, exits 0 instead of forced 143/130 |
| Cancellation | `_stopping` CTS reused forever → per-operation CTS | Services/ChatHandsAttachment.cs:19, 41-49, 103-125 | **Bug fix:** file ops after a cancel stop being silently dropped |
| Cancellation | `forge mcp` `RunAsync(None)` → `RunAsync(ct)` | Commands/McpCommand.cs:93-96 | Depends on the `ct` + OCE→130 rows |
| Cancellation | MCP tool handler drops its `ct` | Commands/McpCommand.cs:54, 85-86 | Client-cancelled calls stop the mission run |

## → Stage 1 (cleanup): missed by Stage-1 discovery

| Lens | Finding | Locations | Why Stage 1 |
|---|---|---|---|
| Config | Dead `FORGE_API_ENDPOINT` pass-through (nothing reads it since commit c6c0bdb) | Commands/ForgeChat.cs:99; Services/MacChatWindow.cs:11-110 | Dead-code removal, not a platform practice |

## → Stage 2 · Phase 2 (serialization)

| Source | Finding |
|---|---|
| Stage-1 STOP #3d | Hand-rolled JSON schema → STJ source-gen |

## → Later stage (author territory): Stage-1 STOPs

| Source | Finding |
|---|---|
| Stage-1 STOP #4 | Wire protocol → `Katasec.Forge.Client` (cross-repo) |
| Stage-1 STOP #7 | `forge run` mis-wired / `ForgeRun` wire-or-delete (product decision) |
| Stage-1 STOP #3b | Consolidate `run` + `exec` (public CLI surface) |

Stage-1 STOP #1 (DI composition root) is in this phase's backlog as row 1.
