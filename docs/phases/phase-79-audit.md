# Phase 79 — Step 2 cleanup audit

> **Status: audit complete (2026-11-14), awaiting operator approval (step 3).** Read-only audit of
> every Forge repo against [product.md](../design/product.md), one auditor per repo, on `main` as
> of 2026-11-14. Nothing has been deleted. "Inferred" = no caller found, not proven. This file is
> active work: it is deleted when step 4 is done.

## Size

Whole files and folders only (in-file trims not counted), measured 2026-11-14:
~23,500 of ~101,800 code lines in clear deletes (23%), ~27,000–30,000 with D1 and D4 (~30%).
Per repo: forge-mcl 29–36%, forge-rooms 29–~55%, forge-platform 31–33%, forge-runner ~30%,
forge-infra 25%, forge-client 6%, forge-conversations 4%. This repo: ~92% of doc lines go.
Deslop (step 6) is expected to cut a further 10–20% of what survives (estimate, not measured).

## GitHub Actions impacts

Every workflow in the Forge repos, checked 2026-11-14 against the agreed deletes. Goal: keep every
remaining workflow working, not improve it. Each repo's delete PR updates its rows here and proves
them.

| Repo | Workflow | Impact | Action in that repo's delete |
|---|---|---|---|
| forge-mcl | `publish-terminal-extensions-package.yml` | Publishes a deleted package | Delete workflow (package deprecated in step 5) |
| forge-mcl | `publish-docker-package.yml` | Publishes a deleted package (D1) | Delete workflow |
| forge-mcl | `publish-serve-package.yml` | Publishes a deleted package (D1) | Delete workflow |
| forge-mcl | `release.yml` | Builds the CLI; has Exec probe steps and probe evidence upload (D4) | Remove probe steps; keep `libonnxruntime` (ONNX kept); prove by a CLI release run |
| forge-mcl | `publish-{core,parser,chatclients,missionregistry,scout}-package.yml` | Test filters name namespaces; none deleted | No change; prove the next publish passes |
| forge-mcl | — | **No PR CI exists** | Proof is the local build, full tests and AOT publish (`scripts/build.ps1`) |
| forge-client | `ci.yml` | Builds and tests the whole solution | Must stay green on the delete PR |
| forge-client | `publish-{client,client-contracts,hands}-package.yml` | Always-failing visibility step (already agreed to remove) | Remove that step; prove one publish run |
| forge-conversations | `publish-conversations-packages.yml` | Packs `ConversationPresentation` (deleted) | Remove the Presentation pack and verify lines |
| forge-platform | `forge-api-image.yml`, `billing-image.yml` | Build the images; API A deletion changes the ForgeAPI image | Prove one image build |
| forge-platform | `publish-billing{,-contracts}-package.yml` | No impact | None |
| forge-rooms | `forge-ui-image.yml` | Builds `Dockerfile.forgeui`, which includes the Rooms projects and the migration bundle | Update Dockerfile and slnx with the Rooms deletion; prove one image build |
| forge-runner | `forge-runner-image.yml` | Image changes (no `missions/`, no tesseract/poppler); a comment mentions Rooms | Prove one image build (two arch) and the process-hosting probe |
| forge-runner | `forge-runner-nuget.yml` | Packs the `Katasec.Forge.Runner` host package (deleted) | Remove that pack; keep Contracts |
| forge-infra | `infra-validate.yml` | Compiles the kept Bicep layers | Must stay green after Kind removal |
| forge-infra | `infra-deploy.yml` | Manual deploy; no deleted layer in its options | None |
| mission-control-language | — | No workflows | None |

Not fixed here (would be optimizing): forge-mcl's publish workflows share the always-failing
visibility step (backlog row "Publish workflow final check"); forge-platform has no PR test
workflow.

## How to read

- **KEEP (trim)** = keep the component, but remove the parts listed.
- **UNSURE** rows map to a numbered decision in [Decisions needed](#decisions-needed).
- Rows are grouped by folder or project, not per file.

## Review progress (step 3)

Walked through per repo with the operator, one question at a time.

| Repo | Clear deletes | Notes |
|---|---|---|
| forge-mcl | **Agreed** (2026-11-14) | ONNX kept; D1 delete (incl. Serve, Docker); D4 yes |
| forge-client | **Agreed** (2026-11-14) | Local history copy deleted (a recorder export can replace it later); D2, D3 still open |

## Decisions needed

Answered one at a time with the operator; the answer is recorded here and drives every row marked
with that decision.

| # | Decision | Rows it settles | Answer |
|---|---|---|---|
| D1 | Do the local-runtime commands stay: `forge serve`, `forge claude`, `forge connect vscode` (and the `/v1` wire doors behind them)? MCP already covers "Forge inside the user's own agent". | forge-mcl Serve/claude/connect, MissionServerFactory, EnrichmentCache, ForgeMission.Docker, agent/webui, Dockerfile; runner MissionDoorClient, RunnerRegistry, MemberIdHeader; platform WireProxy + `/v1`; this repo's agents/, clients/python, README sections, Phase 42.x | **Delete** (operator, 2026-11-14). Every row marked D1 becomes DELETE. Also deleted: the `ForgeMission.Serve` library (`Katasec.Forge.Mcl.Serve`) and its server tests, because its only consumers are `forge serve` and the runner's `/v1` doors; `ForgeMission.Docker` (only other consumer is the archived forge-desktop). `MissionServerBuilder` stays, trimmed, because `forge mcp` uses it. |
| D2 | `forge project create`: only write `forge.project.json`, or keep creating a hosted Chat conversation? | client StarterChatLaunch, StarterMissions, ChatProjectCreationTests; CLI output "Run `forge chat`" | — |
| D3 | Keep Hands crash recovery (`RecoverMissionHandsInFlight`) for `forge run`? | conversations grain/route; platform route | — |
| D4 | Exec and terminal: apply the operator's existing decision B (standard `Process`, branch `adeen/phase-78-exec-process` in this repo), and delete the unreachable macOS terminal provider? | forge-mcl ExecProcess*, Exec.Probe, build/release probe steps; client ProjectBoundTerminalProvider, terminal profile + tests; runner python3 | **Yes** (operator, 2026-11-14): apply the 11-02 decision B (standard `Process`), deleting `ExecProcess*`, the Exec probe and its build/release steps; also delete the macOS sandbox terminal provider, the terminal profile member and their tests (unreachable, judged slop). Runner keeps python3 (exec missions are Python scripts). |
| D5 | Names: rename the `forge-rooms` repo and solution; keep or rename DB `forge_rooms`, `kv-forgerooms-dev`, `crforgeroomsdev`, compose names? | forge-rooms, forge-infra | — |
| D6 | ForgeUI DB history: add one migration that drops the Rooms tables (keeps prod data), or squash to a fresh identity migration? | forge-rooms Migrations/ | — |
| D7 | Keep the PWA assets (manifest, service worker, offline page)? | forge-rooms wwwroot | — |
| D8 | Keep the xAI key in the runner (only if a `forge.toml` provider or the `search` kind needs it)? | forge-infra 500-app; Grok search tests backlog row | — |
| D9 | Remove Kind/local mode everywhere (Azurite stays for tests)? | forge-infra 350 Kind files, SAS rules, deploymentScript; conversations connection-string branch; backlog `forge dev start`, Local Host isolation | — |
| D10 | Dev data: purge stored Project/Evaluation conversation rows, or keep the enum members readable? | conversations `ConversationPurpose`, event kinds | — |
| D11 | Docs and demos in this repo: rewrite or delete `desktop-interaction-principles.md`; fold or delete the UNSURE design docs; move MCL demos, `editors/vscode`, `html/` to forge-mcl or delete; keep or drop the Phase 22/26/30/37/39 candidates | mission-control-language Table 1 and 2 | — |

The viewer's read path (ForgeAPI `ListProjectRuns`/`GetProjectRun` vs `ListMissionConversations`)
is a [Phase 78](phase-78-run-detail-capture.md) design question, not a cleanup decision. Those rows
stay until Phase 78 settles it.

## Cross-repo order

**One repo at a time** (operator, 2026-11-14): delete one repo's agreed list on a branch, prove it
(`build -warnaserror`, full tests, AOT publish for forge-mcl, and a real `forge run` against the
default cloud endpoint), merge, then start the next. A failure points at one delete.


1. **forge-mcl:** move `ForgeChat.Print` into `run --verbose` and strip Theme from `ForgeSettings`,
   then delete the chat TUI (tag `forge-chat-final` first).
2. **forge-client:** then delete chat-only and Desktop-era APIs (major version bump of Client and
   Contracts); forge-mcl picks up the new version.
3. **forge-rooms:** extract the account model (`Member`, member storage, EF context) out of the
   Rooms projects first; deploy ForgeUI without Rooms before the table-drop migration runs.
4. **forge-platform:** delete API A (no callers anywhere) and the unused conversation routes,
   in lockstep with a Contracts version bump in forge-conversations.
5. **forge-runner:** move `RunWorkspace` out of `MissionRunHandler.cs` (the queue path uses it),
   then delete the HTTP `/run` side, but only after ForgeUI Rooms and ForgeAPI API A stop calling it
   (ForgeAPI probes `/missions` at boot).
6. **forge-infra (step 5, live):** retire legacy queues with the existing target (what-if first)
   before deleting its script; drop SAS rules and the Kind deploymentScript in one PR; confirm
   Host/runner use managed identity only before disabling shared-key/local auth.
7. Published packages are deprecated, not deleted: `ConversationPresentation`,
   `Terminal.Extensions`, `Mcl.Docker`, `Katasec.Forge.Runner` host package.

## forge-mcl

| Path | What | Verdict | Reason |
|---|---|---|---|
| `src/ForgeMission.Cli/Tui/` (incl. Graphics, Fonts) | `forge chat` TUI | DELETE | Chat deleted (Q3) |
| `Cli/Commands/ForgeChat.cs` | `forge chat` | DELETE after moving `Print` | `run --verbose` calls `ForgeChat.Print` |
| `Cli/Services/MacChatWindow.cs` | Mac/Ghostty launcher | DELETE | Chat only |
| `Cli/Services/ForgeSettings.cs` | `~/.forge/config.json` | KEEP (trim) | Drop Theme and its validator |
| CLI csproj: XenoAtom (+Markdown, CodeEditor.TextMateSharp → libonigwrap), StbTrueTypeSharp, fonts, AllowUnsafeBlocks | TUI dependencies | DELETE | TUI only; remove from `Directory.Packages.props` |
| `src/ForgeMission.Terminal.Extensions/` + its publish workflow, verify script, Makefile targets | XenoAtom add-ons package | DELETE | No consumer; deprecate package |
| `eng/fonts/inter-subset.sh` | Font subset script | DELETE | TUI only |
| Commands: run, publish, ForgeDurableRun, ForgeProject, Auth, MissionDev (init/validate/list/expert), Provider, Clean, CommandActions | Core CLI | KEEP | The product's CLI |
| `Commands/McpCommand.cs` | `forge mcp` (runs missions locally in-process) | KEEP | MCP wrapper fundamental |
| `Commands/ServeCommands.cs`, `ConnectCommand.cs` | `serve`, `claude`, `connect vscode` | UNSURE (D1) | Local runtime, not in product.md |
| `Commands/AgentCommand.cs`, `WebuiCommand.cs` | `forge agent` (Docker), Open WebUI | DELETE | Not in product.md |
| `Cli/Services/` (MissionServerBuilder, Cli*, ImmediateProgress, ProviderClientBuilder, Contracts/, …) | Shared CLI services | KEEP (trim) | Trim serve/agent-only parts after D1 |
| `src/ForgeMission.Cli/README.md` | CLI doc | KEEP (rewrite) | ~62 chat/TUI mentions |
| `src/ForgeMission.Parser/` | MCL grammar | KEEP | Language |
| `src/ForgeMission.Core/` (Runtime, Manifest, Resolution, Experts, Rules, Tools, Adapters) | Mission runtime | KEEP | Used by client, Host, runner; deslop in step 6 |
| Core ONNX: `OnnxExpertRunner`, ONNX branches, `Microsoft.ML.OnnxRuntime` | ONNX expert kind | KEEP (operator, 2026-11-14) | Non-LLM expert kind stays in the language |
| `Core/Runtime/EnrichmentCache.cs`, `Adapters/MissionServerFactory.cs` | Serve-path internals | UNSURE (D1) | Only serve/claude use them |
| `Core/Runtime/AgenticSession.cs`, `Tools/BashToolExecutor.cs`, `Tools/WorkspaceTerminalProvider.cs` | In-proc agent session, terminal tools | UNSURE (D1/D4) | No consumer outside forge-mcl (inferred dead) |
| `Core/Adapters/ExecExpertRunner.cs` | `kind: exec` | KEEP | Used by runner |
| `Core/Adapters/ExecProcess*.cs`, `tests/ForgeMission.Exec.Probe/`, probe/pid1 steps in build.ps1 + release.yml | Custom process spawning + proof | UNSURE (D4) | Operator decision B replaces it |
| `src/ForgeMission.ChatClients/`, `MissionRegistry/`, `Scout/` | Providers, OCI, search | KEEP | Used by runner/client/platform |
| `src/ForgeMission.Serve/` + its publish workflow and verify script | Wire mapping for `/v1` | DELETE (D1) | Only consumers: `forge serve` and the runner doors |
| `src/ForgeMission.Docker/` + publish workflow, verify script, Makefile targets; `Dockerfile`, `.dockerignore` | Docker helpers, `forge agent` image | DELETE (D1) | Only agent/webui/claude and Desktop use them |
| Publish workflows for core/parser/chatclients/missionregistry/scout/serve, `release.yml`, their `eng/verify-*` | Package + CLI release | KEEP | Drop probe lines if D4 |
| `scripts/` | Build/release | KEEP (trim) | Remove probe steps and chat test filters |
| Makefile, slnx, Directory.*.props, nuget.config, .gitignore, LICENSE | Plumbing | KEEP (trim) | Remove Terminal.Extensions, Docker, Probe |
| `README.md` | Repo doc | KEEP (edit) | Drop Docker helpers, Desktop/Rooms |
| Tests: all TUI/chat tests (ChatScreen*, ChatTranscript, ChatTuiRefollow, CodeCopyControl, FileEditor, ForgeChat, ForgeMarkdownStyle, ForgeText, GlyphText, GposKerning, HeadingRoute, KittyImagesThread, MacChatWindow, Motion, SpinnerFrames, StartPage, TerminalCaret/Pointer, TextArt, TextImages, TextInteraction, TileSet, TuiColourLiteral, TuiQuitContract, TypeAhead, XenoAtom*, XenoInternalsContract, CpuBoundCollection, Fixtures/Kerning), `tests/.../Terminal/` | TUI tests | DELETE | Test deleted code |
| `Cli/ForgeChatLineModeTests.cs` | `Print` pins | KEEP (move) | Retarget at `run --verbose` |
| `Cli/CtrlCTests.cs`, `ForgeConfigTests.cs` | Mixed | KEEP (trim) | Drop chat and theme rows |
| Kept-CLI tests (ForgeDurableRun, ForgeProject, PlatformLogin, McpCommand, ListExperts, MissionFileResolution, MissionReadFailure, ProviderScaffold, CleanCommand, DefaultPath, Harness) | | KEEP | Drop agent/serve/webui rows from DefaultPath |
| `ForgeServeTests`, Integration ClaudeCode/ForgeClaudeLauncher/WireCapture/MockClaudeHost/CopilotSdk | serve/claude tests | UNSURE (D1) | |
| Integration Anthropic/OAI server fixtures, ConvergedServe, RequestClassifier, InMemorySessionStore, anthropic-wire fixtures | Serve-library tests | DELETE (D1) | Serve library deleted |
| `OnnxExpertRunnerTests`, `Fixtures/onnx/` | | KEEP | ONNX kept |
| `ExecExpertRunnerTests` | | KEEP | Known broken-pipe flake |
| `DockerCliTests`, `DockerPrereqCheckerTests` | | DELETE (D1) | Follow Docker library |
| `AgenticSessionTests`, `BashToolExecutorTests`, `Missions/agentic` | | UNSURE (D1/D4) | Follow those types |
| Remaining tests (Parser, Registry, Resolution, Rules, Runtime, Tools, Manifest, Experts, ChatClients, Scout, Adapters, Fixtures/oci, Missions/converse, Missions/noop) | | KEEP | |

## forge-client

The CLI uses only: `ApplicationComposition.Create`; `PrepareSelectedPackageAsync` + `StartAsync` →
`ProjectConversation.RunTurnAsync` (`forge run`); `CreateChatProjectAsync` (`project create`); and,
for chat only, `ContinueChatAsync`, `GetConversationAsync`, `StreamEventsAsync`, `ResumeAsync` and
the `ProjectConversation` header properties. `forge mcp` uses no client package.

| Path | What | Verdict | Reason |
|---|---|---|---|
| `ClientRuntime/ClientExecutionSession.cs`, `MissionHandsDriver.cs` | Hands (Bob) | KEEP | `forge run` |
| `ClientRuntime/MissionExecutionProfile.cs` | Profile enum | KEEP (trim) | Terminal member per D4 |
| `ClientRuntime/ProjectBoundTerminalProvider.cs` | macOS sandbox terminal | UNSURE (D4) | Unreachable today |
| `Application/ApplicationComposition.cs` | Composition root | KEEP (trim) | Remove unused `UploadInputAsync`, named-input `SubmitAsync`, chat-only methods |
| `Application/Missions/ProjectConversation.cs` | Run/chat handle | KEEP (trim) | Remove `ResumeAsync` and chat header properties; follow loop is shared, stays |
| `Application/Missions/MissionConversationService.cs` | Run core | KEEP (trim) | Dead: `RetryAsync`, `UploadInputAsync`, named `SubmitAsync`; chat-only: `ContinueChatAsync`, `ReadChatLaunchAsync` |
| `Application/Missions/MissionHandsConversationService.cs` | Hands claim/execute | KEEP (trim) | Confirmation branch unreachable |
| `Application/Missions/StarterChatLaunch.cs`, `Projects/StarterMissions.cs` | Chat launch | UNSURE (D2) | |
| `Application/Interaction/PendingConfirmationHandler.cs` | Confirmation handler | DELETE | Never triggered; replace with a deny handler (Bob requires one) |
| `Application/Sessions/ApplicationSessionService.cs` | Session registry | KEEP (fix) | Sessions never removed (leak) |
| `Application/Sessions/ChatSessionProjection.cs` + tests | `~/.forge/sessions` copy | DELETE | Never read; the recorder replaces it |
| `Application/Projects/` (ProjectService, manifest, declaration, MissionPackageBuilder) | Project + package prep | KEEP | Run path |
| `Application/Adapters/Conversations/ConversationHostClient.cs` | ForgeAPI adapter | KEEP (trim) | Dead: `UploadMissionInputAsync`, `RetryMissionTurnAsync`, `BeginMissionHandsConfirmationAsync` |
| `Application.Transport/ApplicationContracts.cs` | Error codes, DTOs | KEEP (trim) | 20 of 30 error codes unused; check no ForgeAPI contract serializes the enum |
| `Application.Transport/ApplicationEvent.cs` | In-process notice | DELETE | Dead with the projection |
| `Application.Transport/` project (Contracts package) | Separate package for Desktop transport | UNSURE | Fold into Client, or keep; decide in step 4 |
| `ForgeMission.Application.csproj` | | KEEP | Check whether `Microsoft.AspNetCore.App` is Desktop-era |
| READMEs (Application, Sessions, Missions, Projects, ClientRuntime, repo) | Docs | KEEP (rewrite) / DELETE Sessions | Desktop framing, links to deleted docs |
| Tests: MissionConversationService, ProjectConversationTurn, ProjectConversation, PortableChat | Mixed | KEEP (trim) | Drop chat/resume/upload cases |
| Tests: MissionHandsConversationService, ProjectDeclaration, ConversationHostClientMessage, ClientExecutionBoundary, MclPackageConsumption, ClientExecutionSession, PortableChatFixture | | KEEP | |
| `ChatProjectCreationTests` | | UNSURE (D2) | |
| `MissionExecutionProfileTests` | | UNSURE (D4) | |
| `ApplicationCompatibilityBoundaryTests` | Guards long-deleted files | DELETE | History, not product |
| Workflows ci + 3 publishers, `eng/verify-*` | | KEEP (fix) | Remove the always-failing visibility step |

## forge-conversations

| Path | What | Verdict | Reason |
|---|---|---|---|
| `src/ForgeMission.ConversationHost/` (Program, Api, Grains, Messaging, Persistence) | Host | KEEP | Every `forge run`; recorder store (Phase 78) |
| Grain mission-conversation, turn, cancel/retry, Hands, progress, snapshot, ReadAfter | Core paths | KEEP | forge-client calls all |
| Project-run family: `AcceptProjectMissionContainerCreate/Run`, `ReadProjectRun(s)/Events`, `ReadProjectCommand`, `ProjectRunIndex`, its stores, routes `/conversations/{id}/runs*`, `/project-commands/*`, `ProjectRunHistoryTests` | Desktop-era runs | DELETE (inferred) | Only forge-desktop used it |
| Evaluation family: `AcceptEvaluationCreateAsync`, Start/GetEvaluation, `/evaluations/*` | Hidden evaluations | DELETE (inferred) | Only forge-desktop |
| Generic `StartConversation`/`SubmitConversationCommand`/`SubmitToolResult` | Pre-mission path | UNSURE | No client caller; trace whether `ContinueAfterTool` (runner) depends on it first |
| `RecoverMissionHandsInFlight` | Hands recovery | UNSURE (D3) | |
| `ConversationPurpose.ProjectControl` etc. | Stored enum members | UNSURE (D10) | |
| Event hub, observer, SSE writer, feed | Live stream | KEEP | `forge run`, viewer live tail |
| `/mission-conversations/{projectId}` | Run list query | KEEP | Phase 78 Runs list |
| Messaging (dispatcher, progress, failure recorder, ingress) | Host ↔ runner, edge | KEEP (trim) | Trim handlers for deleted families |
| Persistence (event store, bodies, directory store) | Durable log | KEEP | Recorder store |
| `Program.cs` Kind/Azurite connection-string branch | Local credentials | UNSURE (D9) | Azurite tests use it |
| `src/ForgeMission.Conversations.Contracts/` | Contracts package | KEEP (trim) | Version bump with deleted families |
| `Contracts/ProjectMissionNames.cs` | | UNSURE | Check if only the project-run family uses it |
| `src/ForgeMission.ConversationPresentation/` | Razor activity package | DELETE | Only Rooms and Desktop used it; deprecate package |
| slnx, publish workflow, verify scripts | | KEEP (trim) | Drop Presentation |
| Dockerfile, READMEs | | KEEP (rewrite) | Desktop/Rooms wording |
| Tests: Api, Ingress, OwnerScoping, Grain | Mixed | KEEP (trim) | Drop project/evaluation/StartConversation cases |
| Other tests and fixtures | Core path | KEEP | |

## forge-platform

| Path | What | Verdict | Reason |
|---|---|---|---|
| API A: `MissionEndpoints.cs`, `MissionExecutionService.cs`, `MissionCatalog.cs` (+ boot probe), `RunStore.cs`, `ArtifactStore.cs`, `Messages.cs` + their tests | One-shot API | DELETE | No caller in any repo (confirmed) |
| `WireProxy.cs` + `/v1` + `RunnerBaseUrl` + `WireProxyTests` | Hosted Claude Code wire | UNSURE (D1) | No caller in these repos |
| `Program.cs` | Host | KEEP (trim) | |
| `PlatformKeyAuth.cs`, `ApiJson.cs` | Auth, errors | KEEP | |
| `Conversations/ConversationEndpoints.cs` kept routes (CreateMissionConversation, SubmitMissionTurn, Retry/Cancel, Hands routes, UploadMissionInput, GetConversation, GetMissionHandsWork, StreamConversationEvents) | `forge run` path | KEEP | All used by forge-client |
| Routes StartConversation, SubmitConversationCommand, SubmitToolResult, CreateProjectMissionContainer, StartProjectMissionRun, StartEvaluation, GetEvaluation | | DELETE | No callers; lockstep with Contracts |
| Route RecoverMissionHandsInFlight | | UNSURE (D3) | |
| Queries ListProjectRuns, GetProjectRun, ListProjectRunEvents, GetProjectCommand, ListMissionConversations | Run reads | UNSURE (Phase 78) | Viewer read path |
| Bus, edge registration/results, Host queries | | KEEP (trim) | |
| `ForgeMission.Billing/*`, `Billing.Service/*`, `Billing.Contracts/*` + tests | Accounts, keys, pricing, settlement | KEEP | ForgeUI, `forge login`, runner settlement |
| Dockerfiles, image + package workflows | | KEEP | No PR test workflow exists: step-4 proof is a local `dotnet test` |
| READMEs | | KEEP (rewrite) | Desktop/Rooms wording |

## forge-runner

| Path | What | Verdict | Reason |
|---|---|---|---|
| `Conversations/*` (17 files) | Service Bus consumer engine | KEEP | Cloud `forge run` |
| `Settlement/*` | Settlement to Billing | KEEP | Drop `MemberIdHeader.cs` (D1) |
| `RunnerExpertRunnerFactory`, `ModelRequestShapeLogger`, `RunnerTelemetry` | | KEEP | Durable path, recorder |
| `Program.cs` | Host | KEEP (trim) | Keep `/health`, settlement, consumer, OTel |
| `MissionRunHandler.cs` | `/run` handler + `RunWorkspace` | Split: handler DELETE, `RunWorkspace` KEEP (move) | Queue path uses `RunWorkspace` |
| `MissionDoorClient.cs` | `/v1` doors | UNSURE (D1) | |
| `RunnerRegistry.cs`, `RunnerMissionSource.cs` | HTTP mission registry | DELETE (D1) | Only `/run`, `/missions`, doors |
| `RunnerArtifactStore.cs` | `/artifacts/*` | DELETE | Only API A |
| `BuiltinMissions*`, `missions/` (8 baked missions) | Baked fallback missions | DELETE | Only the HTTP registry |
| `RunnerWebSearch.cs` | Web search for `/run`, doors | DELETE | Durable path doesn't use it (inferred) |
| `Runner.Contracts/RunContracts.cs` | Contracts package | KEEP (trim) | Keep `RunSettlement`, `RunUsage`; delete the one-shot shapes (version bump) |
| Tests: Conversations/*, ModelRequestShapeLogger | | KEEP | |
| Tests: ConversationEntryPoint, SettlementEntryPoint, RunContractsSerialization | | KEEP (trim) | Drop `/run` and door cases |
| `MissionRunHandlerTests` | | Split | Move workspace tests |
| `RunnerRegistryTests`, `BuiltinMissionsTests`, `MissionSourceSelectionTests` | | DELETE | |
| `Dockerfile.runner` | Image | KEEP (trim) | Drop `missions/`, tesseract/poppler; python3 per D4 |
| `forge-runner-image.yml`, `verify-process-hosting.py` | | KEEP | |
| `forge-runner-nuget.yml` | | KEEP (trim) | Drop the host package (used only by Rooms tests); deprecate it |
| `README.md`, Runner and Contracts READMEs | | KEEP (rewrite) | `/run` and Rooms framing |
| `docs/extraction.md` | Extraction record | DELETE | Fold "locked boundary" lines into README |

## forge-rooms (ForgeUI)

| Path | What | Verdict | Reason |
|---|---|---|---|
| Root `README.md` | Phase 49 stub | DELETE | Replace with a ForgeUI README |
| `.claude/launch.json`, `.dockerignore`, `.gitignore`, `nuget.config` | | KEEP (trim) | Stale comments |
| `docker-compose.yml`, `scripts/db/init`, `scripts/dev-*.sh` | Local Postgres, dev loop | KEEP | Names per D5 |
| `scripts/gen-pwa-icons.py` | One-off | DELETE | |
| `Dockerfile.forgeui`, `forge-ui-image.yml`, `Directory.Build.props`, slnx | Build/deploy | KEEP (trim) | Migration bundle targets the new identity context |
| `src/ForgeMission.Rooms/` (Room, Membership, Invite, Message*, Mention, ReservedHandles, TrustIntegrity, AgentDescriptor, RoomMetadata, README) | Rooms domain | DELETE | Q2 |
| `Rooms/Member.cs`, `MemberKind.cs` | **Account model** | KEEP (move) | Extract a Human-only `Member` into the shell first |
| `Rooms.Data/` Room/Message/Invite/Membership configs, `RoomMemberInfo`, `RoomsSeeder`, room store methods | Rooms persistence | DELETE | |
| `Rooms.Data/` Member config + member store methods, `RoomsDbContext`, design-time factory, DI | Account storage | KEEP (move) | Login, `/me`, key issuance need it |
| `Rooms.Data/Migrations/` | Schema history | UNSURE (D6) | |
| Rooms tests (TrustIntegrity, MentionParser, RoomStore, HandleMigration) | | DELETE | |
| `IdentityAndInviteTests`, `PostgresFixture`, test csproj | | KEEP (move/trim) | Keep identity facts; drop invites; drop unused refs |
| `ForgeUI.csproj` | Web host | KEEP (trim) | Drop Rooms refs, ConversationPresentation, Mcl.Core, SignalR.Client |
| `ForgeUI.csproj.user` | IDE file | DELETE | |
| `Program.cs` | Host | KEEP (trim) | Keep auth, OIDC, billing, platform keys; remove runner probe, agents, Rooms DI, ChatHub, `/invite`, `/rooms` redirect |
| `PlatformKeyEndpoints.cs` | `forge login` keys + `/me` | KEEP | forge-mcl `PlatformLogin.cs` calls it |
| `Services/ForgeClaims`, `CurrentUser`, `MemberProvisioningService` | Identity, starting credit | KEEP (rewire) | |
| `Services/` AgentRegistry, Invite, MissionRunnerClient, RoomAdmin, RoomAgentInvoker, RoomAgentMembership, RoomBroadcaster, RoomContextAssembler, RoomCreation, RoomMessage, StarterRoom | Rooms services | DELETE | |
| `Services/MarkdownRenderer.cs` | | DELETE | No references |
| `Hubs/ChatHub.cs`, `Models/` room/chat models, `Shared/` RoomConversation, PipelineTrace, TrustSignalBadge, IdentitySealMark, `Pages/Rooms.razor`, `Pages/Library.razor` | Rooms UI | DELETE | |
| `Models/ShellChrome.cs` | Layout flag | DELETE unless the viewer needs it | Only flag is Rooms-specific |
| `Pages/Account`, `Login`, `Index`, `_Host`, `Error`, `App`, `_Imports`; `Shared/` MainLayout, BareLayout, NavShell, AccountMenu, RedirectToLogin, Icon | Shell | KEEP (edit) | Rewrite Rooms copy; Index → Runs; nav → Runs |
| `wwwroot/css/forge.css`, favicon, icons | Design system | KEEP (trim) | Prune Rooms styles |
| `wwwroot/` manifest, service worker, offline, install-hint | PWA | UNSURE (D7) | |
| `appsettings*`, launchSettings | | KEEP | |
| `src/ForgeUI/README.md`, Rooms READMEs | | DELETE (rewrite later) | |
| Repo name `forge-rooms` | | UNSURE (D5) | |

## forge-infra

| Path | What | Verdict | Reason |
|---|---|---|---|
| `bootstrap/` | CIAM tenant | KEEP | Login |
| `dev/100-base/`, `250-dns/`, `370-billing-data/`, `380-conversation-edge/`, `400-appenv/`, `450-migrate/`, `525-conversation-app/`, `540-billing/`, `550-api/` | Kept layers | KEEP | Fix "Forge Rooms" comments |
| `dev/150-ci/` | CI identity + OIDC | KEEP (trim) | Delete legacy `gh-app-image` federation (live teardown too) |
| `dev/200-entra/` | App registrations, user flow | KEEP | ForgeUI login, `forge login` |
| `dev/300-data/` | Postgres, `forge_rooms` + `authbilling_db` | KEEP | `forge_rooms` holds accounts (`Members`) |
| Rooms tables in `forge_rooms` | | DELETE via forge-rooms migration (D6) | Not an infra change |
| `350-conversation-data/storage.bicep` | Storage | KEEP (edit, D9) | `allowSharedKeyAccess` true only for Kind |
| `350 servicebus.bicep` namespace + `private-*` queues | | KEEP | |
| `350` SAS rules, local auth, `writeKindCredentials` script + param, `kind/`, kind scripts + Makefile targets | Kind | DELETE (D9) | Kind unsupported; live teardown |
| `350 retire-legacy-queues.sh` + targets | Teardown tool | KEEP until run, then DELETE | Legacy queues still live |
| `350` README, param | | KEEP (edit) | |
| `500-app` ForgeUI + runner, `alerts.bicep` | | KEEP (trim) | Runner ingress can go once only `/health` remains; Rooms comments |
| `500-app` runner xAI key | | UNSURE (D8) | |
| Makefile, workflows | | KEEP (trim) | Drop Kind targets; deploy workflow lacks 370/380/540 (gap, not cleanup) |
| `README.md`, `AGENTS.md` | | KEEP (edit) | "for Forge Rooms"; `codex/` branch rule may be stale |
| `agent_bootstrap.sh` | Codex sandbox shim | UNSURE | Only if Codex still runs here |
| Resource names with "rooms" | | UNSURE (D5) | Renaming recreates them; KV has purge protection |

## mission-control-language (this repo)

### Folders and docs

| Item | Verdict | Reason |
|---|---|---|
| AGENTS.md / CLAUDE.md, README.md, plan.md, backlog.md | KEEP (edit) | See [Agent-rule edits](#agent-rule-and-link-edits) |
| `docs/plan_completed.md` | DELETE | Q5; fold "Resolved under discussion" into language.md first |
| `docs/why.md` | UNSURE (D11) | Overlaps product.md |
| `docs/brainstorm/`, `docs/evidence/`, `docs/retrospectives/` | DELETE | Desktop-era and old evidence |
| `docs/images/phase-78/` | KEEP | Mockups |
| `docs/images/` everything else | DELETE | Desktop/TUI screenshots |
| `docs/playbooks/dotnet-deslop*.md` | KEEP | Step 6 |
| `docs/playbooks/runs/` | DELETE | Old run output |
| design: product, supervisor-workflow, security-architecture, engineering-philosophy, code-style, collaboration-style, default-path-acceptance, deploy, code-execution-isolation-references, architecture, language, stdlib, oci-artifact-schema, how-conversations-work, command-bus-architecture, ui-design-system | KEEP (edit) | Strip Desktop/Rooms/chat content and dead links |
| design: desktop-interaction-principles, forge-architecture, decisions-v0.7, observability, sdlc-meta-mission, research, methodology | UNSURE (D11) | |
| design: claude-codex-workflow, durable-conversations, forge-desktop-client-runtime, forge-desktop-dark-…(+assets), mission-chat-experience-v1 (+assets), forge_tui_* mockups, tui-graphics, room-artifacts, persistence, model-request-payloads, interaction-modes, maf, marcus-neurosymbolic | DELETE | Superseded, Desktop, TUI, Rooms, spikes |
| Phases 1–21, 23, 24, 25, 27, 28, 29, 31, 32, 34–36, 38, 40, 41, 42, 43, 44, 45, 46, 47, 48, 50, 51, 52, 53, 54–62, 63, 64, 65–69, 70, 71, 73, 74, 75, 77 (and sub-phases) | DELETE | Finished, superseded, Desktop, Rooms or TUI. Fold first: Phase 42 north-star → security-architecture |
| Phases 22, 26, 30, 37, 39 | UNSURE (D11) | Deferred candidates |
| Phase 33 (forge-mcp) | UNSURE | MCP has no design doc; fold, then delete |
| Phase 72 hub | KEEP | Parity harness (Q9); delete its `_completed` |
| Phase 76 | UNSURE | Fold locked `forge run` decisions into a design doc, then delete |
| Phases 78, 79 | KEEP | Active (78's Desktop and chat lines are stale) |
| Every `*_completed.md` | DELETE | Q5 |
| `agents/` | UNSURE (D1) | `forge serve` demo |
| `personas/` | KEEP (edit) | Supervisor workflow; Desktop/Rooms/chat examples |
| `skills/` | KEEP | |
| `missions/what-is-next` | KEEP | `.mcp.json` dogfoods MCP |
| `missions/family-halaqa`, `janus`, `naive` | DELETE | Rooms/Desktop starters |
| `missions/` language demos, `missions/concepts` | UNSURE (D11) | Move to forge-mcl or delete |
| `tools/task-timing`, `tools/supervisor-test`, `tools/supervisor-code-test` (harness) | KEEP | Supervisor workflow, parity check |
| `tools/supervisor-code-test/evidence/` | UNSURE | Old evidence unless the parity check reuses it |
| `tools/tui-capture` | DELETE | TUI |
| `clients/python` | UNSURE (D1) | `forge serve` client |
| `editors/vscode`, `html/` (+ `.claude/launch.json`) | UNSURE (D11) | |
| `.mcp.json` | KEEP | |
| Untracked: `dist/` (89 MB Desktop binaries), `out/`, `src/`, `.DS_Store` | DELETE locally | Not in git; not part of a PR |

### Backlog rows

| Verdict | Rows |
|---|---|
| **KEEP** | Runner isolation; ForgeAPI one-shot endpoints (now a confirmed delete in step 4); Registry login; Parallel Hands calls; Dead code after step 2; Config edges; Core/Client cycle; OCI without `latest`; CLI test assembly config; Fast dev cycles (re-point link); Move `forge login` key issuance; grok-4.5 price; libgssapi; Billing-only Postgres role; Retire old queues; Alert recipient; Run outcome when runner never returns; Runner never settles failed commands; Progress abandon backoff; Runner turn concurrency |
| **MERGE** | The three publish-check rows into one; the two ExecExpertRunner flake rows into one |
| **FOLD** | Central workflow telemetry → Phase 78; Phase 72 → Phase 79 step 8 |
| **DELETE** | Phases 70, 71; forge-desktop on new client; Piped hands idle-shutdown; Phase 45.5; Explorer OCI; Desktop client upgrade; Phase 46; chat editor follow-ups; Ghostty window; Open links from chat; Piped chat prints twice; SSE heartbeat (rejected); Phase 50 row 7; Phase 51; Phases 27, 29, 31, 41, 42, 42.6 5b; Unauthenticated local runtime; Rooms to command bus; Extract Rooms; API B continuation; Desktop Supervisor orphans; Rooms "Not verified"; Desktop post-run polling; Desktop run-history name; `/transport` enums; Starter mission reply (Janus); Local ForgeAPI; Streamed tool calls (absorbed); Backfill 0-token turns; Phase 52 spokes; Phases 34, 35; Phase 43.17; Phase 43 follow-ups |
| **UNSURE** | Resume a conversation; Follow reconnects; Hands after Ctrl-C; Local SDK advisory; MXC under Hands; Typed attachment reject; Phases 22, 26, 30, 37, 39 (D11); `forge dev start` and Local Host isolation (D9); Host trusts client-claimed approval; Durable run control; Grok search tests (D8); Debate/governance/guardrails |

### Agent-rule and link edits

- **Fold before deleting:** the migration-job split is already in deploy.md (retarget the AGENTS.md
  link only); Phase 76's locked `forge run` decisions → a design doc; Phase 42's north-star → 
  security-architecture; plan_completed's resolved language decisions and decisions-v0.7 →
  language.md; MCP wrapper (Phase 33) → a design doc.
- **AGENTS.md:** forge-desktop row and "(Rooms, ForgeUI)"; Phase 50 repo-map link and exception
  block; orientation step 3, plan routing and the `_completed` spoke-shape section (Q5); the
  Desktop/ForgeUI rule pointing at desktop-interaction-principles; project-structure lines.
- **README:** forge-desktop and Rooms rows, Phase 53.1 and 50 links, the `forge chat` section;
  `serve`/`claude`/`connect` sections per D1.
- **Link fixes after deletion:** default-path-acceptance (~20 phase links), engineering-philosophy,
  deploy, security-architecture, collaboration-style, supervisor-workflow, phase-78,
  tools/supervisor-test README, backlog, plan.md.
