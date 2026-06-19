# SDLC Meta-Mission — Planned Reference Example

## What this is

A planned showcase mission demonstrating two capabilities that are not yet implemented:
- **Mission composition as steps** — sub-missions used as pipeline steps in a top-level mission
- **`debate {}` block** — multi-agent round-robin debate with a synthesiser

Once both capabilities land, this becomes the canonical end-to-end example of MCL's full thesis: complex SDLC reasoning expressed in ~30 lines, each mode independently testable.

## The target mission

```fsharp
// Each mode is a self-contained, independently testable mission
mission DesignMode(input) loop(2) = {
    Architect
    -> CriticalReviewer
    -> Synthesiser
    -> QualityJudge
}

mission TaskMode(input) = {
    Developer
    -> Tester
    -> Releaser
}

mission ResearchMode(input) = {
    debate(rounds: 3) {
        Researcher
        FactChecker
        DomainExpert
    }
    -> Synthesiser
}

// Top-level mission: Classifier + routing table only
mission SDLCAgent(input) = {
    Classifier
    -> DesignMode(input: input)   when(mode: "design")
    -> TaskMode(input: input)     when(mode: "task")
    -> ResearchMode(input: input) when(mode: "research")
    -> Planner                    when(else)
}
```

## What each piece demonstrates

| Feature | Where used | Status |
|---------|-----------|--------|
| `Classifier` + `when(output: "x")` routing | `SDLCAgent` | Working — see `missions/when-routing/` |
| `when(else)` fallback | `SDLCAgent -> Planner` | Working |
| `loop(N)` with judge | `DesignMode loop(2)` | Working — see `missions/loop-demo/` |
| `->` sequential composition | All sub-missions | Working |
| Mission-as-step with param binding | `-> DesignMode(input: input)` | Not implemented — needs Mission Composition phase |
| `debate {}` block | `ResearchMode` | Not implemented — needs Multi-Agent Debate phase |

## What needs to be built

### 1. Mission composition (blocking)

`PipelineRunner` currently calls `IExpertRunner.RunAsync(expertName, ...)` for every step. It has no path to execute a sub-mission's pipeline. Required work:

- Resolution: when a step name matches a declared `MissionDeclaration` in the AST, treat it as a sub-mission call, not an expert call
- Execution: recursively invoke `PipelineRunner` with the sub-mission and its bound context
- Parameter binding: `(input: input)` maps the caller's context bag values into the sub-mission's declared params
- Error propagation: a failing sub-mission fails the parent step immediately

`ExpertLoader.Validate` already accepts mission names as valid step targets (landed in Phase 25 Spoke 1). The runtime gap is in `PipelineRunner.ExecuteStepAsync`.

### 2. `debate {}` block (blocking for ResearchMode)

Round orchestration: each expert sees the accumulated debate transcript. After `rounds` iterations, output is passed to the next step in the pipeline. See `plan.md` → "Multi-Agent Debate" and `docs/design/research.md` for the research-backed defaults (rounds: 3, warn beyond 5).

## Why this example matters

The `SDLCAgent` mission is the clearest expression of MCL's core claim — that reasoning structure is a first-class artifact worth expressing in its own language:

- **Readability**: the routing table is obvious at a glance. No framework code, no configuration YAML, no orchestration logic.
- **Testability**: `DesignMode`, `TaskMode`, and `ResearchMode` are each runnable independently. You can validate the design path without invoking the full agent.
- **Composability**: adding a new mode (`CodeReviewMode`) is one line in `SDLCAgent` plus a new mission file. No existing code changes.
- **Separation**: the routing logic lives in `SDLCAgent`. The reasoning logic lives in each sub-mission. These are different concerns and they stay separate.

## Relationship to existing work

- `missions/when-routing/` is the current working approximation — same routing structure, routes to experts instead of sub-missions
- `missions/parallel-synthesis/` demonstrates the fan-out/synthesiser pattern used inside sub-missions
- `missions/loop-demo/` demonstrates the `loop(2)` pattern used in `DesignMode`
- `docs/design/interaction-modes.md` covers the Classifier-router pattern and SDLC use case in detail

## When to build this

1. Mission Composition phase lands → `SDLCAgent` + `DesignMode` + `TaskMode` become runnable (minus `debate`)
2. Multi-Agent Debate phase lands → `ResearchMode` becomes runnable
3. At that point, add `missions/sdlc-agent/` as a full working showcase mission
