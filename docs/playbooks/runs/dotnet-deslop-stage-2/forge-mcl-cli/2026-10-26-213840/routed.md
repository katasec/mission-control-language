# Routed findings — run `2026-10-26-213840`

Findings that belong outside Stage 2 · Phase 1, each with its destination. Nothing dropped.

## → Later stage (author territory): the app's own behavior would change

| Lens | Finding | Locations | App behavior change |
|---|---|---|---|
| Config | `FORGE_API_ENDPOINT` would start overriding `api.endpoint` once both join one pipeline | `ForgeChat.RunAsync`; `MacChatWindow.Endpoint/RestoreContext`; `ForgeExec.ApiEndpoint` | An env var that has no effect today would take effect (the dead plumbing also → Stage 1) |
| Logging | MCP startup diagnostics via `Console.Error` → `ILogger` | `McpCommand.ServeAsync` | Existing stderr lines reformatted (`info: …[EventId]`) |
| Logging | Aux chat client failure swallowed → `app.Logger` warning | `TryBuildMissionServerAsync` (Program.cs) | New line on **stdout** (slim builder default; stderr routing lives in ForgeMission.Serve) |
| Logging | `forge serve` banner via `Console.Error` → `app.Logger` | `BuildServeCommand` (Program.cs) | Banner moves to stdout and is reformatted |
| Cancellation | `forge claude` `Survive` handler: forge exits 130 after 2s while `claude` still runs; teardown skipped | `RunWiredClaudeAsync`; `BuildClaudeCommand` teardown | Fixes our bug toward documented intent: forge stays alive until `claude` exits, then returns its exit code |
| Cancellation | TUI session CTS not linked to the action token | `ChatTui.RunAsync` (Tui/ChatTui.cs) | SIGTERM takes the app's quit path (exit 0, terminal restored) instead of forced 143 |

## → Stage 1 (cleanup)

| Finding | Why |
|---|---|
| Dead `FORGE_API_ENDPOINT` pass-through to the Mac child window (nothing reads it) | Dead-code removal |
| `TuiColourLiteralTests` hard-code the pre-Stage-1 `ForgeConfig.cs` path | Regression from Stage-1 #8 (folder layout) |

## → Stage 2 · Phase 2

| Source | Finding |
|---|---|
| Stage-1 STOP #3d | Hand-rolled JSON schema → STJ source-gen (serialization) |

## → Later stage: Stage-1 STOPs (product / cross-repo)

| Source | Finding |
|---|---|
| Stage-1 STOP #4 | Wire protocol → `Katasec.Forge.Client` (cross-repo) |
| Stage-1 STOP #7 | `forge run` mis-wired / `ForgeRun` wire-or-delete |
| Stage-1 STOP #3b | Consolidate `run` + `exec` (public CLI surface) |

## Outside `{scope}` (noted by lenses, not routed to a stage yet)

- `ExpertResolver`/`ExpertLoader` take `TextWriter` for diagnostics (ForgeMission.Core).
- `ForgeServe.BuildApp` console logger stderr threshold (ForgeMission.Serve).
- `DockerCli`/`DockerPrereqChecker` take no cancellation token (ForgeMission.Docker).
