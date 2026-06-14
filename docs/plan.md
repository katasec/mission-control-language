# FML — Implementation Plan

## Phases

| Phase | Description | Status |
|-------|-------------|--------|
| [Phase 1 — Project Scaffold](phases/phase-1-scaffold.md) | Solution structure, projects, package references | Done |
| [Phase 2 — Parser](phases/phase-2-parser.md) | Lexer, token stream, recursive-descent parser, AST | Done |
| [Phase 3 — Expert Loader](phases/phase-3-expert-loader.md) | Resolve expert names to markdown, parse frontmatter, validate | Not Started |
| [Phase 4 — Pipeline Runner](phases/phase-4-pipeline-runner.md) | Orchestration loop, IExpertRunner interface, output writer | Not Started |
| [Phase 5 — MAF Adapter](phases/phase-5-maf-adapter.md) | Implement IExpertRunner using Microsoft Agent Framework | Not Started |
| [Phase 6 — CLI](phases/phase-6-cli.md) | fml run, fml validate, fml list experts | Not Started |
| [Phase 7 — Validation](phases/phase-7-validation.md) | Build build-operator example, test hypothesis, document findings | Not Started |

## Design docs

| Doc | Description |
|-----|-------------|
| [Language Design](design/language.md) | Grammar, syntax decisions, primitives, capitalisation rationale |
| [Architecture](design/architecture.md) | Components, boundaries, dependency flow |
| [MAF Research](design/maf.md) | Microsoft Agent Framework 1.0 spike findings |
| [Methodology](design/methodology.md) | The broader engineering approach FML fits into |
