# Forge Mission Language (FML)

A minimal language for expressing how a problem should be reasoned about — as a composition of experts.

---

## What it is

Forge Mission Language lets you define a **mission** — a problem or desired outcome — and express how it should be reasoned about as a pipeline of **experts**.

```fsharp
let goal    = "Design a production-grade K8s build operator"
let persona = "Principal SRE, Tekton specialist"

mission BuildOperatorDesign(goal, persona) =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer with { style = "terse ADR" }
```

This is not an execution plan. It is a reasoning structure. Each expert applies a lens, refines the previous output, and passes a better-constrained result to the next. The `|>` operator is progressive refinement, not function composition.

The language has five constructs: `mission`, `expert`, `|>`, `let`, and `with`. That is intentional.

Experts are the composable unit. Over time, a registry of experts representing common reasoning capabilities — security review, cost analysis, risk assessment, principal review — can be assembled, versioned, and reused across many missions and problem domains. Most problems in IT are not novel; they are familiar problem shapes applied to new contexts. A well-stocked registry of experts makes that reuse explicit.

---

## Where it came from

FML emerged from six months of real-world LLM usage across meaningfully different problem domains: production debugging on custom applications, understanding and debugging complex infrastructure defined in IaC, Kubernetes and Helm operations, and software development across multiple languages and stacks.

In every case, getting reliable output required the same manual work: decompose the problem, identify the relevant reasoning lenses, sequence them deliberately, and structure the handoff between them. That process was always implicit — buried in ad-hoc prompts, markdown files, and trial and error.

FML is the codification of that process. It makes the reasoning structure explicit, named, composable, and reviewable.

---

## The broader methodology

FML does not stand alone. It is one layer in a deliberate engineering approach to LLM-driven work:

1. **Design first** — never execute cold. Iterate on design until it is solid before any implementation begins.
2. **Phase decomposition** — break the design into agreed phases. Each phase is a meaningful, bounded unit of work.
3. **Atomic task generation** — per phase, generate tasks in sequential dependency order so each can be executed and tested before the next begins.
4. **Narrow execution** — by the time an agent executes, the work is so well-prescribed that there is little room for drift. The design thinking is already done.
5. **Oversight** — an architect agent reviews the work of the executing agent, catches omissions, and enforces quality gates including testing.
6. **Session continuity** — agent performance degrades as context fills. Sessions are treated as bounded units with structured handoffs, so a fresh agent picks up exactly where the last left off — at full capacity, with full context of what is done and what remains.

FML addresses layer one and three of this stack: expressing the reasoning structure of a design, and giving executing agents a clear, reviewable definition of how to approach a problem.

Without an explicit reasoning structure, agents work from vague instructions and produce inconsistent output. With one, the work is prescriptive enough to execute reliably and narrow enough to test.

---

## Core thesis

Large language models perform best when reasoning is constrained through deliberate decomposition and the application of expertise.

A single general-purpose prompt asks the model to architect, review, challenge, and conclude simultaneously. Expert composition asks each lens to do one thing well, in sequence, with the previous output as input.

**A mission is a reasoning structure, not an execution plan.**

---

## Why explicit reasoning structures matter

Most AI tooling expresses reasoning through prompts, markdown instructions, YAML, or agent configuration. This works at small scale, but the result is:

- **ambiguous** — intent is buried in prose, not structure
- **not composable** — reasoning patterns cannot be named, reused, or shared
- **not reviewable** — a human or oversight agent cannot inspect the reasoning approach, only the output
- **not handoff-friendly** — a fresh agent session cannot reconstruct how a problem was being reasoned about
- **execution-focused** — tool calls, retries, model selection get more attention than the reasoning itself

FML addresses all of these by making the reasoning structure a first-class artifact — something that can be written, reviewed, versioned, and handed to any agent session. And because experts are named, portable artifacts, they can be shared and sourced from a registry — accumulated practice that any team can pull and compose rather than rebuild from scratch.

---

## Syntax

### Primitives

| Construct | Meaning |
|-----------|---------|
| `mission` | A problem or desired outcome |
| `expert`  | A reusable reasoning capability |
| `\|>`      | Progressive refinement / expert composition |
| `let`     | Bind a value into the context bag |
| `with`    | Override context keys for a specific step |
| `use`     | Declare an expert source (local path or OCI registry) |

Expert names are PascalCase. Variable names and keywords are lowercase. This is enforced by the grammar — experts are proper nouns representing roles, and the visual distinction from keywords matters when reading a pipeline.

### A complete mission

```fsharp
use "./experts"

let goal    = "Design a production-grade K8s build operator"
let persona = "Principal SRE, Tekton specialist"

mission BuildOperatorDesign(goal, persona) =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer with { style = "terse ADR" }
```

`use` declares where experts come from. `let` bindings seed a context bag that flows through every step. Expert system prompts use `{{goal}}`, `{{persona}}`, `{{style}}` placeholders — interpolated at runtime before each step runs.

### Environment variables

API keys and secrets stay out of source files via `env()`:

```fsharp
let apiKey = env("OPENAI_API_KEY")
let model  = env("FML_MODEL", "gpt-4o-mini")   // second arg = default
```

`env()` resolves at runtime when seeding the context bag. Missing required vars fail with a clear error before any expert runs.

### Composing experts

Experts can be composed from other experts, giving the language recursive decomposition:

```fsharp
expert KubernetesArchitect =
    RequirementsAnalyst
    |> PlatformArchitect
    |> ReliabilityArchitect
```

### Per-step overrides

`with` injects or overrides context keys for a single step without affecting the rest of the pipeline:

```fsharp
mission BuildOperatorDesign =
    KubernetesArchitect
    |> PrincipalReviewer with { style = "terse ADR", audience = "C-suite" }
```

### Expert definitions (markdown-backed)

Each leaf expert is backed by a markdown file. The system prompt uses `{{key}}` placeholders:

```markdown
---
name: KubernetesArchitect
input: Task description
output: Kubernetes architecture design
---

You are a senior Kubernetes architect.

Goal: {{goal}}
Perspective: {{persona}}

Produce a concrete architecture covering CRD design, controller structure, RBAC, and operational concerns.
```

---

## Variable resolution order

From lowest to highest precedence:

1. `let` binding (including `env()` calls)
2. `with { }` clause on a step (merges into context before that step)
3. `--var key=value` CLI flag (overrides everything at run time)

---

## Grammar

```antlr
program    : (letBinding | declaration)* EOF ;
letBinding : 'let' LOWER_ID '=' value ;
declaration : mission | expert ;
mission    : 'mission' UPPER_ID params? '=' pipeline ;
expert     : 'expert'  UPPER_ID params? '=' pipeline ;
params     : '(' LOWER_ID (',' LOWER_ID)* ')' ;
pipeline   : step ('|>' step)* ;
step       : UPPER_ID withClause? ;
withClause : 'with' '{' binding (',' binding)* '}' ;
binding    : LOWER_ID '=' value ;
value      : STRING | LOWER_ID | envCall ;
envCall    : 'env' '(' STRING (',' STRING)? ')' ;
STRING     : '"' (~["\r\n])* '"' ;
UPPER_ID   : [A-Z][a-zA-Z0-9]* ;
LOWER_ID   : [a-z][a-zA-Z0-9]* ;
```

The parser is generated by ANTLR4 from [`src/ForgeMission.Core/Parser/FmlGrammar.g4`](src/ForgeMission.Core/Parser/FmlGrammar.g4).

---

## Thinking models FML can express

The same language expresses many common reasoning structures through expert composition. The thinking models below are themselves a reusable catalog — proven approaches to recurring problem shapes that can be matched to new problems without reinventing the reasoning structure each time.

### Progressive refinement

```fsharp
mission BuildOperator =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
```

Each expert improves or constrains the previous output.

### Hierarchical decomposition

```fsharp
expert KubernetesArchitect =
    RequirementsAnalyst
    |> PlatformArchitect
    |> ReliabilityArchitect
```

A high-level expert is decomposed into smaller, more focused experts.

### Separation of concerns

```fsharp
mission DesignPlatform =
    PlatformArchitect
    |> SecurityReviewer
    |> CostReviewer
    |> ReliabilityReviewer
```

Each expert applies a distinct concern to the same problem.

### Scientific method

```fsharp
mission ValidateIdea =
    HypothesisBuilder
    |> ExperimentDesigner
    |> EvidenceReviewer
    |> ConclusionWriter
```

### OODA loop

```fsharp
mission RespondToIncident =
    Observer
    |> Orienter
    |> DecisionMaker
    |> Remediator
```

### Adversarial review

```fsharp
mission ReviewArchitecture =
    Architect
    |> Skeptic
    |> RiskReviewer
    |> PrincipalReviewer
```

The goal is not just to produce a plan but to challenge it.

### Tradeoff analysis

```fsharp
mission ChooseArchitecture =
    OptionGenerator
    |> TradeoffAnalyst
    |> ScenarioModeler
    |> DecisionAdvisor
```

### Meta-advisory (future)

A meta expert that helps design missions from a problem statement. Given a problem, it selects the appropriate thinking model and assembles a mission from experts sourced from the registry — lowering the barrier for users who can describe their problem but may not know how to reason about it:

```fsharp
expert MetaAdvisor =
    ProblemFramer
    |> ThinkingModelSelector  -- matches the problem to a proven reasoning model
    |> MissionDesigner        -- assembles the right experts from the registry
    |> MissionReviewer        -- challenges the proposed composition
```

Given:
```text
I need to migrate 300 Terraform modules to a new platform.
```

Suggests a mission composed from existing registry experts:
```fsharp
mission TerraformMigration =
    DiscoveryAnalyst
    |> DependencyMapper
    |> MigrationArchitect
    |> RiskReviewer
    |> PrincipalReviewer
```

This is the long-term purpose of the registry: not just reuse, but accessibility. Most problems in IT are well-understood by someone. A registry of experts encodes that understanding in a form that anyone can compose and apply.

---

## Testable hypothesis

> Expert composition improves reasoning quality, consistency, and outcomes compared to a single general-purpose prompt.

The `build-operator` example tests this: the same problem is run through a composed mission and a single general-purpose prompt, and the outputs are compared for quality, consistency, and reviewability. Findings are documented in [`docs/findings.md`](docs/findings.md).

---

## CLI

The workflow mirrors Terraform — init once, then validate and run:

```pwsh
# Resolve expert sources and generate fms.lock
fms init mission.fms

# Validate — checks all experts resolve, no circular refs, lock file is current
fms validate mission.fms

# Run a mission against an input
fms run mission.fms --input input.md

# Override let bindings at run time
fms run mission.fms --input input.md --var goal="Redesign for ARM"

# List available experts
fms list experts

# Scaffold a new expert
fms expert init SecurityArchitect
```

`fms run` fails immediately if `fms.lock` is absent — init is not optional.

Output lands in `runs/<MissionName>/` (gitignored):

```text
runs/
  BuildOperatorDesign/
    01-KubernetesArchitect.md
    02-SecurityArchitect.md
    03-PrincipalReviewer.md
    final.md
```

---

## Non-goals

The following are explicitly out of scope for the language:

- Low-level tool call syntax
- Retry and error-handling mechanics
- Model provider selection syntax
- Vector store or retrieval configuration
- Agent loop internals
- Workflow-engine plumbing
- Complex DAG or branching syntax

The language should remain small unless a new construct clearly improves reasoning composition.

---

## Repository structure

```text
forge-mission-language/
  README.md
  docs/
    plan.md               # hub — all phases
    findings.md           # Phase 7 hypothesis validation
    design/               # language design, architecture, methodology
    phases/               # per-phase specs and results
  src/
    ForgeMission.Core/    # Parser (ANTLR4), AST, pipeline runner, expert resolution
    ForgeMission.Cli/     # CLI (fms init / run / validate / list / expert)
    ForgeMission.Tests/   # Unit and integration tests
  missions/
    build-operator/
      mission.fms
      fms.lock             # committed — reproducible expert resolution
      input.md
      experts/
        KubernetesArchitect/
          expert.md
        SecurityArchitect/
          expert.md
        PrincipalReviewer/
          expert.md
  runs/                   # gitignored — output of fms run
  .fms/                   # gitignored — resolved local state (regenerated by fms init)
```

---

## Status

Working prototype. All ten phases complete.

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Project scaffold | Done |
| 2 | Parser | Done |
| 3 | Expert loader | Done |
| 4 | Pipeline runner | Done |
| 5 | MAF adapter (OpenAI via Microsoft Agent Framework) | Done |
| 6 | CLI (`fms run`, `fms validate`, `fms list experts`) | Done |
| 7 | Validation — build-operator end-to-end, hypothesis tested | Done |
| 8 | ANTLR migration — hand-rolled parser replaced | Done |
| 9 | Variables — `let`, params, `with`, `env()`, context bag runtime | Done |
| 10 | Expert resolution — `use`, directory-per-expert, `fms init`, lock file, error codes | Done |

See [`docs/plan.md`](docs/plan.md) for the full implementation plan.
