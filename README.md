# Mission Control Language (MCL)

A minimal scripting language for staging LLM experts into reliable reasoning pipelines.

> **Why it exists:** see [docs/why.md](docs/why.md)

---

## Quick start

```bash
export OPENAI_API_KEY=sk-...

mcl init       # resolve experts, generate mcl.lock
mcl run        # run the mission, output to stdout
```

---

## Writing a mission

A mission file is self-contained. It declares where experts come from, binds any input values,
and describes the pipeline.

```fsharp
use "./experts"

let goal    = "Design a production-grade K8s build operator"
let persona = "Principal SRE, Tekton specialist"

mission BuildOperatorDesign(goal, persona) =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer with { style = "terse ADR" }

output(BuildOperatorDesign)
```

Experts execute left to right. Each stage receives the accumulated context and all prior
outputs, then applies its own reasoning before passing forward.

```
KubernetesArchitect
        ↓
SecurityArchitect
        ↓
PrincipalReviewer
```

The mental model is a pipeline of reviewers, not a call stack.

---

## CLI

```bash
# Resolve expert sources and write mcl.lock
mcl init

# Check all experts resolve, pipeline is well-formed, lock file is current
mcl validate

# Run the mission — output to stdout
mcl run

# Stream each expert's progress to stderr as the pipeline runs
mcl run --steps

# Override any let binding at run time
mcl run --var goal="Redesign for ARM64"

# Scaffold a new expert directory
mcl expert init SecurityArchitect

# List experts in the current directory
mcl list experts
```

`mcl run` requires an `mcl.lock` — init is not optional. This mirrors Terraform's
`plan` / `apply` discipline: resolve first, then run.

---

## Output routing

By default the mission result goes to stdout — pipeable like any CLI tool:

```bash
mcl run                          # stdout
mcl run > report.md              # redirect
mcl run | pbcopy                 # pipe
```

Declare the destination in the mission file to make it explicit:

```fsharp
output(BuildOperatorDesign)                  # stdout (default)
output(BuildOperatorDesign, "./report.md")   # write to file
```

Status messages (`Running mission...`, step progress) always go to stderr and never
pollute the output stream.

---

## Experts

Each expert is a markdown file with a YAML frontmatter header and a system prompt.
`{{key}}` placeholders are interpolated from the context bag before each step runs.

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

Experts live in a directory declared by `use`:

```
missions/build-operator/
  mission.mcl
  mcl.lock
  experts/
    KubernetesArchitect/
      expert.md
    SecurityArchitect/
      expert.md
    PrincipalReviewer/
      expert.md
```

Experts can be built from nested pipelines:

```fsharp
expert KubernetesArchitect =
    RequirementsAnalyst
    |> PlatformArchitect
    |> ReliabilityArchitect
```

---

## Variables

`let` bindings seed the context bag. Every expert in the pipeline can read any binding
via `{{key}}` in its system prompt.

```fsharp
let goal   = "Design a build operator"
let apiKey = env("OPENAI_API_KEY")              // from environment
let model  = env("MCL_MODEL", "gpt-4o-mini")   // with default
```

`with` overrides context for a single step only:

```fsharp
mission BuildOperatorDesign(goal, persona) =
    KubernetesArchitect
    |> PrincipalReviewer with { style = "terse ADR", audience = "C-suite" }
```

Variable resolution order (lowest → highest precedence):

1. `let` binding
2. `with { }` clause on a step
3. `--var key=value` CLI flag

---

## Pass / fail

Every step in a pipeline passes by default — like a bash command exiting with code 0.
A step explicitly signals failure by returning a structured fail status in its output.

If any step fails, the pipeline stops immediately and the mission fails with that step's
reason. There is no silent continuation past a failure.

Expert authors declare failure conditions in plain prose in their system prompt:

```markdown
# PitchJudge/expert.md

You are the final judge. If the pitch is unclear, too long, or contains jargon —
declare failure and state which criterion was missed.
```

The runtime handles the structured contract. Expert authors write prose, not JSON.

---

## Looping until quality passes

A mission can declare a loop cap — the maximum number of times the full pipeline will
retry until all steps pass:

```fsharp
mission RefinedPitch(product) =
    PitchDrafter
    |> PitchCritic
    |> PitchReviser
    |> PitchJudge
    loop 3
```

The runtime retries the full pipeline on failure, up to the declared limit. Two reserved
variables are available to every expert in every attempt:

| Variable | Value |
|----------|-------|
| `{{attempt}}` | Current attempt number, 1-based. Always `1` for missions without `loop`. |
| `{{max_loops}}` | Declared loop cap. Always `1` for missions without `loop`. |

Experts can use these to adjust behaviour across retries:

```markdown
This is attempt {{attempt}} of {{max_loops}}.
If this is the final attempt, be especially strict — there are no more chances to improve.
```

### Seeing it in action

The `missions/loop-demo` pair shows the difference concretely. Both missions ask the same
question — *why is the sky blue?* — but one runs once and one loops until a judge approves.

```bash
make demo-naive      # one shot — whatever comes out, no retry
make demo-reliable   # loops up to 3 times until QualityJudge passes
```

**Naive — one shot, no judge:**

> The sky appears blue due to some kind of interaction between sunlight and the atmosphere.
> You see, the sunlight comes in and interacts with the air, scattering light in some fashion.
> It's all about how different wavelengths of light get influenced by particles in the air.
> The shorter wavelengths seem to scatter more, and that leads our eyes to perceive it as blue.
> It's not something we can see directly, but trust me, it's there. Don't think too hard
> about it, it's all pretty straightforward!

**Reliable — loops until QualityJudge approves:**

> The sky appears blue primarily due to a phenomenon known as Rayleigh scattering. This occurs
> when sunlight interacts with molecules and small particles in the Earth's atmosphere. Each
> color has a different wavelength, with blue light having a shorter wavelength compared to red
> light. When sunlight enters the atmosphere, it collides with gas molecules, which scatter
> shorter wavelengths much more effectively than longer ones. A concrete example: during sunrise
> and sunset, sunlight passes through a thicker layer of atmosphere, scattering the short
> wavelengths out of the direct line of sight and allowing the longer red and orange wavelengths
> to prevail — which is why those times of day look so different from noon.

The naive output is plausible-sounding but vague — it never names the mechanism. The reliable
output names Rayleigh scattering, explains why blue wavelengths scatter more, and gives a
concrete example. Run `make demo-reliable` to watch the retry happen in real time on stderr.

---

## Reserved context variables

These are injected by the runtime and available to every expert. They cannot be overridden.

| Variable | Value |
|----------|-------|
| `{{output}}` | The previous step's text output. Empty string on the first step. |
| `{{attempt}}` | Current loop iteration, 1-based. |
| `{{max_loops}}` | Declared loop cap. |

---

## Repository structure

```
mission-control-language/
  src/
    ForgeMission.Core/    # parser (ANTLR4), AST, pipeline runner, expert resolution
    ForgeMission.Cli/     # CLI — mcl init / run / validate / list / expert
    ForgeMission.Tests/   # unit and integration tests
  missions/
    build-operator/       # production K8s operator design example
    elevator-pitch/       # single-expert pitch example
    elevator-pitch-refined/  # Generator → Critic → Reviser → Judge example
  docs/
    plan.md               # implementation plan — all phases
    why.md                # origin, methodology, thesis
    design/               # language design, architecture docs
    phases/               # per-phase specs
```

---

## Status

| Phase | Description | Status |
|-------|-------------|--------|
| 1–10 | Core runtime, parser, CLI, expert resolution, variables | Done |
| 12 | Structured step envelope — pass/fail per step | Done |
| 14 | `loop N` — retry pipeline until all steps pass | Done |
| 15 | Token streaming — live output as experts generate | Done |
| 16 | MCL rename — binary and extension surface rename | Done |

See [`docs/plan.md`](docs/plan.md) for the full plan.
