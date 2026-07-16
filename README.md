# Mission Control Language (MCL)

**MCL is a language for codifying how to think, not just what to ask.**

---

## The problem

Anyone who works seriously with AI develops reasoning patterns.

Not just prompts — patterns. Sequences of lenses applied to a problem in a deliberate order: generate, then critique, then revise, then judge. Or: classify, then route, then execute. Or: architect, then harden, then sign off.

These patterns get encoded somewhere. Instruction files. Skill definitions. Internal playbooks. Prompt libraries. They all attempt to answer the same question:

> *How should this problem be thought through?*

But they share the same weaknesses:

- buried in prose, not structure — intent is invisible, only output is visible
- not composable — patterns cannot be named, reused, or combined
- not reviewable — a collaborator cannot inspect the reasoning approach, only the result
- not transferable — tied to a session, a tool, or a person
- rebuilt repeatedly — accumulated lessons don't survive the context window

The industry optimises prompts. The real asset is the reasoning pattern behind the prompt.

---

## The insight

Experienced practitioners don't solve hard problems with a single prompt. They apply repeatable reasoning techniques:

```
Generate → Critique → Revise → Judge
Architect → Security Review → Principal Review
Debate → Synthesis
Classify → Route → Execute
Generate → Verify → Refine
```

These are not prompts. These are thinking models.

MCL turns thinking models into reusable software artifacts.

---

## A first example

```fsharp
mission RefinedPitch(product) loop(3) = {
    PitchDrafter
    -> PitchCritic
    -> PitchReviser
    -> PitchJudge
}
```

This is Self-Refine — a known reasoning architecture — expressed declaratively. The pipeline drafts, critiques, revises, and judges, retrying up to three times until the judge passes. The mission is the asset, not the prompt.

Compare [`missions/elevator-pitch/`](missions/elevator-pitch/) (one expert, one pass) with [`missions/elevator-pitch-refined/`](missions/elevator-pitch-refined/) (four experts in sequence). Same product, different thinking model — the difference is the mission.

---

## The three concepts

### Expert — *who performs the work*

An Expert is a reusable intelligence package: a markdown file with a YAML frontmatter header and a system prompt. Experts encapsulate knowledge, methodology, review criteria, and domain expertise. They are first-class artifacts, not inline prompt strings.

```markdown
---
name: KubernetesArchitect
input: Task description
output: Kubernetes architecture design
---

You are a senior Kubernetes architect.

Goal: {{goal}}

Produce a concrete architecture covering CRD design, controller structure, RBAC,
and operational concerns.
```

Local experts live at `./experts/<Name>/expert.md`. OCI-distributed experts are declared in `forge.toml` and pulled by `forge init`.

### Mission — *how should this problem be reasoned about*

A Mission is a codified thinking model. It makes explicit which experts engage, in what order, and how their outputs build on each other. It is the structure of thought, written down.

```fsharp
mission BuildOperatorDesign(goal, persona) = {
    KubernetesArchitect
    -> SecurityArchitect
    -> PrincipalReviewer(style: "terse ADR")
}
```

Each stage receives the accumulated context and all prior outputs, then applies its domain expertise before passing forward. Missions are composable — a mission can be used as a step in another mission.

### Agent — *how is the capability consumed*

An Agent is a runtime endpoint. It exposes one mission behind a conversational interface or HTTP API — one mission, one agent, one endpoint.

---

## Expert kinds — heterogeneous reasoning substrates

Different reasoning tasks require different substrates. Not all cognition should be delegated to a language model.

| Kind | Substrate | When to use |
|------|-----------|-------------|
| `llm` | Language model | Generation, reasoning, extraction |
| `rule` | Deterministic evaluator | Hard invariants — certainty, not probability |
| `onnx` | Trained ML model | Statistical scoring, classification |
| `json_extract` | Structured parser | Bridging LLM output into typed context keys |
| `http` | External service | Existing microservices, scoring APIs |

Rules enforce invariants. Language models perform reasoning. This separates certainty from probability.

A pipeline is not constrained to one substrate. A language model can extract signals, a rule gate can verify structure, a trained ML model can score — all in the same mission:

```
LLMAnalyst → ExtractFeatures → Scorer → LLMInterpreter
  (llm)       (json_extract)   (onnx)       (llm)
```

MCL is neurosymbolic orchestration expressed declaratively.

---

## Language primitives

| Primitive | What it expresses |
|-----------|------------------|
| `->` | Sequential composition — expertise flows from one step to the next |
| `parallel {}` | Concurrent execution — multiple experts run simultaneously |
| `when()` | Conditional guard — a step runs only if the context bag matches |
| `loop(N)` | Quality convergence — reruns the pipeline until quality passes, up to N times |
| `using` | Provider selection — which `forge.toml` profile a step uses |
| Mission as step | Composition — a mission used as a step in another mission's pipeline |

---

## Reasoning patterns as assets

MCL is not about individual prompts. It is about packaging accumulated experience.

The [`missions/concepts/`](missions/concepts/) folder contains proven reasoning architectures — not examples, but reusable thinking models ready to adopt and adapt:

| Pattern | What it encodes |
|---------|----------------|
| [Self Refine](missions/concepts/self-refine/) | Iterative quality convergence — draft, critique, revise, judge |
| [Debate](missions/concepts/debate/) | Adversarial reasoning across positions → synthesis |
| [Constitutional AI](missions/concepts/constitutional-ai/) | Principle-guided self-correction |
| [Hallucination Reduction](missions/concepts/hallucination-reduction/) | Cross-verification before commitment |
| [Verifiable Reasoning](missions/concepts/verifiable-reasoning/) | Deterministic checks alongside LLM reasoning |
| [Mixture of Agents](missions/concepts/mixture-of-agents/) | Parallel specialists → synthesiser |
| [Hybrid LLM + ML](missions/concepts/hybrid-llm-ml/) | Neural reasoning + classical ML scoring in one pipeline |

Each pattern is named, runnable, and versioned. Because experts are OCI artifacts, reasoning patterns can be published and pulled like packages:

```
ghcr.io/org/self-refine@1.0
ghcr.io/org/debate@1.0
ghcr.io/org/cis-audit@2.1
```

OCI becomes a package manager for reasoning techniques. `forge init` is dependency resolution for thinking models.

---

## Writing a mission

```fsharp
// Domain inputs
let goal    = "Design a production-grade K8s build operator"
let persona = "Principal SRE, Tekton specialist"

// Thinking model
mission BuildOperatorDesign(goal, persona) = {
    KubernetesArchitect
    -> SecurityArchitect
    -> PrincipalReviewer(style: "terse ADR")
}

output(BuildOperatorDesign)
```

See [`missions/build-operator/`](missions/build-operator/) for a working version of this mission.

---

## Writing an expert

```markdown
---
name: KubernetesArchitect
input: Task description
output: Kubernetes architecture design
---

You are a senior Kubernetes architect.

Goal: {{goal}}
Perspective: {{persona}}

Produce a concrete architecture covering CRD design, controller structure,
RBAC, and operational concerns.
```

`{{key}}` placeholders are interpolated from the context bag before the step runs.

---

## Quality-convergence loop

```fsharp
mission RefinedPitch(product) loop(3) = {
    PitchDrafter
    -> PitchCritic
    -> PitchReviser
    -> PitchJudge
}
```

The runtime reruns the full pipeline on failure, up to the declared limit. The last step's pass/fail status controls the loop. On each retry, the runtime prepends structured critique to the first expert's context — no developer action required.

| Variable | Value |
|----------|-------|
| `{{attempt}}` | Current attempt number, 1-based |
| `{{max_loops}}` | Declared loop cap |
| `{{feedback}}` | Failure message from the prior attempt's judge or rule gate |

See [`missions/loop-demo/`](missions/loop-demo/) and compare with [`missions/loop-demo-naive/`](missions/loop-demo-naive/) — same question, no retry — to see what the loop adds.

---

## Deterministic rule gates

Not every check needs a language model. `kind: rule` evaluates a declarative expression against the prior step's output — in-process, zero latency, zero tokens.

```markdown
---
name: WordCountGate
kind: rule
check: word_count >= 50 and markdown_has_heading
onFail: Response too short and missing a heading. Expand to at least 50 words with a ## heading.
---
```

On failure, `onFail` is written to `{{feedback}}` and injected into the next loop iteration automatically.

```fsharp
mission Draft(topic) loop(3) = {
    Drafter
    -> WordCountGate    // kind: rule — instant, no LLM call
}
```

**Available evaluators:**

| Evaluator | Example |
|-----------|---------|
| `word_count op N` | `word_count >= 50` |
| `char_count op N` | `char_count < 500` |
| `line_count op N` | `line_count >= 3` |
| `sentence_count op N` | `sentence_count >= 2` |
| `contains "text"` | `contains "## Summary"` |
| `starts_with "text"` | `starts_with "{"` |
| `ends_with "text"` | `ends_with "}"` |
| `no_match "pattern"` | `no_match "TODO\|FIXME"` |
| `contains_pattern "pattern"` | `contains_pattern "\d{4}-\d{2}-\d{2}"` |
| `json_parseable` | `json_parseable` |
| `xml_parseable` | `xml_parseable` |
| `markdown_has_heading` | `markdown_has_heading` |

Multiple clauses joined with `and` must all pass.

See [`missions/rule-gate-demo/`](missions/rule-gate-demo/) for a working example.

---

## Parallel execution

```fsharp
mission Analysis(input) = {
    DataExtractor
    -> parallel {
        Summariser
        FactChecker
        Critic
    }
    -> Synthesiser
}
```

All experts in a `parallel {}` block run concurrently. Each expert's output is available downstream as `{{ExpertName}}`. If any expert fails, the whole block fails immediately.

See [`missions/parallel-synthesis/`](missions/parallel-synthesis/) — three independent analysts evaluate a proposal in parallel, then a Synthesiser consolidates their views into a single go/no-go recommendation.

---

## Conditional routing

```fsharp
mission SDLCAgent(input) = {
    Classifier
    -> Architect when(mode: "design")
    -> Developer when(mode: "task")
    -> Researcher when(mode: "research")
    -> Planner   when(else)
}
```

`when(key: "value")` guards a step — it runs only if the context bag matches. `when(else)` is the default branch. `Classifier` is a stdlib expert — ships embedded in the binary, always available, no declaration needed.

See [`missions/when-routing/`](missions/when-routing/) for a working example.

---

## Mission composition

A mission is an expert at the interface level — it takes input and produces output. The caller never knows whether a step is a single LLM call or a full sub-pipeline.

```fsharp
mission DesignMode(input) loop(2) = {
    Architect
    -> CriticalReviewer
    -> Synthesiser
    -> QualityJudge
}

mission TaskMode(input) = {
    Developer
    -> Tester
}

mission SDLCAgent(input) = {
    Classifier
    -> DesignMode(input: input) when(mode: "design")
    -> TaskMode(input: input)   when(mode: "task")
    -> Planner                  when(else)
}
```

`DesignMode` and `TaskMode` are independently runnable missions. `SDLCAgent` composes them behind a routing layer — routing logic and reasoning logic stay in separate, independently testable units.

See [`missions/sdlc-agent/`](missions/sdlc-agent/) for a working example.

---

## Distribution

Reasoning artifacts should be reusable, reproducible, and versioned.

```
missions/build-operator/
  mission.mcl     ← thinking model — reasoning and domain variables only
  forge.toml      ← manifest — experts, provider profiles, OCI sources
  mcl.lock        ← resolved hashes (generated by forge init, never hand-edited)
```

`mission.mcl` is a pure reasoning artifact. It is readable without knowing anything about the infrastructure running it. Infrastructure — which model, which API key, which experts came from OCI — lives in `forge.toml`.

```toml
# Expert sources — OCI refs pulled by forge init
[experts]
KubernetesArchitect = "ghcr.io/katasec/forge-kubernetes-architect@0.1.0"

# Default provider
[providers.default]
provider = "openai"
model    = env("MCL_MODEL", "gpt-4o-mini")
apiKey   = env("MCL_API_KEY")

# Named profile — selected per step with `using`
[providers.architect]
provider = "anthropic"
model    = "claude-opus-4-8"
apiKey   = env("ANTHROPIC_API_KEY")
```

Supported providers: `openai`, `anthropic`, `azure`, `ollama`. Run `forge provider scaffold <name>` to generate a starter block.

---

## Execution

```bash
forge init                                    # resolve experts, write mcl.lock
forge validate                                # parse and validate the mission file
forge run                                     # run the mission
forge run --steps                             # stream each expert's output live
forge run --var goal="Redesign for ARM64"     # override a let binding at runtime
forge clean                                   # purge ~/.forge/experts cache
forge login ghcr.io --token <pat>            # save registry credentials
forge list experts                           # list local experts
forge provider list                          # list supported providers
forge provider scaffold <name>               # print a ready-to-paste forge.toml block
```

Output routing:

```bash
forge run                        # stdout
forge run > report.md            # redirect
forge run | pbcopy               # pipe
```

Or declare the destination in the mission file:

```fsharp
output(BuildOperatorDesign)                 // stdout (default)
output(BuildOperatorDesign, "./report.md")  // write to file
```

Status messages always go to stderr and never pollute the output stream.

---

## Interoperability — forge serve

A mission can be exposed as a service.

```bash
forge serve                   # reads agent.yaml in current directory
forge serve path/to/agent.yaml
```

`forge serve` exposes the pipeline as an endpoint conforming to the OpenAI wire protocol — the de facto integration standard for AI services. Any client or framework that already speaks this format can call a forge mission with no migration cost and no new SDK.

It can also speak the **Anthropic wire** (`POST /v1/messages`), so Anthropic-native clients — including the `claude` CLI via `ANTHROPIC_BASE_URL` — can point at a forge mission directly. Select the wire in `agent.yaml`:

```yaml
mission: mission.mcl
port: 8080
id: my-agent
wire: anthropic     # default: openai
```

On the Anthropic wire the mission receives the **full conversation**, not just the last user message: the context bag gains `conversation` (structured messages — interpolate `{{conversation}}` for a transcript), `system` (effective system instructions), and `goal` (the latest user intent — the last text block of the last user message).

### forge claude — Claude Code fronted by a mission, one command

```bash
forge claude                    # serve the mission in cwd + launch claude wired to it
forge claude ./mission.mcl      # explicit mission file (agent.yaml not required)
forge claude @grok              # pull a built-in mission from the catalog and front it
forge claude -p "one-shot"      # pass a prompt straight through to claude
forge claude --print-env        # just serve + print the export lines (wire tools by hand)
forge claude --container        # run the mission as the cloud container (parity mode)
```

`forge claude` picks an ephemeral port, serves the mission on the Anthropic wire in-process, launches the real `claude` CLI with `ANTHROPIC_BASE_URL` pointed at it, and tears everything down when claude exits. Claude Code **stays a real coding agent** — Read/Edit/Write/Bash round-trip through the mission, which enriches once per user turn and verifies the final answer.

Redirectable surfaces: **CLI** (this command), **VS Code** (`claudeCode.environmentVariables` — use `--print-env`), **JetBrains** (shell env). The Claude **desktop app** is OAuth-only and cannot be redirected this way.

```python
# Python — call a forge mission like any other OAI-compatible service
import requests

response = requests.post(
    "http://localhost:8080/v1/chat/completions",
    json={
        "model": "debate",
        "messages": [{"role": "user", "content": question}],
    },
)
```

The reasoning pipeline — ONNX models, rule gates, multi-expert composition — is invisible to the consumer. They call an endpoint.

See [`clients/python/`](clients/python/) for a working client against the Debate mission.

```bash
forge agent start --agent-file <path>     # start agent container (Docker)
forge agent stop  --agent-file <path>     # stop agent container
forge webui start --agent-file <path>     # start Open WebUI connected to agent
forge webui stop                          # stop Open WebUI
```

---

## Pass / fail

Every step passes by default. Only experts declared as judges can stop the pipeline. Add `role: judge` to the expert's frontmatter to opt in:

```markdown
---
name: QualityJudge
role: judge
---

You are the final judge. If the pitch is unclear, too long, or contains jargon —
declare failure and state which criterion was missed.
```

Critics and reviewers that find issues always pass their output downstream — they never stop the pipeline. Only a judge can halt execution.

---

## Reserved context variables

| Variable | Value |
|----------|-------|
| `{{output}}` | Previous step's text output. Empty string on first step. |
| `{{attempt}}` | Current loop iteration, 1-based. Always `1` without `loop`. |
| `{{max_loops}}` | Declared loop cap. Always `1` without `loop`. |
| `{{ExpertName}}` | Output from a named step in a `parallel {}` block. |
| `{{feedback}}` | Failure message from the prior loop attempt's judge or rule gate. Empty on attempt 1. |

---

## Examples

| Mission | What it demonstrates |
|---------|---------------------|
| [`elevator-pitch`](missions/elevator-pitch/) | Single expert — minimum viable mission |
| [`elevator-pitch-refined`](missions/elevator-pitch-refined/) | Sequential composition — draft → critique → revise → judge |
| [`build-operator`](missions/build-operator/) | Named params `(key: value)` — per-step context overrides |
| [`loop-demo`](missions/loop-demo/) | `loop(N)` — quality convergence with automatic retry |
| [`loop-demo-naive`](missions/loop-demo-naive/) | Same question, no retry — shows the contrast |
| [`when-routing`](missions/when-routing/) | `when()` — classifier routes to the right specialist |
| [`parallel-synthesis`](missions/parallel-synthesis/) | `parallel {}` — three independent analysts, one synthesiser |
| [`rule-gate-demo`](missions/rule-gate-demo/) | `kind: rule` — deterministic gate with loop feedback |
| [`sdlc-agent`](missions/sdlc-agent/) | Mission composition — sub-missions as steps |

---

## Quick start

```bash
export MCL_API_KEY=sk-...   # OpenAI key (default provider in all examples)

cd missions/elevator-pitch
forge init                  # resolve experts, write mcl.lock
forge validate              # parse and validate
forge run                   # single expert, single pass — simplest possible mission
```

For sequential composition with adversarial review:

```bash
cd missions/elevator-pitch-refined
forge init && forge run     # drafter → critic → reviser → judge
```

Or try the showcase missions:

```bash
cd missions/when-routing        # conditional routing
cd missions/parallel-synthesis  # parallel experts + synthesiser
cd missions/loop-demo           # quality-convergence loop with retry
cd missions/rule-gate-demo      # deterministic rule gate + loop feedback
cd missions/sdlc-agent          # mission composition — sub-missions as steps
forge init && forge run
```

Provider and model are configured in each mission's `forge.toml`. To switch to Anthropic, swap the `[providers.default]` block — the commented alternative is already in each example file.

---

## Why MCL exists

Six months of real-world usage across production debugging, infrastructure automation, Kubernetes operations, and software development across multiple stacks.

Every single time, getting reliable output from a language model required the same manual work: decompose the problem, identify the relevant reasoning lenses, sequence them deliberately, and structure the handoff between them. That process was always implicit — rebuilt from scratch each session, buried in ad-hoc prompts and trial and error.

The accumulated lesson: a single general-purpose prompt asks the model to architect, review, challenge, and conclude simultaneously. That is where reliability breaks down. Decomposed reasoning — each lens doing one thing well, in sequence — produces measurably better output.

MCL is the codification of that process. It makes the reasoning structure explicit, named, composable, and reviewable.

The value is not the prompt. The value is the lesson. MCL exists to make those lessons reusable.

---

> Full origin story and methodology: [docs/why.md](docs/why.md)
