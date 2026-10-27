# Routed findings — run `2026-10-27-015904`

## → Later stage (author territory): the app's own behavior would change

| Lens | Finding | Locations | App behavior change |
|---|---|---|---|
| Logging | Unhandled exception printed by hand → `ILogger` Critical | top-level `catch` (Program.cs) | `Unhandled exception: …` reformatted to console-formatter output |
| Logging | MCP startup banner → `[LoggerMessage]` | `McpCommand.ServeAsync` | Existing stderr lines reformatted |
| Logging | `forge serve` banner → `[LoggerMessage]` | `BuildServeCommand` | Existing stderr lines reformatted |
| Cancellation | `forge claude` `Survive` handler: Ctrl+C in claude ends forge after 2s, kills its server | `RunWiredClaudeAsync` | Fixes our bug: forge waits for claude, then tears down and returns claude's exit code |
| Cancellation | TUI session CTS not linked | `ChatTui.RunAsync` | SIGTERM takes the app's quit path (exit 0) instead of forced 143 |
| Cancellation | Host `ConsoleLifetime` owns signals for **every** command | top-level invocation | Token-ignoring calls (`you>` `ReadLine`, `ReadKey`) would hang on Ctrl+C instead of exiting 130 — and the workload rule keeps short commands on System.CommandLine |

## → Stage 1 (cleanup)

| Finding | Why |
|---|---|
| Dead `FORGE_API_ENDPOINT` pass-through to the Mac child window (nothing reads it) | Dead-code removal |
| `TuiColourLiteralTests` hard-code the pre-Stage-1 `ForgeConfig.cs` path | Regression from Stage-1 #8 |

## → Stage 2 · Phase 2

| Source | Finding |
|---|---|
| Stage-1 STOP #3d | Hand-rolled JSON schema → STJ source-gen |

## → Later stage: Stage-1 STOPs (product / cross-repo)

Stage-1 STOP #4 (wire protocol → `Katasec.Forge.Client`), #7 (`forge run` / `ForgeRun`), #3b (consolidate `run` + `exec`).
Stage-1 STOP #1 (DI composition root) is backlog row 1.
