# forge-mcl — Phase 1 backlog (structure & idiom)

Run: Phase 79 step 6 · repo `forge-mcl` · scope `src/` (product code; Parser `Generated/` excluded) ·
base `main` `e5a85b2` · lenses: `csharp-expert` (idiom), `ownership-reviewer`, `code-style-reviewer`
· baseline: build 0 warnings, tests 605 passed / 4 skipped / 0 failed.

Merged and deduplicated; ordered dependency first. Rows marked **(D)** delete code with no caller in
forge-mcl, forge-runner or forge-client; the rest are behaviour-preserving carves in place.

## Carves (Stage 1)

| # | Issue | Locations | Lenses |
|---|---|---|---|
| 1 ✅ `6eac71c` | **(D)** The old client-tool wire path (from `forge serve` / `forge claude`): `MissionChatClient`, `EnrichmentCache`, `ToolContinuationGate`, `ConversationHash`, `Conversation`, and the `PipelineRunOptions` fields only it sets (`Tools`, `StartAtAgent`, `OnPreAgentComplete`, `ContextObjects`) with their runner/`DirectExpertRunner` branches | Core/Adapters/MissionChatClient.cs; Core/Runtime/{EnrichmentCache,ToolContinuationGate,ConversationHash,Conversation}.cs; PipelineRunOptions.cs; PipelineRunner.cs; DirectExpertRunner.cs | idiom+ownership+style |
| 2 ✅ `bf10d0d` | **(D)** Console streaming hooks with no setter (`StepWriter`, `ContentWriter`, `OnSearchProgress`) and their branches | PipelineRunOptions.cs:9-16; PipelineRunner.cs | idiom+ownership |
| 3 ✅ `6f03007` | **(D)** `ToolExecutorRegistry`, `IToolExecutor`, Read/Edit/Write executors, `ToolArguments` (move live `ToolExecutionResult` first) | Core/Tools/* | idiom+ownership |
| 4 ✅ `b12b0ab` | **(D)** Unused public types and members: `IChatClientFactory`, `ToolCallNotification`, `MissionSourceSelection`, `MissionDiscovery`, `CredentialStore.GetProvider`/`SaveProvider`, `ExpertLoader.LoadFromLockFile` | Core/Runtime, Core/Resolution, Core/Experts | idiom+ownership |
| 5 | Comments that narrate history or name deleted surfaces (Phase/task refs, `fms`, `forge serve`) | PipelineRunner.cs, DirectExpertRunner.cs, PipelineRunOptions.cs, MissionDevCommands.cs, READMEs | style+idiom+ownership |
| 6 | Split `PipelineRunner.RunCoreAsync` (~150 lines, nesting 4+) and `InvokeStepAsync` (~68 lines) into named steps; dedupe the sequential/parallel sub-mission path | PipelineRunner.cs:110-258, 260-331, 404-466, 471-538 | style |
| 7 | Name `ResumeAsync`'s six-clause admission check | PipelineRunner.cs:77-83 | style |
| 8 | `ExpertLoader`: flatten `ValidateContextKeys` (5 levels), collapse `ParseContent` copy-paste, reuse `SplitFrontmatter` | ExpertLoader.cs:214-273, 343-421, 285-294 | style |
| 9 | `[DoesNotReturn]` on `CliDiagnostics.Die`; drop the dead `return`s after its 22 calls | Cli/Services/CliDiagnostics.cs + callers | idiom |
| 10 | One CLI mission-load sequence for `init`, `validate`, `mcp` (removes the double `File.Exists` and the drifted catches) | Cli/Commands/{MissionDevCommands,McpCommand}.cs; CliHelpers.cs | idiom+ownership+style |
| 11 | `McpCommand.ServeAsync`: extract the 45-line handler; build the error result once | Cli/Commands/McpCommand.cs:83-157 | style+idiom |
| 12 | One registry-token reader (drop the `RegistryCredentialStore` copy) | MissionRegistry/RegistryCredentialStore.cs; Core/Resolution/CredentialStore.cs | idiom+ownership |
| 13 | One provider list (catalog, build switch, key guard, TOML reader) | ChatClients/*; Core/Manifest/ForgeTomlReader.cs | idiom+ownership |
| 14 | Dedupe `DirectExpertRunner` tool-mode options; `CapabilityDispatcher` preamble; tool-argument JSON writer | DirectExpertRunner.cs; CapabilityDispatcher.cs; PipelineRunner.cs:817-868 | style |
| 15 | Readability carves: `OciMissionPuller.ExtractBundleAsync`, `ExecExpertRunner.RunAsync`, short names in hot code, Scout namespace | MissionRegistry, Core/Adapters, Scout | style+idiom |

## Behaviour changes (own commit each, flagged)

| # | Issue | Locations |
|---|---|---|
| B1 | `CredentialStore` turns a corrupt credentials file into an empty one, then overwrites it on save | Core/Resolution/CredentialStore.cs:119-143 |
| B2 | MCP handler: `Die` throws inside `CallToolHandler`, so its error branch never runs | Cli/Services/MissionServerBuilder.cs; McpCommand.cs:137-139 |
| B3 | `forge project create` ignores Ctrl+C | Cli/Commands/ForgeProject.cs:21,26,62-63 |

## Out of Stage 1 (needs design; recorded verbatim, not scoped here)

Capability authority (policy, confirmation, audit) lives in Core but belongs to forge-client; the
`~/.forge/credentials.json` owner; expert resolution built twice (CLI `init` and forge-client); Scout
carrying the Grok client into the Core package; the hand-written TOML reader; the orphaned terminal
capability (forge-runner still offers Bash); the CLI registering forge-client's HttpClient.
