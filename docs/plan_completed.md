# Plan — Completed / Resolved Archive

Hub-level items (phases, open issues, "under discussion" entries) that are resolved and no longer
need to occupy space in [plan.md](plan.md)'s active tables. Same rule as phase spokes' `_completed.md`
siblings ([AGENTS.md](../AGENTS.md#spoke-shape--lookup-table-not-narrative-completed-work-moves-out)):
the phase still has its own full spoke doc under [phases/](phases/) — this file is just the TOC row,
relocated so `plan.md` only shows what's still active.

## Completed phases

Same table shape as `plan.md`'s active "Phases" table — moved here 2026-08-09 once verified `Done`
(or settled/dropped/superseded, no further action under that number). Only open this section if you
need to review historical decisions; a fresh agent orienting on "what's next" doesn't need it —
`plan.md`'s "Now" section and active "Phases" table are sufficient on their own.

| Phase | Description | Status |
|-------|-------------|--------|
| [Phase 1 — Project Scaffold](phases/phase-1-scaffold.md) | Solution structure, projects, package references | Done |
| [Phase 2 — Parser](phases/phase-2-parser.md) | Lexer, token stream, recursive-descent parser, AST | Done |
| [Phase 3 — Expert Loader](phases/phase-3-expert-loader.md) | Resolve expert names to markdown, parse frontmatter, validate | Done |
| [Phase 4 — Pipeline Runner](phases/phase-4-pipeline-runner.md) | Orchestration loop, IExpertRunner interface, output writer | Done |
| [Phase 5 — MAF Adapter](phases/phase-5-maf-adapter.md) | Implement IExpertRunner using Microsoft Agent Framework | Done |
| [Phase 6 — CLI](phases/phase-6-cli.md) | fml run, fml validate, fml list experts | Done |
| [Phase 7 — Validation](phases/phase-7-validation.md) | Build build-operator example, test hypothesis, document findings | Done |
| [Phase 8 — ANTLR Migration](phases/phase-8-antlr-migration.md) | Replace hand-rolled parser with ANTLR4-generated parser, existing tests as regression gate | Done |
| [Phase 9 — Variables](phases/phase-9-variables.md) | `let` bindings, mission parameters, per-step `with` clauses, context bag runtime | Done |
| [Phase 10 — Expert Resolution](phases/phase-10-expert-resolution.md) | `use` declarations, directory-per-expert, `mcl init`, lock file, error codes | Done |
| [Phase 11 — OCI Source Support](phases/phase-11-oci-sources.md) | `expert … from/version` grammar, OCI pull into `./experts`, `forge login`; prerequisite library published | Done |
| [Phase 12 — StepEnvelope](phases/phase-12-step-envelope.md) | Structured JSON envelope flowing through pipeline; fail-fast on any step failure; `MissionResult` carries status | Done |
| ~~Phase 13 — passes when~~ | Dropped — failure is declared in the expert MD, not the mission grammar. Bash exit-code model: all steps pass by default; any step returning `fail` stops the mission. | Dropped |
| [Phase 14 — loop N](phases/phase-14-loop.md) | `loop N` on the mission declaration; reserved variables `{{attempt}}` and `{{max_loops}}` injected by runtime | Done |
| [Phase 14.5 — Loop Demo](phases/phase-14.5-loop-demo.md) | `ContextOverloaded` (drunk expert, always self-passes) + `QualityJudge` demo showing loop converging on quality | Done |
| [Phase 15 — Token Streaming](phases/phase-15-streaming.md) | `IAsyncEnumerable<string>` from runner; chunks forwarded to `StepWriter` live; no more silent wait per expert | Done |
| [Phase 16 — FML → MCL Rename](phases/phase-16-fms-rename.md) | Full rename: binary (mcl), extension (.mcl), grammar, generated parser classes, docs. | Done |
| [Phase 17 — Provider Configuration](phases/phase-17-provider-config.md) | Make LLM provider fully configurable via `let` bindings (`provider`, `apiKey`, `model`, `endpoint`). Remove hardcoded OpenAI from CLI. | Done |
| [Phase 18 — Drop MAF](phases/phase-18-drop-maf.md) | Replace `MafExpertRunner` with `DirectExpertRunner` (direct `IChatClient` calls). Remove `Microsoft.Agents.AI` packages. Primary AOT unblocking step. | Done |
| [Phase 19 — Agent Runtime](phases/phase-19-agent-runtime.md) | `forge serve` + `agent.yaml`; expose a mission as an OAI-compatible endpoint via `Katasec.AgentHost` (separate library); one-mission-per-file constraint; stateful sessions via `ISessionStore`. | Done |
| [Phase 20 — Parser Project Extraction](phases/phase-20-parser-extraction.md) | Move `ForgeMission.Core/Parser` into a standalone `ForgeMission.Parser` project. Clean compiler/runtime boundary; enables reuse in tooling (language server, IDE plugins). | Done |
| [Phase 21 — Parallel Steps + Named Outputs](phases/phase-21-parallel-steps.md) | `parallel { }` block runs experts concurrently; each step's output available as `{{ExpertName.output}}`; fan-out/fan-in patterns. Syntax revised from `[A, B, C]` — see Phase 25 Spoke 1. | Done |
| [Phase 23 — Container Commands](phases/phase-23-container-commands.md) | `forge agent start/stop` and `forge webui start/stop` — run agent and Open WebUI in Docker; shared prereq checker with Spectre.Console TUI; Process.Start docker CLI (AOT-safe). Hub + 4 spokes. | Done |
| [Phase 24 — Copilot SDK Integration Tests](phases/phase-24-copilot-sdk-integration-tests.md) | Prove real AI coding agents (GitHub Copilot SDK, then Claude Code CLI) drive through an MCL mission end-to-end. OaiServer on a random port; BYOK points the agent at forge. Hub + 3 spokes. | Done |
| [Phase 25 Pre-flight — Open Design Decisions](phases/phase-25-preflight-design-decisions.md) | Eleven design decisions resolved: error messages, versioning, parallel failure, context accumulation, provider ambiguity, mission metadata, Hejlsberg/Pike review, `when()` conditional, loop convergence, syntax consolidation, mission composition. Blocking gate for Phase 25. | Done |
| [Phase 25 — Language & Manifest Evolution](phases/phase-25-language-manifest-evolution.md) | `->` operator, `parallel {}` block, `forge.toml` manifest, expert resolution (local-first), provider profiles. Two-file model: `mission.mcl` + `forge.toml`. Hub + 6 spokes. | Done |
| [Phase 25a — Expert Role Declaration](phases/phase-25a-expert-role.md) | `role: judge` field in expert frontmatter. `DirectExpertRunner` only injects fail/pass structured output semantics for judge experts; critic and other non-judge experts always receive a pass-only wrapper. Discovered when PitchCritic (a critic) stopped the pipeline because it found issues — the same behaviour a judge should have. Explicit opt-in beats silent default. | Done |
| [Phase 28 — Deterministic Experts & Rule Stdlib](phases/phase-28-deterministic-experts.md) | `kind: rule` in expert frontmatter. In-process deterministic checks (`word_count`, `json_parseable`, `contains_pattern`, etc.) with `check` expression and `onFail` feedback message. `RuleExpertRunner` integrates with loop convergence — `onFail` becomes the structured feedback on retry. Push determinism left, LLM judgment right. After Phase 22a. | Done |
| [Phase 32 — Safe Execution (`kind: exec`)](phases/phase-32-exec-expert-kind.md) | Add deterministic out-of-process execution as a first-class expert kind. `process` backend only: JSON stdin/stdout contract, multi-artifact expert packaging, `resources:` frontmatter for GPU declaration. Enables "reasoning, measuring, verifying" pattern. Hub + 6 spokes. `wasm` and `hyperlight` backends deferred — K8s pod boundary is the isolation primitive in production. Implemented in v0.7.0 (`process` backend). | Done |
| [Phase 33 — MCP Server (`forge mcp`)](phases/phase-33-forge-mcp.md) | `forge mcp <mission.mcl>` starts a stdio MCP server exposing the mission as a single tool. Claude Desktop (and any MCP-aware client) can call the mission directly — no Node.js, no sidecar, no extra runtime. Tool name = mission name; tool parameters = mission `inputs:` schema; tool response = mission output markdown. Uses official Microsoft `ModelContextProtocol` 1.4.0 NuGet (confirmed AOT-clean). Integration point: add one entry to `claude_desktop_config.json` and the mission appears as a native tool in the Claude UI. | Done |
| [Phase 38 — Forge Rooms (Agents as `@`-addressable members)](phases/phase-38-forge-rooms.md) | Native multi-party chat where a Forge Agent is `@`-addressable exactly like an LLM — the accessibility surface for the engine. Full decision log + status per sub-spoke in the hub. | **DONE (accessible + verified surface)** — 38.1–38.4a + 38.7 done; 38.5 accessible surface complete; forward-leaning pieces (save-as-agent, acquisition) moved to Phase 39. |
| &nbsp;&nbsp;↳ [38.1 Room Foundation](phases/phase-38.1-room-foundation.md) | Multi-party domain model + EF/Postgres + SignalR + minimal Blazor room view. | Done |
| &nbsp;&nbsp;↳ [38.2 Agent as Member](phases/phase-38.2-agent-as-member.md) | `@mention` → invoke mission with room-scoped context, pull-only. | Done |
| &nbsp;&nbsp;↳ [38.3 Trust Surface](phases/phase-38.3-trust-surface.md) | Verified badge + expandable trace, sender-attributed, trust-integrity guard. | Done |
| &nbsp;&nbsp;↳ [38.4 Identity & Membership](phases/phase-38.4-identity-membership.md) | Real OIDC, invite-link onboarding, membership = confidentiality boundary. | Done (real Entra External ID sign-in verified live) |
| &nbsp;&nbsp;↳ [38.4a UI Foundation, Auth Gating & Onboarding](phases/phase-38.4a-ui-and-onboarding.md) | Tokenized design system, "gate everything" auth IA, first-run starter room. | Done |
| &nbsp;&nbsp;↳ [38.5 Registry / GAL + Save-as-Agent](phases/phase-38.5-registry-save-as-agent.md) | `@handle` directory + promote a live chain into a named agent. | Accessible surface complete; save-as-agent (tasks 4/5) resequenced → 39.5 |
| &nbsp;&nbsp;↳ [38.6 Acquisition Loop](phases/phase-38.6-acquisition-loop.md) | Shareable verified outputs + share-an-agent links. | Resequenced → post-Phase 39 |
| &nbsp;&nbsp;↳ [38.7 Hosting & Deployment (Azure)](phases/phase-38.7-hosting-deployment.md) | ACA + ACR + Key Vault + Postgres via Bicep, passwordless CI, custom domain. | Done — live at `forge.katasec.com` |
| &nbsp;&nbsp;↳ [38.8 Mobile Access (Responsive + PWA)](phases/phase-38.8-mobile-access.md) | Responsive master/detail + installable PWA, no native apps. | Backlog — absorbed into [Phase 40](phases/phase-40-forge-ui-shell.md) |
| &nbsp;&nbsp;↳ [39.1 Containerized Mission Runner](phases/phase-39-metered-runtime-marketplace.md) | Extract mission execution into a stateless container service (`ForgeMission.Runner`). | ✅ Done + live |
| &nbsp;&nbsp;↳ [39.2 Cost Meter, Ledger & Credits](phases/phase-39-metered-runtime-marketplace.md) | Per-user cost-meter + balance ledger; F&F credits on the same ledger. | ✅ Done + live |
| &nbsp;&nbsp;↳ [39.3 Forge OCI Artifact Schema (B0)](phases/phase-39-metered-runtime-marketplace.md) | `artifactType` discriminator for expert vs mission OCI artifacts. | ✅ Done |
| &nbsp;&nbsp;↳ [39.4 OCI Mission Distribution](phases/phase-39-metered-runtime-marketplace.md) | Built-ins pulled by digest from `ghcr.io/katasec`, signature-verifiable. | ✅ Done + live |
| &nbsp;&nbsp;↳ [41.1 Grok web_search POC](phases/phase-41.1-grok-web-search.md) | `ForgeMission.Scout` + `IWebSearch` (provider-neutral) + `GrokWebSearch` backend. | ✅ Built + verified live |
| &nbsp;&nbsp;↳ [41.7 Streaming search progress + timeout hardening](phases/phase-41.7-streaming-progress.md) | Stream step-level progress so the ~40–60s search isn't a frozen spinner / idle-timeout risk. | ✅ ALL TASKS DONE + DEPLOYED LIVE (runner 0.7.0 / ui 0.4.2) — `plan.md` previously showed this as "Design (spec written)," a stale leftover; corrected 2026-08-09 after checking the spoke's own status line |
| &nbsp;&nbsp;↳ [42.1 Anthropic `serve` + full-conversation responder](phases/phase-42.1-anthropic-serve-responder.md) | `Katasec.AnthropicServer` wired into `forge serve`, full conversation history. | Done |
| &nbsp;&nbsp;↳ [42.2 `forge claude` local launcher](phases/phase-42.2-forge-claude-launcher.md) | One command: serve → export env → `exec claude` → teardown. | Done |
| &nbsp;&nbsp;↳ [42.3 Tool-capable enriching responder](phases/phase-42.3-tool-capable-enriching-responder.md) | Tool round-trip + enrich-once/re-entrancy gate — the load-bearing engineering. | Done |
| &nbsp;&nbsp;↳ [42.4 One `/v1` image: Docker ≡ ACA](phases/phase-42.4-container-convergence.md) | `forge serve` + `ForgeMission.Runner` converge onto one image, both wires. | Done |
| &nbsp;&nbsp;↳ [42.5 Platform identity & keys](phases/phase-42.5-platform-identity-keys.md) | `forge login` → platform key + free credits, usable as bearer token. | Done |
| &nbsp;&nbsp;↳ [42.6a Hosted artifacts + OCR demo](phases/phase-42.6a-hosted-artifacts-ocr.md) | Binary-artifact channel on the hosted API; `@ocr`, `@summarize`, URL input. | **DONE + LIVE** |
| &nbsp;&nbsp;↳ [43.1 Tool-execution engine](phases/phase-43.1-tool-execution-engine.md) | Forge executes `Read`/`Edit`/`Write`/`Bash` itself — no external `claude` CLI. | ✅ Done |
| &nbsp;&nbsp;↳ [43.7 Workspace provider abstraction](phases/phase-43.7-workspace-provider.md) | `IWorkspace` (multi-root, `LocalDiskWorkspace` v1). | ✅ Done |
| &nbsp;&nbsp;↳ ~~43.2 Avalonia vanilla shell~~ [(shelved)](phases/phase-43.2-avalonia-vanilla-shell.md) | Spike — Tasks 1–3 worked, Task 4 abandoned; code removed once WASM/Photino proved out. | Shelved, code removed 2026-08-01 |
| &nbsp;&nbsp;↳ [43.2 Electron Forge Desktop shell](phases/phase-43.2-electron-forge-desktop-shell.md) | Superseded track — see [Architecture](phases/phase-43-forge-desktop.md#architecture-2026-08-01--supersedes-the-electronblazor-server-decision-below) for why. | Superseded by 43.8–43.11 |
| &nbsp;&nbsp;↳ [43.13 Mission Runtime resolution & orchestration](phases/phase-43.13-mission-runtime-orchestration.md) | Shared `ForgeMission.Orchestration` (start/find/teardown the Mission Runtime), surface-agnostic. | ✅ Done 2026-08-04 |
| &nbsp;&nbsp;↳ [43.14 Desktop cloud missions via API A](phases/phase-43.14-desktop-cloud-missions.md) | Desktop reaches cloud missions through API A (small additive extension), not API B. | ✅ **DONE + LIVE 2026-08-08** — all 10 tasks, 4 named live observations, see its own [_completed doc](phases/phase-43.14-desktop-cloud-missions_completed.md) |
| [Phase 40 — Forge UI App Shell & Responsive Foundation](phases/phase-40-forge-ui-shell.md) | Multi-surface, mobile-first app shell (rail↔bottom-tab-bar nav: Rooms · Library · Account); absorbs [38.8](phases/phase-38.8-mobile-access.md). Full status per sub-spoke in the hub. | **✅ COMPLETE + LIVE** (`forge-ui:0.3.4`, user-verified installable) |
| &nbsp;&nbsp;↳ [40.1 Design System Foundation](phases/phase-40.1-design-system-foundation.md) | Global-safe mobile primitives (dead-CSS prune, `100dvh`, iOS input-zoom fix, safe-area insets, breakpoint convention). | Done |
| &nbsp;&nbsp;↳ [40.2 App Navigation Shell](phases/phase-40.2-app-navigation-shell.md) | Net-new nav layer: `NavShell.razor`, `/library`, `/account`. | Done (2026-07-12, verified in-browser) — `plan.md` previously showed this as "Design"; corrected 2026-08-09 after checking the spoke's own status line and confirming `NavShell.razor`/`Library.razor`/`Account.razor` exist in code |
| &nbsp;&nbsp;↳ [40.3 Responsive Surface Collapse](phases/phase-40.3-responsive-collapse.md) | Rooms' two-pane → master/detail on mobile; absorbs 38.8 Task 1. | Done (2026-07-12, verified in-browser) — same correction as 40.2 |
| &nbsp;&nbsp;↳ [40.4 PWA Shell](phases/phase-40.4-pwa-shell.md) | Installable, online-only PWA (manifest, icons, deliberate service worker); absorbs 38.8 Task 2. | **✅ Done + live** |

## Resolved open issues

### 1. OaiServer — full OAI spec compliance + Responses API

`katasec/oaiserver` was built against an informal reading of the spec rather than the authoritative
OpenAPI definition; the openai Python SDK 2.x could not parse the streaming response. Root cause:
first SSE chunk emitted `role: null` instead of `"role": "assistant"` — the SDK aborted
deserialization.

**What was done:** (a) pinned the official OpenAI OpenAPI spec in
`oai-server-dotnet/spec/openai-openapi.yaml` with a `make update-spec` target; (b) wrote
spec-compliance tests using the official `OpenAI` NuGet SDK (v2.11.0) as the test client pointed at
an in-process server — Kiota was considered and rejected because `ResponsesClient` / all needed
types are already bundled in the `OpenAI` package and Kiota would add a large generated-file
maintenance burden with no benefit; (c) tests committed with 17 passing (non-streaming chat +
models already compliant) and 5 failing — `Streaming_FirstUpdate_HasRoleAssistant` (the streaming
bug) plus all 4 `/v1/responses` tests (endpoint not yet implemented). Fixed the streaming role
emission bug and implemented `POST /v1/responses` (OpenAI's 2025 Responses API — the forward-looking
surface; `chat/completions` is on a deprecation path). All 28 spec tests pass (non-streaming +
streaming for all three endpoints).

**Status:** Done — pending Cli bump (`katasec/oaiserver` package version bump in
`ForgeMission.Cli` still outstanding as of the last check).

### 2. Python integration example

`clients/python/client.py` now uses the official `openai` SDK targeting `/v1/responses`.
`pip install openai` + `forge serve` is all that's needed.

**Status:** Done.

### 3. Provider-key IaC drift (38.5 task 7) — dev

The dev ForgeUI app was wired for `@claude`/`@grok` by adding KV secrets `Anthropic-ApiKey` +
`Xai-ApiKey` and container env vars `ANTHROPIC_API_KEY` / `XAI_API_KEY` **manually via `az`
(2026-07-08)** — not in the `forge-infra` `dev/500-app` Bicep, so a `dev/500-app` redeploy would
revert them. Also the `forge-ui-image` workflow builds+pushes but does not roll the Container App
(rollout is a separate step).

**Fix applied:** forge-infra `6fd0fd3` wires both secrets + env vars into `dev/500-app` Bicep (on
the **runner**, where 39.1 execution lives) and applied live; vault name reconciled
`kv-forge-dev`→`kv-forgerooms-dev` (`5e859ec`). This was causing the `@claude` "No mission is bound"
regression; also needed a core structured-output fix + Anthropic account funding — see
[38.7 §9](phases/phase-38.7-hosting-deployment.md).

**Status:** Done 2026-07-09.

### 4. `ExecExpertRunnerTests` Windows hygiene (8 pre-existing failures)

Known since Phase 43.1, carried unresolved through 43.7 and 43.2 Tasks 1–3, confirmed each time as
zero-diff/pre-existing. Root cause confirmed 2026-07-26: the test helper hardcoded
`Command: "python3"`; on this Windows box both `python` and `python3` (and `py`) resolve to nothing
working — either a non-functional Windows App Execution Alias stub or, for `py`, no launcher
installed at all.

**Fix applied** (implemented by Codex, independently re-verified by Claude): `ExecExpertRunnerTests.cs`
now probes `python3`/`python`/`py -3` by actually **executing**
`import json,sys; print(json.dumps({'major': sys.version_info[0]}))` (not just checking PATH — a
`File.Exists`-only check would wrongly accept the non-functional stub, which is a real file), cached
in a `static readonly Lazy<>` so the probe runs once per test run, not once per test. All 8 tests
converted `[Fact]` → `[SkippableFact]` (verified: 8/8, zero remaining `[Fact]`), skipping with a
clear reason when no working interpreter is found rather than failing red. Reproduced independently:
focused filter → 8/8 skip cleanly (no working interpreter on this box); full suite → same
established zero-regression bar. Scope correctly stayed test-infra-only, no production
`ExecExpertRunner.cs` change.

**Status:** Done 2026-07-26.

### 5. WDAC intermittently blocking locally-built DLLs (this Windows dev box only)

First seen by Codex on `ForgeMission.Rooms.Tests.dll`; independently hit by Claude on
`ForgeMission.Desktop.dll`, breaking a Desktop test (`FileLoadException`, `0x800711C7`) that had
passed cleanly earlier the same session. Did not clear after `dotnet clean` + rebuild. Likely
triggered by directly launching the unsigned `ForgeMission.Desktop.exe` a few times that session
(AOT-publish verification).

**Root cause pinned down 2026-07-26** via `Get-WinEvent -LogName
Microsoft-Windows-CodeIntegrity/Operational` (events 3033/3077): a real **WDAC (Windows Defender
Application Control) Code Integrity policy**, not Smart App Control — `Add-MpPreference` exclusions
(Defender AV) do not affect it. Policy ID `{0283AC0F-FFF1-49AE-ADA1-8A933130CAD6}.cip` exists
locally, dated 01/04/2024 — machine confirmed **not** domain/Azure-AD/Enterprise-joined
(`dsregcmd /status`), so this is a default Windows-shipped baseline policy, not an org-managed one
nobody local can touch.

**Decision:** not a code defect, not caused by any change, intermittent — logged so a future agent
hitting a sudden unrelated `FileLoadException` on this machine doesn't mistake it for a regression
or waste time on a Defender-exclusion fix that won't work. Deliberately not pursuing a WDAC policy
edit — disproportionate to "one flaky test." No action item unless it starts actually blocking work.

**Status:** Environment-only, root-caused, closed — no fix planned unless it starts blocking real work.

### 6. Migration job DB-wipe risk

The migration job definition and actually running a migration were previously one coupled step,
meaning updating the job definition could unintentionally trigger a real migration against the dev
database. Defused 2026-07-18, structurally fixed 2026-07-19 — full incident + investigation write-up
in [phase-42.6-hosted-endpoint-ttfa_completed.md](phases/phase-42.6-hosted-endpoint-ttfa_completed.md#migration-job-db-wipe--defused-2026-07-18-structurally-fixed-2026-07-19)
(not duplicated here). The structural fix: deploying a migration job definition
(`make 450-migrate`) and starting a migration are now two separate deliberate steps — see
[AGENTS.md](../AGENTS.md#deploying-the-hosted-app-forge-infra--separate-from-the-release-workflow-above).

**Status:** Closed — not a deploy gate, structural recurrence prevention in place.

## Resolved "under discussion" items

### Mission Composition

Missions usable as steps in other missions — explicit parameter binding, isolated child context,
failure propagation, arbitrary depth. `PipelineRunner` recursively dispatches when a step name
matches a `MissionDeclaration`. Reference example: `missions/sdlc-agent/` — Classifier routes to
`DesignMode` (loop+judge) or `TaskMode` sub-missions. 10 new tests.

**Status:** Done.

### Skills and Tools

Superseded 2026-07-25 — scoped and committed as
[Phase 43.1 — Tool-execution engine](phases/phase-43.1-tool-execution-engine.md).

**Status:** Done (scoped into 43.1).

### Parallel steps runtime model

**Decision (Phase 21):** `Task.WhenAll` with a linked `CancellationTokenSource` for fail-fast.
Channel-based streaming deferred — no demand yet.

**Status:** Done — decided, not revisited since.

### Context bag typing

Currently all values are strings. Typed values (float, bool, byte[]) needed for non-LLM stages.

**Decision (Phase 22b):** keep the bag as `Dictionary<string, object>`, add `double` alongside
strings. LLM interpolation calls `.ToString()` automatically. Strongly-typed envelope deferred.

**Status:** Done — decided, not revisited since.
