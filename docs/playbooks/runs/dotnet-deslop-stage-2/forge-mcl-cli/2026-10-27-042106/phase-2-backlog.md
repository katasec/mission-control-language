# Phase 2 backlog: App-type idioms & project shape · run `2026-10-27-042106` (run 5)

Repo `forge-mcl` · scope `src/ForgeMission.Cli` · branch `dotnet-deslop-stage-2` · base `0be6a51` · playbook hub `d28d487`
Lenses: project layout + build (7) · CLI idioms (16) · serialization (5) · data access (4) · trim/AOT (3) → 35 findings
→ **26 rows**: 6 merged, 18 routed (R17–R34 in `routed.md`). No open questions; the rules settled every choice.
Stage-1 run STOPs: none in Phase-2 areas. Phase-1 routes picked up: R6 (row 4), R7 (row 3), R13 CLI part (rows 1, 2, 11, 19), R15 (row 11).
Settled by the rules, not questions:
- MCP schema: `Utf8JsonWriter`, not a `JsonObject` DOM. It has fewer concepts, the repo already uses it in `ConnectCommand`, and it needs no reflection.
- Row 10 must keep `forge claude` returning 0. The exit-code fix is R12, author territory.

Files are under `src/ForgeMission.Cli/`. `P` = Program.cs.
Build-config rows (14–17) must also prove themselves with `dotnet publish -c Release` (Native AOT). Verify's build does not run ILC.

## Lane 1: main checkout (sequential)

### Group A: Program.cs actions (one implementer)
| # | Sev | Issue | Locations | Practice | Reference | Impact | Files |
|---|---|---|---|---|---|---|---|
| ~~1~~ ✅ c85aa90 | Low | `TryReadFile` takes no token; its catch-all turns cancellation into Die (exit 1) | `TryReadFile`; callers init, validate (`(r, _)`), mcp, `TryBuildMissionServerAsync` | `CancellationToken` param → `File.ReadAllTextAsync`; catch `when (ex is not OperationCanceledException)`; thread action tokens | SCL how-to-parse-and-invoke "Asynchronous actions" | platform: Ctrl+C during read → 130 | P |
| ~~2~~ ✅ f928397 | Low | `TryBuildMissionServerAsync` and connect's `buildMissionServer` seam take no token | `TryBuildMissionServerAsync`; serve, claude, root wiring; `ConnectCommand.Build`/`BuildVsCodeCommand` Func | Add token param + to the Func seam; forward to `TryReadFile` | same | platform | P, ConnectCommand.cs |
| ~~3~~ ✅ 1b429fa | Low | Health poll hand-rolls a `DateTime.UtcNow` deadline, ignores token (R7) | `WaitUntilHealthyAsync` | Linked CTS + `CancelAfter(60s)`; pass to `SendAsync`/`Task.Delay`; expiry → false | Cancellation in managed threads (CancelAfter) | platform | P |
| ~~4~~ ✅ 8decff2 | Med | `--print-env` hand-rolls a Ctrl+C wait (R6) | `WaitForCtrlCAsync`; claude action | `Task.Delay(Infinite, actionToken)`, OCE on that token → return normally; teardown unchanged | how-to-parse-and-invoke | platform: SIGTERM also ends it | P |
| ~~5~~ ✅ 713ed09 | Low | In-process server for `forge claude` stopped but never disposed | claude action teardown | `await app.StopAsync(); await app.DisposeAsync();` | Generic Host shutdown | none | P |
| ~~6~~ ✅ d027b1b | Low | Port-in-use detected by message text | `BuildServeCommand`; `ConnectCommand.BuildVsCodeCommand` | catch `AddressInUseException` | API AddressInUseException | none | P, ConnectCommand.cs |
| ~~7~~ ✅ 6f55190 | Low | async lambda ending `await Task.CompletedTask` | `BuildListCommand` | non-async delegate returning `Task.CompletedTask` | how-to-parse-and-invoke | none | P |
| ~~8~~ ✅ 2235bdf | Low | Fixed provider-name set offers no completions | `BuildProviderScaffoldCommand` `nameArg` | `CompletionSources.Add(ProviderCatalog.Entries.Select(e => e.Name))` | Tab completion | platform: tab completion | P |
| ~~9~~ ✅ a1a5adf | Low | Defaults hand-rolled with `??`; help shows none | agent start/stop, webui start, `ResolveMission`, `--experts`, publish dir, serve agent (not `chat --project`, `project create folder`) | `DefaultValueFactory` with the same literal; keep `GetFullPath` in action | SCL syntax "Default values" | platform: help shows defaults | P, AgentCommand.cs, WebuiCommand.cs |
| ~~10~~ ⛔ routed R36 | Low | Deliberate exits travel as `ForgeExitException` instead of the action's int | `Die`, `TryParse`, `TryLoadExperts`, `TryBuildMissionServerAsync`, mcp action, AgentCommand, WebuiCommand, `EnsureInitializedAsync` | `Task<int>` `SetCancellableAction` overload, return the code; top-level catch for unexpected only. `forge claude` still returns 0 (R12 is author territory) | how-to-parse-and-invoke "Exit codes" | none | P, AgentCommand.cs, WebuiCommand.cs |

### Group B: PlatformLogin.cs
| ~~11~~ ✅ 785d26b | Med | Login HTTP calls take no token (R13, R15); whoami discards it | `WhoAmIAsync`, `FetchMeAsync`, `IssuePlatformKeyAsync`, `ExchangeCodeAsync`, `RunAsync`; whoami action `(_, _)` | Accept the action token; pass to `SendAsync`/`PostAsync`/`ReadAs*Async` | how-to-parse-and-invoke; CA2016 | platform: Ctrl+C cancels the request | PlatformLogin.cs, P |
| ~~12~~ ✅ 95650b0 | Low | Success body buffered as string, then deserialized | `FetchMeAsync`, `IssuePlatformKeyAsync` | `ReadAsStringAsync` only on failure; `ReadFromJsonAsync(PlatformLoginJsonContext…)` on success | HttpContentJsonExtensions | none | PlatformLogin.cs |

### Group C: ConnectCommand.cs
| ~~13~~ ✅ b4b8bb7 | Med | `settings.json` rewrite truncates before writing | `ConnectCommand.TryWriteVsCodeSettings` | Existing file: temp in `.vscode/` + `File.Replace(temp, path, null)`; new file: `File.Create` | File.Replace | platform: atomic replace; same JSON | ConnectCommand.cs |

## Lane 2: worktree `forge-mcl-p2-lane2` (parallel with lane 1; cherry-picked in after row 13)

### Group D: build config (one implementer)
| ~~14~~ ✅ cba3bd2 | Low | `forge` exe packable by default | csproj | `<IsPackable>false</IsPackable>` | NuGet pack target | none | csproj |
| ~~15~~ ✅ 1376b77 | Med | Native linker args via a hand-written target on ILC-internal `LinkNative`, gated on host OS; Homebrew paths hard-coded | `AddMacNativeAotLinkerArgs` | `<LinkerArg>` items in an `ItemGroup` on `$(RuntimeIdentifier.StartsWith('osx'))`; drop each arg the publish proves unneeded | Native AOT interop; prerequisites | none | csproj |
| ~~16~~ ✅ a014f23 | Low | P/Invoke via `DllImport` | `TypeAhead.Flush` | `[LibraryImport]` on a `partial` class | P/Invoke source generation | none | Tui/TypeAhead.cs |
| ~~17~~ ◐ 5c05bc9 partial + R42 | High | IL3050/IL2104/IL3053 suppressed project-wide (`NoWarn`, undocumented `IlcSuppressWarnings`) | csproj | Remove both; `TrimmerSingleWarn=false`; fix each warning at source, or `[UnconditionalSuppressMessage]` at the smallest scope | Fixing AOT warnings; IL2104; IL3053 | none | csproj (+ sources the warnings name) |

### Group E: serialization + data (one implementer)
| ~~18~~ ✅ 1da994e | Med | MCP input schema hand-built with StringBuilder; `EscapeJson` incomplete; `JsonDocument` undisposed | `JsonSchema.ToJsonElement`/`EscapeJson` | `Utf8JsonWriter` → `ArrayBufferWriter`; `using` JsonDocument + `RootElement.Clone()`; delete `EscapeJson` | use-utf8jsonwriter; use-dom (IDisposable) | platform: full JSON escaping | McpCommand.cs |
| ~~19~~ ✅ 2aad1bf | Low | Event stream and HTTP calls ignore the token (R13) | `ForgeExec.RunAsync` `SendAsync`/`ReadAsStreamAsync`/`ReadLineAsync` | pass the token it already holds | how-to-parse-and-invoke | platform: Ctrl+C cancels the stream read | ForgeExec.cs |
| ~~20~~ ✅ a488a88 | Med | Saving an existing file truncates in place | `EditFile.Save` | `!IsNew`: temp in the same folder + `File.Replace`; new: plain write | File.Replace | platform: atomic replace | Tui/EditFile.cs |
| ~~21~~ ✅ 8c727d0 | Low | Full file array built only to count | `CleanCommand` action | `Directory.EnumerateFiles(…).Count()` | Directory.EnumerateFiles | none | CleanCommand.cs |

## After the merge (sequential; they touch every file)

### Group F: SCL output writers (split by owner)
| ~~22~~ ⛔ R43 | Low | Actions write to `Console.Out/Error`, not SCL's invocation writers | Program.cs actions, `Die`, `ReportDiagnostic` | `parseResult.InvocationConfiguration.Output/Error`, passed down as `TextWriter` (as `ForgeProject.RunCoreAsync` does) | how-to-configure-the-parser "Standard output and error" | none | P |
| ~~23~~ ⛔ R43 | Low | same | CleanCommand, ConnectCommand, PlatformLogin, ForgeProject | same | same | none | those files |
| ~~24~~ ⛔ R43 | Low | same | `ForgeExec.RunAsync` | same | same | none | ForgeExec.cs |
| ~~25~~ ⛔ R43 | Low | same (non-TUI lines only; the TUI keeps `RawStdout`) | `ForgeChat.BuildCommand`/`RunAsync` | same | same | none | ForgeChat.cs |

### Group G: layout
| ~~26~~ ✅ 7724552 | Low | Namespaces don't match folders; two types global | Commands/, Contracts/, Services/; `ProviderClientBuilder`, `WebSearchOptions` | file-scoped folder namespaces; IDE0130 enforced | IDE0130 | none | all (+ tests' usings) |

## Re-discover (17:30): 3 lenses clean (layout/build, serialization, trim/AOT); 7 new findings
Rows from re-discover (carved 18:27–18:32):
| ~~27~~ ✅ 40b9aba | Low | Async-only streams opened synchronously | `ForgeExec.Sha256Async`, `UploadLocalInputAsync`, `DownloadUrlInputAsync` | `FileStreamOptions { Options = FileOptions.Asynchronous }` | Async file access | none |
| ~~28~~ ✅ 3b70a4b | Low | `ResolveMission` fallback builds the whole `*.mcl` array | Program.cs | `EnumerateFiles(...).Take(2)` | Directory.EnumerateFiles | none |
| ~~29~~ ✅ 6554ddf | Low | `ReportExpertDiagnostic` reads the whole file and splits by hand | Program.cs | `File.ReadLines(...).ElementAtOrDefault` | File.ReadLines | platform: a lone \r ends a line |
Routed (app impact → later stage): R44 agent-file `AcceptExistingOnly` (message changes) · R45 provider `AcceptOnlyFromAmong` (case-sensitive) · R46 port range validators · R47 `connect --port` help shows the default twice.
