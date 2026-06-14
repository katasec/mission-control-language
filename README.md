# Forge Mission Language (FML)

A minimal, human-readable language for expressing how a problem should be reasoned about — not merely how a runtime should execute tasks.

---

## What is Forge Mission Language?

Forge Mission Language is a prototype language for expressing structured reasoning through the composition of experts.

A **mission** describes a problem or desired outcome. An **expert** is a reusable reasoning capability. The `|>` operator composes experts into a progressive reasoning pipeline.

```fsharp
mission BuildOperator =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
```

This is not an execution plan. It is a reasoning structure: apply the Kubernetes architecture lens, then the security lens, then the principal review lens. Each expert refines and constrains the output of the previous one.

---

## Why does it exist?

Most AI tooling expresses structured reasoning through prompts, markdown instructions, YAML, tool calls, or agent configuration. This works, but the result is often:

- ambiguous — intent is buried in prose
- difficult to compose — reasoning structures are not reusable
- difficult to validate — there is no schema to check against
- difficult to review — the reasoning flow is implicit
- focused on execution mechanics — tool calls, retries, model selection — rather than the reasoning itself

Users end up manually inventing prompt-driven and markdown-driven workflows to improve reliability. FML makes those reasoning structures explicit and reviewable.

---

## Core thesis

Large language models perform best when reasoning is constrained through deliberate decomposition and the application of expertise.

Forge Mission Language provides a human-readable way to express that decomposition as a composition of experts.

**A mission is a reasoning structure, not an execution plan.**

---

## Syntax

The initial language has three primitives:

| Primitive | Meaning |
|-----------|---------|
| `mission` | A problem or desired outcome |
| `expert`  | A reusable reasoning capability |
| `\|>`      | Progressive refinement / expert composition |

### Defining a mission

```fsharp
mission BuildOperator =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
```

### Defining a composed expert

Experts can themselves be composed from other experts, giving the language recursive composition:

```fsharp
expert KubernetesArchitect =
    RequirementsAnalyst
    |> PlatformArchitect
    |> ReliabilityArchitect
```

### Expert definitions (markdown-backed)

Each expert is backed by a markdown file that describes its reasoning role:

```markdown
---
name: KubernetesArchitect
input: MissionBrief
output: ArchitectureProposal
---

You are a Kubernetes platform architect.

Your job is to:
- understand the mission
- propose a practical architecture
- identify tradeoffs
- explain operational risks
- produce a clear architecture proposal
```

---

## Thinking models FML can express

FML is not tied to a single problem-solving pattern. The same language can express several common reasoning models through expert composition.

### 1. Progressive refinement

```fsharp
mission BuildOperator =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
```

Each expert improves or constrains the previous output.

### 2. Hierarchical decomposition

```fsharp
expert KubernetesArchitect =
    RequirementsAnalyst
    |> PlatformArchitect
    |> ReliabilityArchitect
```

A high-level expert is decomposed into smaller, more focused experts.

### 3. Separation of concerns

```fsharp
mission DesignPlatform =
    PlatformArchitect
    |> SecurityReviewer
    |> CostReviewer
    |> ReliabilityReviewer
```

Each expert applies a distinct concern to the same output.

### 4. Scientific method

```fsharp
mission ValidateIdea =
    HypothesisBuilder
    |> ExperimentDesigner
    |> EvidenceReviewer
    |> ConclusionWriter
```

Encodes hypothesis, testing, evidence, and conclusion as a pipeline.

### 5. OODA loop

```fsharp
mission RespondToIncident =
    Observer
    |> Orienter
    |> DecisionMaker
    |> Remediator
```

Useful for incident response and operational decision-making.

### 6. Adversarial review

```fsharp
mission ReviewArchitecture =
    Architect
    |> Skeptic
    |> RiskReviewer
    |> PrincipalReviewer
```

The goal is not just to produce a plan, but to challenge it.

### 7. Tradeoff analysis

```fsharp
mission ChooseArchitecture =
    OptionGenerator
    |> TradeoffAnalyst
    |> ScenarioModeler
    |> DecisionAdvisor
```

Useful when the answer is not binary and the goal is to surface and compare options.

### 8. Meta-advisory (future)

A meta expert that helps users design missions given a problem statement:

```fsharp
expert MetaAdvisor =
    ProblemFramer
    |> ThinkingModelSelector
    |> MissionDesigner
    |> MissionReviewer
```

**Example input:**

```text
User: I need to migrate 300 Terraform modules to a new platform.
```

**Suggested mission output:**

```fsharp
mission TerraformMigration =
    DiscoveryAnalyst
    |> DependencyMapper
    |> MigrationArchitect
    |> RiskReviewer
    |> PrincipalReviewer
```

---

## MVP scope

The MVP focuses on the language and a minimal runtime — not a production-grade agent framework.

### In scope

- Hand-written parser for `.fml` files
- Sequential pipeline execution
- Markdown-backed expert definitions
- One LLM client abstraction
- CLI runner (`fml run`)
- Saved run outputs per step

### CLI example

```bash
fml run examples/build-operator/mission.fml --input examples/build-operator/input.md
```

### Output structure

```text
runs/
  build-operator/
    01-KubernetesArchitect.md
    02-SecurityArchitect.md
    03-PrincipalReviewer.md
    final.md
```

---

## Non-goals

The following are explicitly out of scope for the initial language and runtime:

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
  src/
    ForgeMission.Core/        # Parser, AST, pipeline runner, LLM client abstraction
    ForgeMission.Cli/         # CLI entrypoint (fml run ...)
  examples/
    build-operator/
      mission.fml
      input.md
      experts/
        KubernetesArchitect.md
        SecurityArchitect.md
        PrincipalReviewer.md
  runs/                       # gitignored — output of fml run
```

---

## First implementation plan

### Phase 1 — Language and parser

- Define the `.fml` grammar (mission, expert, `|>`)
- Write a hand-written recursive-descent parser in C#
- Produce an AST: `MissionDeclaration`, `ExpertDeclaration`, `Pipeline`
- Write unit tests for the parser

### Phase 2 — Expert loader

- Load expert markdown files by name from an `experts/` directory
- Parse YAML frontmatter (`name`, `input`, `output`)
- Validate that all experts referenced in a mission exist

### Phase 3 — Pipeline runner

- Execute the pipeline sequentially
- Pass the previous expert's output as context to the next
- Write each step's output to `runs/<mission-name>/NN-<ExpertName>.md`

### Phase 4 — LLM client

- Abstract the LLM call behind a single interface (`ILlmClient`)
- Implement one concrete client (Anthropic Claude or Azure OpenAI)
- Inject the system prompt from the expert definition and the user context from prior output

### Phase 5 — CLI

- `fml run <mission.fml> --input <input.md>` — run a mission
- `fml validate <mission.fml>` — check that all experts exist and the pipeline is valid
- `fml list experts` — list available experts in the current directory

### Phase 6 — Example and validation

- Build the `build-operator` example end-to-end
- Run it and evaluate whether expert composition produces meaningfully better output than a single general-purpose prompt
- Document findings

---

## Testable hypothesis

> Expert composition improves reasoning quality, consistency, and outcomes compared to a single general-purpose prompt.

The first prototype exists to test this hypothesis. The `build-operator` example is the initial test case.

---

## Status

Early prototype. Language design and parser are not yet implemented.
