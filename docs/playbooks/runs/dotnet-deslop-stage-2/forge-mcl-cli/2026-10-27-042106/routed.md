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

## Phase 2 routes (discovery, 15:15)
Confirmed again by the CLI-idioms lens: R12 (claude exit code, author territory) and R5 (an SCL 2 s forced exit leaks the container: `ProcessTerminationTimeout = null` for `claude`; changes app behavior → author territory).

| # | Finding | From | Destination | Why |
|---|---|---|---|---|
| R17 | `registry login --token` not `Required`; null token saved | CLI lens | later stage | app: becomes a parse error |
| R18 | `expert init` PascalCase check hand-rolled; empty name crashes | CLI lens | later stage | app: message format changes |
| R19 | `HasJsoncComments` hand scanner → `Utf8JsonReader` comments | serialization lens | later stage | app: a malformed-before-comment file takes the die path |
| R20 | NDJSON read line by line → `DeserializeAsyncEnumerable` | serialization lens | later stage | app: a malformed line fails the run (today it is skipped) |
| R21 | Artifact download writes straight to the output path | data lens | later stage | app: no partial file left; an old file survives |
| R22 | TargetFramework/ImplicitUsings/Nullable duplicated in 9 projects | layout lens | repo-level (root Directory.Build.props) | fix lands outside scope |
| R23 | Inline `PackageReference` versions → Central Package Management | layout lens | repo-level | outside scope |
| R24 | YamlDotNet reflection + IL3050 suppressions in Core | AOT + layout lenses | Stage 2 on ForgeMission.Core | outside scope |
| R25 | `CreateEmptyApplicationBuilder` for size | AOT lens | later stage | product decision; drops env-var provider |
| R26 | `DockerCli`/`DockerPrereqChecker` take no token (R13 Docker part) | CLI lens | Stage 2 on ForgeMission.Docker | signature outside scope |
| R27 | `forge serve` endpoint idioms | CLI lens | Stage 2 on ForgeMission.Serve | outside scope |
| R28 | `FindFreePort` duplicated (Program + PlatformLogin); `TcpListener` undisposed | CLI lens | Stage 1 re-run | duplication |
| R29 | Hand-rolled `IsOnPath`, duplicate `ReportDiagnostic`/`ReportExpertDiagnostic`, ANSI helpers | CLI lens | Stage 1 re-run | duplication |
| R30 | `"mcl.lock"` path built in 5+ places | data lens | Stage 1 re-run | duplication |
| R31 | Unused `using System.Text.Json` in Program.cs | serialization lens | Stage 1 re-run | hygiene |
| R32 | README links files at old paths | layout lens | Stage 1 re-run (docs) | doc drift |
| R33 | Program.cs holds registration + handlers | layout lens | Stage 1 re-run (structure) | relocation |
| R34 | `CredentialStore`/`LockFileIO`/`ForgeCache` atomic writes and secret storage | data lens | Stage 2 on Core/MissionRegistry | outside scope |

Not routed: `ValidateOnStart` for `ForgeSettings` was settled in Phase 1, row 3. It is left off on purpose so an unknown theme doesn't stop non-chat commands.
| R35 | `MissionFileResolutionTests` pins a private mirror of the old null-tolerant `ResolveMission`, which row 9 removed from product code. It tests code that exists only in the test. | row 9 Check | Stage 3 (test reduction) | test deletion belongs to Stage 3; `DefaultPathTests` now covers the behavior end to end |
| R36 | Row 10 (`ForgeExitException` → int returns) STOPPED. Two paths would change output: MCP per-call `BuildRunners` failure text, and expert diagnostics stopping at the first error. Keeping both needs sentinel returns in ~8 helpers, a stop flag in 4 loops and a temporary print-only bridge. Proposed 6-step split by command is in run-log row 10. | row 10 carve | later stage (author decision on the MCP per-call failure text; then the split) | no Microsoft form keeps output without a bridge (no net complexity) |
| R37 | MCP per-call provider failure: the tool result is the SDK's generic "An error occurred invoking 'X'.", stderr leaks a `ForgeExitException` stack, and the intended `callRunners is null` message in McpCommand.cs is dead code | row 10 probe | later stage (author) | visible behavior choice |
| R38 | Suspected: `validate` exits 0 when `TryValidate` returns false after contract errors without a file path. Not verified. | row 10 reading | later stage (verify, then fix) | possible app bug |
| R39 | `ld: warning: building for macOS-12.0, but linking with dylib … built for newer version` ×5 (Homebrew openssl/brotli) escape the zero-warning gate; `-L/opt/homebrew/lib` assumes Apple-silicon Homebrew (Intel is `/usr/local/lib`) | row 15 | later stage (build/release owner) | deployment-target or static-link decision; not a platform-practice swap |
| R40 | Nine CLI test files each carry their own `LoadForge()`; the shared `ForgeText.Forge` already does it | row 16 | Stage 3 (test reduction) | test dedup |
| R41 | Duplicate mission params (`M(a, a)`) are accepted; the MCP schema's `required` then repeats a name (JSON Schema expects unique items) | row 18 | later stage (parser) | behavior change |
| R42 | Row 17 STOP (rest): the CLI's `NoWarn` IL3050/IL2104/IL3053 stays. Sources: 4 × IL3050 in ForgeMission.Core (YamlDotNet builders in static field initializers; `[UnconditionalSuppressMessage]` on the field doesn't cover the `.cctor` ILC reports → move the suppression onto a static method or ctor), and IL2104/IL3053 summarising 36 YamlDotNet 18.0.0 warnings (→ YamlDotNet's static source-generated context). Once Core is fixed, the CLI can drop IL3050 from NoWarn. | row 17 carve | Stage 2 on ForgeMission.Core (extends R24) | outside scope; no documented per-assembly suppression narrower than NoWarn |
| R43 | Rows 22–25 (actions write through SCL's `InvocationConfiguration.Output/Error`) routed: no consumer. Nothing sets non-default writers (the only InvocationConfiguration sets EnableDefaultExceptionHandler), and the harness reads the real process streams. A partial adoption (row 22's carve) left two output conventions; the full one means threading a writer into ~30 helpers and 3 delegate seams. | row 22 Check FAIL | later stage, only if an in-process host or test needs redirected output | no net complexity; the platform form needs a consumer first |
