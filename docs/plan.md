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
| [Phase 10 — Expert Resolution](phases/phase-10-expert-resolution.md) | `use` declarations, directory-per-expert, `fms init`, lock file, error codes | Done |
| [Phase 11 — OCI Source Support](phases/phase-11-oci-sources.md) | `use "oci://..."` runtime resolution, pull from OCI registry | Blocked |
| [Phase 12 — StepEnvelope](phases/phase-12-step-envelope.md) | Structured JSON envelope flowing through pipeline; fail-fast on any step failure; `MissionResult` carries status | Pending |
| ~~Phase 13 — passes when~~ | Dropped — failure is declared in the expert MD, not the mission grammar. Bash exit-code model: all steps pass by default; any step returning `fail` stops the mission. | Dropped |
| [Phase 14 — loop N](phases/phase-14-loop.md) | `loop N` on the mission declaration; reserved variables `{{attempt}}` and `{{max_loops}}` injected by runtime | Pending |
| [Phase 14.5 — Loop Demo](phases/phase-14.5-loop-demo.md) | `ContextOverloaded` (drunk expert, always self-passes) + `QualityJudge` demo showing loop converging on quality | Pending |
| [Phase 15 — Token Streaming](phases/phase-15-streaming.md) | `IAsyncEnumerable<string>` from runner; chunks forwarded to `StepWriter` live; no more silent wait per expert | Pending |
| [Phase 16 — FML → FMS Rename](phases/phase-16-fms-rename.md) | Surface rename: CLI binary `fml`→`fms`, extension `.fml`→`.fms`, docs. C# namespaces unchanged. | Pending |

## Design docs

| Doc | Description |
|-----|-------------|
| [Language Design](design/language.md) | Grammar, syntax decisions, primitives, capitalisation rationale |
| [Architecture](design/architecture.md) | Components, boundaries, dependency flow |
| [MAF Research](design/maf.md) | Microsoft Agent Framework 1.0 spike findings |
| [Methodology](design/methodology.md) | The broader engineering approach FML fits into |
