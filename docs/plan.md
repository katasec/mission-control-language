# FML — Implementation Plan

## Phases

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
| [Phase 22 — Non-LLM Expert Kinds](phases/phase-22-non-llm-experts.md) | `kind` field in expert frontmatter (`llm` default, `onnx`, `http`). Static runner dispatch, no reflection. Context bag gains typed numeric values. Motivated by log anomaly detection (UC-3). | Partial (22a done: kind dispatch + HttpExpertRunner; 22b ONNX deferred) |
| [Phase 23 — Container Commands](phases/phase-23-container-commands.md) | `forge agent start/stop` and `forge webui start/stop` — run agent and Open WebUI in Docker; shared prereq checker with Spectre.Console TUI; Process.Start docker CLI (AOT-safe). Hub + 4 spokes. | Done |
| [Phase 24 — Copilot SDK Integration Tests](phases/phase-24-copilot-sdk-integration-tests.md) | Prove real AI coding agents (GitHub Copilot SDK, then Claude Code CLI) drive through an MCL mission end-to-end. OaiServer on a random port; BYOK points the agent at forge. Hub + 3 spokes. | Done |
| [Phase 25 Pre-flight — Open Design Decisions](phases/phase-25-preflight-design-decisions.md) | Eleven design decisions resolved: error messages, versioning, parallel failure, context accumulation, provider ambiguity, mission metadata, Hejlsberg/Pike review, `when()` conditional, loop convergence, syntax consolidation, mission composition. Blocking gate for Phase 25. | Done |
| [Phase 25 — Language & Manifest Evolution](phases/phase-25-language-manifest-evolution.md) | `->` operator, `parallel {}` block, `forge.toml` manifest, expert resolution (local-first), provider profiles. Two-file model: `mission.mcl` + `forge.toml`. Hub + 6 spokes. | Done |
| [Phase 25a — Expert Role Declaration](phases/phase-25a-expert-role.md) | `role: judge` field in expert frontmatter. `DirectExpertRunner` only injects fail/pass structured output semantics for judge experts; critic and other non-judge experts always receive a pass-only wrapper. Discovered when PitchCritic (a critic) stopped the pipeline because it found issues — the same behaviour a judge should have. Explicit opt-in beats silent default. | Done |
| [Phase 26 — Tooling Foundation](phases/phase-26-tooling-foundation.md) | Source positions on AST nodes, TextMate grammar (syntax highlighting), Tree-sitter grammar (incremental parsing), LSP server (completion, hover, go-to-definition). After grammar stabilises in Phase 25. Hub + 4 spokes. | Todo |
| [Phase 27 — Project Assistant Missions](phases/phase-27-project-assistant.md) | Three-layer mission composition: `project-assistant` (generic hub/spoke ops), `software-project-assistant` (extends with architect + developer modes), `product-owner-assistant` (extends with PO-specific experts). Served behind `forge serve` and pointed at by Claude Code — MCL intercepts every request and routes it through the right expert chain. Self-hosting demonstration. | Design |
| [Phase 28 — Deterministic Experts & Rule Stdlib](phases/phase-28-deterministic-experts.md) | `kind: rule` in expert frontmatter. In-process deterministic checks (`word_count`, `json_parseable`, `contains_pattern`, etc.) with `check` expression and `onFail` feedback message. `RuleExpertRunner` integrates with loop convergence — `onFail` becomes the structured feedback on retry. Push determinism left, LLM judgment right. After Phase 22a. | Done |

## Under discussion

| Topic | Description |
|-------|-------------|
| ~~Mission Composition~~ | Missions usable as steps in other missions — explicit parameter binding, isolated child context, failure propagation, arbitrary depth. `PipelineRunner` recursively dispatches when a step name matches a `MissionDeclaration`. Reference example: `missions/sdlc-agent/` — Classifier routes to `DesignMode` (loop+judge) or `TaskMode` sub-missions. 10 new tests. | Done |
| Multi-Agent Debate (`debate {}` block) | Round orchestration, per-round context summarisation, cross-agent output wiring. Deferred from Phase 25; needs a dedicated phase. Research-backed default: rounds: 3, warn beyond 5. |
| Skills and Tools | Review hub/spoke architecture for expert-level tool-calling support (function calls, MCP tools, shell commands). Decide scope, grammar extension, and AOT-safe dispatch before committing to an implementation phase. |
| Parallel steps runtime model | Decide whether parallel steps use Task.WhenAll (simple) or a channel-based streaming approach (better for token streaming). Consider cancellation on first failure. |
| Context bag typing | Currently all values are strings. Typed values (float, bool, byte[]) needed for non-LLM stages. Decide schema: loose dictionary with type tags vs. strongly-typed envelope. |
| Language governance process | Java uses JSRs, C# uses Language Design Meeting notes, Go uses a formal proposal process (golang/proposal). Key design decisions are currently recorded in ad-hoc markdown files. A standardised proposal format — problem, prior art, alternatives considered, decision, rationale — would make decisions traceable and give future contributors clear reasoning rather than just outcomes. Decide format, location (`docs/proposals/`?), and whether past decisions (Phase 25 pre-flight) are backfilled. |

## Design docs

| Doc | Description |
|-----|-------------|
| [Language Design](design/language.md) | Grammar, syntax decisions, primitives, capitalisation rationale |
| [Standard Library](design/stdlib.md) | Definition of what qualifies as a stdlib expert — four gates, current members, worked examples |
| [Architecture](design/architecture.md) | Components, boundaries, dependency flow |
| [Interaction Modes & Classifier-Router Pattern](design/interaction-modes.md) | Human-AI collaboration modes, classifier as HAProxy, SDLC meta-mission, `when {}` conditional step primitive |
| [SDLC Meta-Mission](design/sdlc-meta-mission.md) | Planned reference example — mission composition + debate{} + routing in one file; feature gap analysis and build order |
| [Research Foundations](design/research.md) | Academic literature mapped to MCL design decisions — Self-Refine, Reflexion, Multi-Agent Debate, Constitutional AI, MoE routing, MoA |
| [MAF Research](design/maf.md) | Microsoft Agent Framework 1.0 spike findings |
| [Methodology](design/methodology.md) | The broader engineering approach MCL fits into |
| [Why MCL exists](why.md) | Origin, core thesis, methodology, thinking models |
