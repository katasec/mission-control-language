# forge-mcl — Phase 1 re-discovery backlog (structure & idiom)

Run: Phase 79 step 7 · repo `forge-mcl` · scope `src/` (Parser `Generated/` excluded) · base `main`
`bd881b1` · lenses: `csharp-expert` (idiom), `ownership-reviewer`, `code-style-reviewer` · baseline:
build 0 warnings, tests 574 passed / 4 skipped / 0 failed.

Phase 1 is **not clean**: the re-discovery found what the first pass missed. Merged and deduplicated;
ordered dependency first. **(D)** = delete with no product caller in forge-mcl, forge-runner,
forge-client or forge-conversations (checked by grep). Dropped after checking: "test-only"
`ForgeTomlReader.TryReadDistribution` (forge-client calls it) and
`DurableMissionPackageValidator.TryValidateInputNames` (Host and Runner call it).

## Carves (no behaviour change)

**Done**: all 13 merged in [forge-mcl#106](https://github.com/katasec/forge-mcl/pull/106) (merge `f072329`, v0.40.0), one commit per row plus a #12 fix after Check (rows 1–11, 13 PASS; #12 FAIL → fixed: Scout 0.2.0, owner-constant reverts). Build 0 warnings, 573 passed / 3 skipped, AOT 0 warnings; real `forge run` from CLI `0.40.0+f072329`, conversation `3fa80c34-9307-54c3-a8b7-7a6e6555ffdc`.

| # | Issue | Locations | Lenses |
|---|---|---|---|
| 1 | **(D)** Legacy `OciMissionPuller.PullAsync(string, bool)` overload | MissionRegistry/OciMissionPuller.cs:13-15; README | idiom+ownership |
| 2 | **(D)** Scout progress path: progress `SearchAsync` overload, `WebSearchProgress`, Grok SSE (`ToProgress`, `GrokStreamEvent`, `stream`), `SourceRef.ImpartialityRating` | Scout/IWebSearch.cs; Scout/Grok/GrokWebSearch.cs; GrokWire.cs; Core/Adapters/SearchExpertRunner.cs:31 | idiom+ownership |
| 3 | **(D)** `ExecEnvelopeSerializerContext`; `ExpertDefinition.IsHttp/IsRule/IsOnnx/IsJsonExtract/IsExec/IsSearch`; the 8 unused `MclErrorCode` members | ExecExpertRunner.cs:280-282; ExpertDefinition.cs:27-32; MclError.cs | idiom+style |
| 4 | **(D)** Unreachable judge and tool-mode branches in `DirectExpertRunner.StreamAsync` (only plain-text non-judge steps stream) | DirectExpertRunner.cs:38-47, 103-108, 119-122 | style+idiom |
| 5 | `IExpertRunner.StreamAsync` as a default interface method; drop the six identical "await RunAsync; yield Text" adapters (forge-runner fakes keep compiling) | IExpertRunner.cs:12-15; Rule/Search/Onnx/JsonExtract/Http/Exec runners | idiom+ownership |
| 6 | One CLI expert-load error path: the `AggregateExpertLoadException`/`ExpertLoadException` catch pair is pasted in four places; `ReportExpertDiagnostic` can `Die` mid-aggregate | CliHelpers.cs:49-70; McpCommand.cs:50-57; MissionDevCommands.cs:143-148; CliDiagnostics.cs:22-31 | ownership+idiom+style |
| 7 | `Try*` helpers that never fail (`TryLoadMissionAsync`, `TryReadFile`, `TryParse`, `TryLoadExperts`): rename and drop the dead null guards | CliHelpers.cs:14-55; McpCommand.cs:48; MissionDevCommands.cs:40,119 | idiom |
| 8 | Lock-file `~/` path format in one owner (written in MissionRegistry, read and re-abbreviated in Core); cache-key sanitising inside `ForgeCache` | OciExpertPuller.cs:18,52-58; ExpertResolver.cs:88-110; ForgeCache.cs:12-20 | ownership |
| 9 | `HttpExpertRunner`: dispose the response and content | HttpExpertRunner.cs:10-31 | idiom |
| 10 | `CredentialStore.ReadCredentials`: narrow the bare `catch` to the corrupt-file exception; drop the repeated `File.Exists` | CredentialStore.cs:118-131 | style |
| 11 | Outline-first: `DirectExpertRunner` opens with ~40 lines of constants; `DurableMissionPackageValidator` packs statements per line and its broad catch hides real bugs | DirectExpertRunner.cs:11-47; DurableMissionPackageValidator.cs:19-105 | style |
| 12 | Names, visibility and constants: `ChatClients` class shares its namespace; `ForgeProject`/`PlatformLogin`/`ProviderClientBuilder` public in the exe; literals with an owner constant; `System.Threading.Lock`; `RuleExpression` `_ =`; Scout namespace `Scout` → `ForgeMission.Scout` | ChatClients.cs; Cli/*; ForgeTomlReader.cs:13; PipelineRunner.cs:24; RuleExpression.cs:148; Scout/* | idiom+style |
| 13 | Comments and READMEs: history/task narration missed by carve 5; stale headers (`ProviderProfileResolver`, `HostedMissionCommands`, `McpCommand`); CLI README piece list; MissionRegistry README gets Why/Owns/Does not own | see lens reports; Cli/README.md; MissionRegistry/README.md | all three |

## Behaviour changes (each its own flagged commit)

| # | Issue | Locations | Lenses |
|---|---|---|---|
| B4 | `forge validate` exits 0 after an expert-load error and crashes on several errors (aggregate) | MissionDevCommands.cs:141-148; Program.cs:33-37 | style+idiom |
| B5 | One expert `kind` vocabulary: an unknown or wrong-case kind (`kind: Exec`) silently runs as an LLM step | PipelineRunner.cs:767-779; ExpertLoader.cs:375-412; DurableMissionPackageValidator.cs:156 | style+idiom |
| B6 | An unparseable `timeout`/`defaultTimeout` silently becomes 30s; reject at load | ExecExpertRunner.cs:37,261-273; ForgeTomlReader.cs:163 | style |
| B7 | A judge step whose reply is not JSON passes verification | DirectExpertRunner.cs:80-92; StepEnvelope.cs:8 | style |
| B8 | MCL error codes collide (MCL010, MCL011 each mean two things); one enum owner | MclError.cs; ExpertResolver.cs; ExpertLoader.cs:248-258; Cli | idiom+ownership |
| B9 | `forge init --refresh` does nothing (both pullers ignore `refresh`): remove the option | OciExpertPuller.cs:8-10; MissionDevCommands.cs:22,72-84 | all three |
| B10 | `forge publish` parses its target with a second grammar (`SplitTaggedTarget`) and can push names `forge run` refuses to pull; use `OciReference.Resolve` | OciReference.cs:41-54; HostedMissionCommands.cs:72-75 | ownership |
| B11 | `ProviderCatalog` omits `xai`, so `forge provider scaffold xai` is refused; scaffold text moves to the CLI | ProviderCatalog.cs; ProviderCommand.cs | ownership |
| B12 | `forge clean` ignores the missions cache and does eviction in the CLI; move it behind `ForgeCache` | CleanCommand.cs:18-35; ForgeCache.cs | ownership |
| B13 | `ExpertLoader.LoadAll` silently drops a duplicate expert name | ExpertLoader.cs:35-53 | style |
| B14 | Culture-dependent `float.Parse`/`ToString("F4")`/`Number.ToString()` | OnnxExpertRunner.cs:34,43; ContextBuilder.cs:44 | idiom |
| B15 | `ExpertResolver.ToLockPath` matches home by `StartsWith` with no separator check (`/Users/xyz` under home `/Users/xy` gives `~//…`) | ExpertResolver.cs | found in carve 8 |
| B16 | Malformed YAML frontmatter reports `Operation is not valid due to the current state of the object.` instead of the YAML error | ExpertLoader (Core) | found in carve 6 |

## Design items (owner decision first; not carved)

| Item | Why it waits |
|---|---|
| Atomic temp-file write copied four times (Core and MissionRegistry; MissionRegistry has no Core reference) | Needs a shared home |
| `UsageTrackingChatClient` in Core but used only by forge-runner billing | Rehome or name in Core Owns |
| One supported-provider list for ChatClients and the `forge.toml` reader | Carve 13 kept both; owner not settled |
| `ForgeCredentials.Providers` round-trips unread | Part of the deferred credentials.json owner item; dropping it changes file content |
| `DurableMissionPackageValidator` catches BCL `InvalidOperationException`/`ArgumentException`, which also hides real bugs (carve 11 stopped) | Needs a validator-owned rejection exception and an explicit duplicate-mission-name check (overlaps B13); duplicate `let` bindings are also accepted |
| `"default"` provider-profile name: constant lives on `DurableMissionPackageValidator`, but PipelineRunner and the CLI use it too | Needs a Manifest owner (e.g. `ProviderProfileResolver`) that the validator aliases; carve 12 kept the literals |
| No Core constant for `mcl.lock`; the CLI and the validator repeat the literal | Same Core lock-file owner as carve 8 |
| Core README keeps version-history wording ("Core 0.1.10 deletes it", "Katasec.Forge.Client 0.15 still binds") | Carve 13 did not cover Core README; next re-discover |
