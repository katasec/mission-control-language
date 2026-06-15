# Phase 14 — loop N

**Status:** Pending — depends on Phase 12 (StepEnvelope)

## Goal

Allow a mission to declare how many times it will retry the full pipeline until all steps pass.
`loop N` is a mission-level property — it has nothing to do with output routing.

## Design Principle

Modelled on bash exit codes. Every step passes by default (exit 0). A step explicitly signals
failure by returning `status: fail` in its envelope (non-zero exit). Mission passes when all
steps pass. `loop N` retries the full pipeline up to N times until that condition is met.

Failure conditions belong in the expert's own MD — not in the mission grammar. An expert author
writes in plain prose when their expert should declare failure. The runtime injects the JSON
contract; the expert decides the semantics.

```markdown
# PitchJudge/expert.md
You are the final judge.
If the pitch is unclear, too long, or contains jargon — declare failure.
```

No `passes when` declaration needed. Any step can fail. The mission loops until none do.

## Syntax

```
mission RefinedPitch(product) =
    PitchDrafter
    |> PitchCritic
    |> PitchReviser
    |> PitchJudge
    loop 3

mission BuildOperatorDesign(goal, persona) =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer
    // no loop — runs once, passes if all steps complete without failure
```

## Runtime Behaviour

```
attempt 1 → PitchJudge fails → retry
attempt 2 → PitchJudge fails → retry
attempt 3 → all steps pass  → done
```

```
attempt 1 → PitchJudge fails → retry
attempt 2 → PitchJudge fails → retry
attempt 3 → PitchJudge fails → mission fails, surface last result + failure reason
```

Each attempt is a full fresh pipeline run. Context is re-seeded from scratch each time.

## Reserved Loop Variables

The runtime automatically injects two reserved variables into the context bag at the start of
each attempt. Expert authors use them via `{{attempt}}` and `{{max_loops}}` in their system
prompts — no declaration required.

| Variable | Value | Always available? |
|----------|-------|-------------------|
| `{{attempt}}` | Current iteration, 1-based | Yes — is `1` for missions without `loop` |
| `{{max_loops}}` | Declared loop cap | Yes — is `1` for missions without `loop` |

These join `{{output}}` as the full set of runtime-reserved context variables. Everything else
in context comes from `let` bindings or `--var` overrides.

`{{attempt}}` is always `1` for non-looping missions, so experts that reference it are safe to
use in any mission — the same way `$?` in bash is always defined even if you never check it.

Example usage in an expert prompt:

```
You are reviewing attempt {{attempt}} of {{max_loops}}.
If this is the final attempt, be especially strict — there are no more chances to improve.
```

## Grammar Changes

```antlr
mission
    : MISSION UPPER_ID params? EQUALS pipeline loopClause?
    ;

loopClause
    : LOOP INT
    ;
```

New lexer tokens: `LOOP`, `INT`.

`loop` without a number is a grammar error. `loop 1` is valid but a no-op (equivalent to no loop).

## AST Changes

```csharp
public record MissionDeclaration(
    string Name,
    IReadOnlyList<string> Params,
    Pipeline Pipeline,
    int MaxLoops = 1)           // 1 = run once (default)
    : Declaration(Name);
```

## MissionResult Changes

```csharp
public record MissionResult(
    string MissionName,
    string Text,
    MissionStatus Status,
    string? FailReason = null,
    int Attempts = 1);
```

## CLI Status Output (stderr)

```
Running mission 'RefinedPitch'... (attempt 1/3)
Running mission 'RefinedPitch'... (attempt 2/3)
Running mission 'RefinedPitch'... (attempt 3/3)
```

## Changes Required

| File | Change |
|------|--------|
| `FmlGrammar.g4` | Add `loopClause`, `LOOP`, `INT` tokens |
| `Ast.cs` | Add `MaxLoops` to `MissionDeclaration` |
| `FmlAstBuilder.cs` | Visit `loopClause` |
| `Program.cs` | Retry loop in `run` command; inject `attempt` into vars each run |
| `MissionResult.cs` | Add `Attempts` field |
| Tests | Loop stops on first all-pass; exhausted loops surfaces last failure; `{{attempt}}` in context |
