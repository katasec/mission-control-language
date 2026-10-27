# Routed — run `2026-10-27-042106` (run 5)

Findings not carved in Phase 1, each with its destination.

| # | Finding | Lens | Destination | Why (stated change) |
|---|---|---|---|---|
| R1 | Remove dead `FORGE_API_ENDPOINT` relay into the Mac chat child (`MacChatWindow`, `ForgeChat`) | config | later stage | app: `FORGE_CHAT_WINDOW` format drops a field; child stops exporting the variable (dead-code removal missed by Stage 1) |
| R2 | `serve` startup banner → `[LoggerMessage]` Information | logging | later stage | app: stderr banner text changes to formatter lines |
| R3 | `mcp` startup banner → `[LoggerMessage]` | logging | later stage | app: stderr banner lines change |
| R4 | Top-level unhandled exception → `[LoggerMessage]` Critical | logging | later stage | app: `Unhandled exception: …` becomes a `crit:` record |
| R5 | `forge claude` Ctrl+C survival via `PosixSignalRegistration`; null termination timeout for `claude` | cancel | later stage | app: returns claude's exit code and always runs teardown (fixes container leak) — a bug fix |
| R6 | `WaitForCtrlCAsync` → scoped `PosixSignalRegistration` | cancel | later stage | app: SIGTERM exit 0 instead of 143 |
| R7 | `WaitUntilHealthyAsync` deadline via linked CTS | cancel | later stage | app: hung probe abandoned at 60 s instead of up to 100 s |
| R8 | `ExpertResolver.ResolveAll(warnings: Console.Error)` | logging | out of scope (ForgeMission.Core) | Core signature change |
| R9 | `ForgeServe.BuildApp` host / logging filters; `DockerCli` tokens | DI, logging, cancel | out of scope (ForgeMission.Serve / Core) | fix lands outside `{scope}` |
| R10 | `forge chat` crashes ("Unhandled exception: ConversationHostProtocolException", exit 1) on a non-JSON 500 from the conversation host; `ForgeChat.RunAsync` doesn't catch it (`project create` does) | row 4 carve | later stage | app: bug in our error handling |
| R11 | `forge project create` keeps `CancellationToken.None` into `ApplicationComposition`/`CreateChatProjectAsync`: Katasec.Forge.Client 0.9.3 converts OperationCanceledException into `ChatProjectAdmissionFailure` (Ctrl+C would become exit 1 + error) | row 18 STOP (partial) | later stage / Application owner (Katasec.Forge.Client) | app: cancellation semantics of a packaged dependency |
| R12 | `forge claude` exit code lost: `Environment.ExitCode = await RunWiredClaudeAsync(...)` overridden by the action's return 0 | row 18 carve | later stage | app: bug |
| R13 | PlatformLogin / DockerCli / DockerPrereqChecker / TryReadFile / TryBuildMissionServerAsync accept no token | row 18 carve | Phase 2 (endpoint/CLI idioms) or later | needs signatures beyond Phase 1's cancellation rows |
| R14 | `ForgeServe.BuildApp` (ForgeMission.Serve) registers ConsoleLifetime via `WebApplication.CreateSlimBuilder()`, so `serve`/`connect vscode` keep a second SIGINT/SIGTERM listener alongside System.CommandLine's token | row 20 STOP (partial) | Stage 2 run on `src/ForgeMission.Serve` (scope) | fix lands outside `src/ForgeMission.Cli`; Serve is shared with the runner |
| R15 | Login HTTP calls (`ExchangeCodeAsync`, `IssuePlatformKeyAsync`, `WhoAmIAsync`, `FetchMeAsync`) take no token | row 19 carve | joins R13 | signature change beyond row 19 |

## R16: an Anthropic credential reaches the OpenAI API (security, pre-existing; not a Stage 2 row)
On the live gate, three claude-through-forge tests failed with OpenAI `401 invalid_api_key` naming an `sk-ant-o…` key: a Claude credential was sent to api.openai.com. Present at Stage 1's baseline; it lies outside the CLI (provider/key resolution for mission steps in Serve/Core). Owner: whoever owns provider resolution. Fix: never send a key to a provider other than the one that issued it; fail fast instead.
