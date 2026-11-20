# forge-mcl — Phase 1 second re-discovery backlog (structure & idiom)

Run: Phase 79 step 7 · repo `forge-mcl` · scope `src/` (Parser `Generated/` excluded) · base `main`
`b82368f` (v0.42.0) · lenses: `csharp-expert` (idiom), `ownership-reviewer`, `code-style-reviewer` ·
baseline: build 0 warnings, tests 650 passed / 3 skipped / 0 failed.

Phase 1 is **not clean**. The three lens reports are merged and deduplicated here, ordered
dependency first and then by severity. Rows marked ✓ were re-checked by the supervisor against the
code at `b82368f`; every other row is as the lens reported it. **(D)** = delete; no caller in
forge-mcl, forge-runner, forge-client or forge-conversations (checked by grep). The design items
from the [first re-discovery](phase-1-rediscover-backlog.md#design-items-owner-decision-first-not-carved)
are still open and are not repeated.

Dropped after checking: "`HttpExpertRunner`'s `HttpClient?` parameter has no caller" — the
`HttpExpertRunnerTests` use it.

## Carves (no behaviour change)

| # | Issue | Locations | Lenses |
|---|---|---|---|
| 1 | Reserved context keys (`output`, `feedback`, `attempt`, `max_loops`, `history`, `tools`, `tool_calls`) are literals at ~30 sites; `IsReserved` and `ExpertLoader.RuntimeKeys` are two more lists. One Core runtime-keys type | PipelineRunner.cs:154,177-184,237,296,322,600-632,989; DirectExpertRunner.cs:80,104,129,137; Rule/JsonExtract/ExecExpertRunner; ContextBuilder.cs:17; ExpertLoader.cs:236-246; DurableMissionInputPolicy.cs:46-50 | all three |
| 2 | Step status `"pass"`/`"fail"` literals at 13+ sites: one owner constant on `StepEnvelope` (wire format unchanged) | StepEnvelope.cs:8; PipelineRunner.cs:488; ExecExpertRunner.cs; JudgeVerdict.cs:42,45; Onnx/Rule/DirectExpertRunner | idiom+style |
| 3 | File-name and path literals with no owner: `"expert.md"` (7 sites, beside `DefaultExpertsDir`); the `~/.forge` root built in 4 places | ExpertResolver.cs:88; SourceResolver.cs:24; ExpertLoader.cs:54,71,259; MissionDevCommands.cs:200; ForgeCache.cs:6-13; CredentialStore.cs:47-50; RegistryCredentialStore.cs:8-10; ForgeSettings.cs:14-15 | style |
| 4 | Other literals with an owner: capability names `"file"`/`"terminal"`; size-limit messages that hardcode their constant; exec stdin checked against `MaxOutputBytes`; `33554432` repeats `OciMissionPuller.MaxBundleBytes`; PlatformLogin's 5-minute timeout and `"(unknown user)"` | CapabilityDispatcher.cs:60-63; CapabilityAuthorizationPolicy.cs:37,39; WorkspaceFileProvider.cs:7; ExecExpertRunner.cs:12,33-34; DurableMissionPackageValidator.cs:73-75; OciMissionPuller.cs:38; OciExpertPuller.cs:9-10,24; PlatformLogin.cs:178,185,270-276 | style+idiom |
| 5 | Mission validation (`Validate`, cycle detection, context-key contracts, lines 85-351) lives in `ExpertLoader`, burying `ParseFile`/`ParseContent`: move it to its own Core class | ExpertLoader.cs:85-389 | style+idiom |
| 6 | `PipelineToolPause.cs` holds 12 types (public pause contract plus internal checkpoint, log, JSON context, codec, constants): one file per concern | PipelineToolPause.cs:9-167 | idiom |
| 7 | Pipeline → step list (step plus parallel) flattened twice in Core: one Parser AST helper (forge-client's copy follows separately) | ExpertLoader.cs:343-349; DurableMissionInputPolicy.cs:37-42 | ownership |
| 8 | `ExpertResolver`: shadow check computed twice (two `ResolveLockPath` calls); `"local"`/`"cache"` labels as bare strings | ExpertResolver.cs:51-68,90,97 | idiom |
| 9 | **(D)** ✓ `CapabilityPolicySource` enum, `CapabilityAuthorizationRule.Source`, `CapabilityAuthorizationPolicy.Rules` | CapabilityAuthorizationPolicy.cs:3-11,27 | idiom |
| 10 | Dead code after `[DoesNotReturn]` `CliDiagnostics.Die`; dead null check on non-nullable `registry`; Try-methods without `[NotNullWhen(true)]` (`TryGetProvider`, `TryRead` `= null!`) | CleanCommand.cs:27; McpCommand.cs:52; HostedMissionCommands.cs:77; CliHelpers.cs:18; CredentialStore.cs:58-59; CapabilityRegistry.cs:28; CapabilityDispatcher.cs:156,175; PipelineToolPause.cs:104-110 | style+idiom |
| 11 | `ExecExpertRunner` idiom: `WriteInputAsync` returns a dummy `""` to fit `WhenAll`; one-line `CreateOutputDirectory` wrapper | ExecExpertRunner.cs:94,133-136,160-168 | style+idiom |
| 12 | Nesting > 2 | GrokWebSearch.cs:76-94; ExpertLoader.cs:321-335 | style |
| 13 | Disposal gaps: `Process.Start` result never disposed; `FormUrlEncodedContent` not disposed; Grok error response leaks if reading its body throws | PlatformLogin.cs:237,279-288; GrokWebSearch.cs:59-66 | idiom |
| 14 | `lstat` via `[DllImport]` with runtime string marshalling: use `[LibraryImport]` (AOT) | OciMissionPuller.cs:122-128,158-167 | idiom |
| 15 | `ProviderClientBuilder` builds only the web-search backend: rename to match | ProviderClientBuilder.cs:7-18; Cli/README.md:137 | ownership |
| 16 | Comments, headers and READMEs: stale forge-infra path; history narration ("(T5)", "(R16)", "future approval hook", "v1", "Formats 1 and 2"); `ExpertResolver` header says warnings always go to stderr; `SpeakerTranscript` names a missing `Conversation`; `ProviderClientBuilder` quotes a non-existent error; `ForgeTomlReader` header omits multi-line arrays; CLI README says Hands owns containment (Core `WorkspaceGuard` does); ChatClients README omits `ProviderKeyGuard`; Core README Owns omits `CredentialStore` | PlatformLogin.cs:61,299; ProviderKeyGuard.cs:5-8; ToolExecutionResult.cs:5-6; CapabilityAuthorizationPolicy.cs:13; InMemoryCapabilityAuditLog.cs:3; PipelineToolPause.cs:101; ForgeExitException.cs:3-4; ExpertResolver.cs:12-13; SpeakerTranscript.cs:6; ProviderClientBuilder.cs:11-12; ForgeTomlReader.cs:5-12; Cli/README.md:171; ChatClients/README.md:19-26; Core/README.md:20-29 | style+ownership |

## Behaviour changes (each its own flagged commit)

| # | Issue | Locations | Sev | Lenses |
|---|---|---|---|---|
| B19 | ✓ A Hands Edit with `old_string: ""` loops forever: `IndexOf("", i)` returns `i` and `index += 0`, so the dispatcher spins at full CPU. The model controls the value | CapabilityDispatcher.cs:105,191-201 | High | idiom |
| B20 | ✓ `LocalDiskWorkspace.ExecuteAsync` child lifetime: caller cancel never kills the shell; after a timeout kill the output tasks and exit are never awaited; output unbounded (exec runner caps it); stdin inherited; `catch (Exception)` around `Start` | LocalDiskWorkspace.cs:50-93 | High | idiom+style |
| B21 | ✓ `MclParser.TryParse` lets `ParseException` (AST builder) and `OverflowException` (`int.Parse` on `INT`) escape, so the CLI shows an unhandled stack trace instead of a diagnostic | MclParser.cs:33-54; MclAstBuilder.cs:47,55,72-75,119,151,166,171; CliHelpers.cs:36-46 | Medium | idiom |
| B22 | ✓ Empty YAML document: `Deserialize<T>` returns null, so an expert with `---\n---` or an empty `mcl.lock` throws `NullReferenceException` instead of a diagnostic | ExpertLoader.cs:367,376; LockFile.cs:69-70; ExpertResolver.cs:25 | Medium | idiom |
| B23 | ✓ forge.toml: `Split('#')[0]` runs before quotes are parsed, so a quoted value containing `#` (URL fragment, API key) is cut off | ForgeTomlReader.cs:52,266 | Medium | idiom |
| B24 | One credentials reader: MissionRegistry's `RegistryCredentialStore` reads `credentials.json` and `FORGE_REGISTRY_TOKEN` with its own DTOs and error rules; Core `CredentialStore` owns the schema and the write. Delete the MissionRegistry copy (`forge init` passes the token, as forge-client does); a corrupt or unreadable file is an error, not "not signed in" | RegistryCredentialStore.cs:6-50; OciExpertPuller.cs:8-10; CredentialStore.cs:51-65,118-130; MissionDevCommands.cs:82 | Medium | all three |
| B25 | Culture: context values become text with the current culture (`0,5` under de-DE in prompts, HTTP bodies, `when()`, the result); `when()` parses with `NumberStyles.Any`; `int.TryParse` without culture; culture-sensitive sorts decide `mcl.lock` key order | ContextInterpolator.cs:16; HttpExpertRunner.cs:20; PipelineRunner.cs:237,251,937; Rule/JsonExtract/Search/DirectExpertRunner; ForgeTomlReader.cs:202; RuleExpression.cs:104; LockFile.cs:77; ExpertLoader.cs:113; MissionDevCommands.cs:43,73,167; PlatformLogin.cs:146 | Medium | idiom |
| B26 | Catch-all `catch (Exception)` at CLI boundaries reports bugs as "Cannot read mcl.lock"/"Cannot initialise provider"; bare `catch { }` on opening the browser | McpCommand.cs:51-52,139-141; MissionServerBuilder.cs:25-26,40-41; PlatformLogin.cs:279-288 | Medium | idiom+style |
| B27 | Exec runtime stdout failures (invalid JSON, missing `outputKey`) are thrown as `ExpertLoadException`; sequence-step failures re-wrapped as `InvalidOperationException("Step X failed")`, losing the original type | ExecExpertRunner.cs:200-201,222-224; PipelineRunner.cs:581-587 | Medium | idiom+style |
| B28 | "Read `mcl.lock`, then resolve" written twice in the CLI; `forge mcp` handles a read failure, `forge validate` does not: one `CliHelpers` path | McpCommand.cs:50-57; MissionDevCommands.cs:140-146 | Low | ownership |
| B29 | onnx `threshold` checked only for non-blank at load; a non-numeric value fails at run time with `FormatException` | ExpertLoader.cs:434; OnnxExpertRunner.cs:34 | Low | idiom |
| B30 | `HttpExpertRunner` falls back to a static `new HttpClient()` (no pooled connection lifetime, redirects on, 100 s timeout) | HttpExpertRunner.cs:9-12; PipelineRunner.cs:770 | Low | idiom |
| B31 | MCP server reports version `"1.0"`, not the CLI version | McpCommand.cs:94 | Low | style |
| B32 | ChatClients' error text tells the user to set `MCL_PROVIDER`, a name used nowhere else (wrong in forge-runner) | ProviderKeyGuard.cs:36-38 | Low | ownership |

## Design items (owner decision first; not carved)

| Item | Why it waits |
|---|---|
| Nobody owns the `mcl.lock` `Source` values: ✓ `forge init` writes `"oci"`, forge-client writes the locator and reads `Source` back as a locator, Core knows only `"experts"` (High) | Core `LockFile` must define the values; forge-client changes with it |
| forge.toml expert locator read with two grammars: `forge init` uses `OciReference.Parse`, forge-client its own `ResolveExpertReference` (High) | One MissionRegistry resolve method; `forge init` would start accepting tag and short forms |
| Platform sign-in storage (`PlatformCredential`, `Save`/`ClearPlatform`) is in Core but only the CLI uses it | Rehoming changes Core's public API (next version bump) |
| `CapabilityAuthorizationPolicy.Default` auto-approves file and terminal; Bob's `HandsPolicy` is the real policy; only tests use `Default` | forge-client tests use it |
| Terminal capability has no implementer in any repo, but Core declares Bash and the runner offers it | Bob owns whether terminal exists |
| Valid input-name rule copied in the CLI, Core and forge-platform | Owner: `ForgeMission.Conversations.Contracts` |
| CLI re-declares forge-client names (`forge.project.json`, `conversation-host` client) | forge-client must expose them |
| Tool results carry no outcome code; forge-client detects a policy denial by matching "denied by policy" | Contract change across Core and forge-client |
| Core types used only inside Core are public (SpeakerTranscript, ContextInterpolator, RuleExpression, five runners, WorkspaceGuard) | Package API change; batch with the next Core version bump |
| `"mission.mcl"` literal with no owner (7 sites) | Same Core owner as the `mcl.lock` constant item |
