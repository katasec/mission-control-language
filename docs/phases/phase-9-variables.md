# Phase 9 — Variables and Functional Parameters

## Goal

Extend the FML grammar and runtime to support `let` bindings, mission parameters, and per-step
`with` clauses. The context carrier between experts changes from a plain `string` to a
`Dictionary<string, object>` — the same "bag" pattern as OWIN's `AppFunc` and Go's
`context.Context`.

## Completion condition

The extended `build-operator` example below parses, validates, and runs end-to-end:

```fsharp
let goal    = "Design a production-grade K8s build operator"
let persona = "Principal SRE, Tekton specialist"

mission BuildOperatorDesign(goal, persona) =
    KubernetesArchitect
    |> SecurityArchitect
    |> PrincipalReviewer with { style = "terse ADR" }
```

Expert system prompts use `{{goal}}`, `{{persona}}`, `{{style}}` placeholders that are interpolated
at runtime from the context bag before each step runs.

## Environment variable support

API keys and provider config must never appear in `.fml` files or be passed as `--var` flags
(they would appear in shell history). Instead, `let` bindings support an `env()` builtin that
reads from the process environment at runtime:

```fsharp
let apiKey  = env("OPENAI_API_KEY")
let model   = env("FML_MODEL", "gpt-4o-mini")   // second arg = default value
```

The runtime resolves `env()` calls when seeding the context bag — before any expert runs.
If a required env var is missing and no default is supplied, the run fails with a clear error.
This keeps secrets out of source files and out of the context bag's string representation.

Variable resolution order (lowest → highest precedence):

1. `let` binding (includes `env()` calls)
2. Mission parameter binding (parsed names, available via context)
3. `with { }` clause on a step (merges into context before that step runs)
4. `--var key=value` CLI flag (overrides everything at seeding time)

## Grammar changes

```antlr
program    : (letBinding | declaration)* EOF ;
letBinding : 'let' LOWER_ID '=' value ;
mission    : 'mission' UPPER_ID params? '=' pipeline ;
expert     : 'expert' UPPER_ID params? '=' pipeline ;
params     : '(' LOWER_ID (',' LOWER_ID)* ')' ;
pipeline   : step ('|>' step)* ;
step       : UPPER_ID withClause? ;
withClause : 'with' '{' binding (',' binding)* '}' ;
binding    : LOWER_ID '=' value ;
value      : STRING | LOWER_ID | envCall ;
envCall    : 'env' '(' STRING (',' STRING)? ')' ;
STRING     : '"' (~["\r\n])* '"' ;
```

See [`src/ForgeMission.Core/Parser/FmlGrammar.g4`](../../src/ForgeMission.Core/Parser/FmlGrammar.g4)
for the authoritative grammar.

## AST changes

- `Program` gains `IReadOnlyList<LetBinding> Bindings`
- `LetBinding(string Name, LetValue Value)` — value is `StringLetValue` or `EnvLetValue`
- `MissionDeclaration` gains `IReadOnlyList<string> Params`
- `ExpertDeclaration` gains `IReadOnlyList<string> Params`
- `Pipeline.Steps` changes from `IReadOnlyList<string>` to `IReadOnlyList<Step>`
- `Step(string ExpertName, IReadOnlyList<Binding> With)` — `With` is empty when no clause
- `Binding(string Key, BindingValue Value)` — value is `StringBindingValue`, `VarRefBindingValue`, or `EnvBindingValue`

## Runtime changes

- Context carrier: `string` → `Dictionary<string, object>` (keyed by `StringComparer.Ordinal`)
- `"output"` key carries the chained result between steps
- `ContextInterpolator.Interpolate(template, context)` — `{{key}}` substitution
- `PipelineRunOptions` gains `IReadOnlyDictionary<string, string>? Vars` for CLI overrides
- `PipelineRunner` seeds context from let bindings, resolves `env()`, applies `--var` overrides,
  merges `with` clause per step, interpolates system prompt before each expert call
- `IExpertRunner.RunAsync` signature changed: second param is `Dictionary<string, object>`
- `MafExpertRunner` extracts `context["output"]` as the user message; interpolates system prompt

## CLI changes

- `fml run` gains `--var key=value` (repeatable) to inject variables at call time

## Tasks

| # | Task | Status |
|---|------|--------|
| 1 | Extend `FmlGrammar.g4` with `let`, params, `with` clause, string literals, `env()` | Done |
| 2 | Regenerate ANTLR parser files | Done |
| 3 | Extend `FmlAstBuilder` — new AST nodes for `LetBinding`, `Step`, `Binding` | Done |
| 4 | Update `Ast.cs` — `Program`, `MissionDeclaration`, `Pipeline`, new sum types | Done |
| 5 | Update `ExpertLoader.Validate` — exclude mission params from expert validation | Done |
| 6 | Change runtime context carrier from `string` to `Dictionary<string, object>` | Done |
| 7 | Add `ContextInterpolator` — `{{key}}` substitution | Done |
| 8 | Update `PipelineRunner` — seed context, resolve env(), merge with-bindings, interpolate | Done |
| 9 | Update `IExpertRunner.RunAsync` signature | Done |
| 10 | Update `MafExpertRunner` — extract `output` key, interpolate system prompt | Done |
| 11 | Implement `env()` builtin — fails clearly on missing var with no default | Done |
| 12 | Update CLI `fml run` — add `--var key=value` flag | Done |
| 13 | Update `missions/build-operator/mission.fml` — `let`, params, `with` clause | Done |
| 14 | Update expert markdown files — `{{goal}}`, `{{persona}}`, `{{style}}` placeholders | Done |
| 15 | Parser tests — let, env(), params, with clause | Done |
| 16 | Runtime tests — context seeding, with override, --var override, missing env var | Done |
| 17 | `StubExpertRunner` updated for context bag signature | Done |

## Result

33 tests pass (2 integration tests skip without OPENAI_API_KEY). `fml validate` accepts the
extended `build-operator` example. All existing tests preserved.

## Notes

- `IExpertRunner` signature change is breaking — `StubExpertRunner` updated in the same commit
- `{{key}}` with no matching context entry is left intact (warn-not-throw) — expert may
  intentionally leave a placeholder for a prior step to fill
- When a composite expert is flattened, the parent step's `with` clause is not propagated to
  sub-steps — each step applies only its own `with` clause
- `--var` overrides happen at context-seeding time, so they override `let` bindings but are
  themselves overridden by per-step `with` clauses (higher precedence in the resolution order)
