# Phase 25 Pre-flight — Open Design Decisions

## Status: Todo

## Purpose

Nine open design questions must be resolved before Phase 25 implementation begins.
Each question is listed below with context. Work through them one by one, record
the decision, and mark each as Resolved before starting Phase 25 Spoke 1.

No code changes in this phase — decisions and documentation only.

**Background reading:** [`docs/design/research.md`](../design/research.md) maps
academic literature (Self-Refine, Reflexion, Multi-Agent Debate, Constitutional AI,
MoE routing) to each decision below. Read it before the discussion session.

---

## 1. Error message design

**Status: Open**

**Context:**
Error message quality is a first-class language concern. Every failure mode should
tell the user exactly what to do next, not just what went wrong. This needs a
deliberate design pass before implementation, not a retrofit after.

**Questions to answer:**
- What are all the failure modes across parse, resolve, and execute phases?
- What is the format/structure of an MCL error message?
- Should errors include a link to docs or an error code for lookup?

**Decision:**
_To be recorded._

---

## 2. File versioning / backwards compatibility

**Status: Open**

**Context:**
Grammar changes in future phases will break mission files written today unless
there is a version declaration. Without versioning, drift between `forge` and
`.mcl` files is invisible and produces confusing errors.

Candidate syntax:
```fsharp
mcl 1.0

mission BuildOperatorDesign(goal) =
    ...
```

**Questions to answer:**
- Should `.mcl` files declare a version?
- What is the version scheme (semver, integer, date)?
- What does `forge` do when it encounters an unsupported version — hard error or warning?
- Does `forge.toml` also need a version declaration?

**Decision:**
_To be recorded._

---

## 3. Parallel failure model

**Status: Open**

**Context:**
Sequential fail-fast is clear — any step failure stops the pipeline. Parallel
introduces a new failure mode: if one expert in a `parallel {}` block fails and
others succeed, the outcome is ambiguous. This must be decided before the grammar
and runtime for parallel execution are written.

**Options:**
- **Fail the whole block** — consistent with fail-fast; passing results are discarded
- **Best-effort** — Synthesiser receives what succeeded; failed experts produce no output
- **Configurable** — `parallel (fail-fast) { }` vs `parallel (best-effort) { }`

**Questions to answer:**
- Which model is the default?
- Is configurability needed now or deferred?
- How does a downstream step know which parallel experts failed?

**Decision:**
_To be recorded._

---

## 4. Context accumulation

**Status: Open**

**Context:**
Each expert receives all prior output via `{{output}}`. In a long pipeline this
context grows unboundedly and can exceed a model's context window — a silent
failure mode unique to this domain that no traditional language has to handle.

**Questions to answer:**
- Is this a language concern or a runtime concern?
- Should the language offer a construct to truncate/summarise context at a step?
- If deferred, should it be formally documented as a known gap in `language.md`?

**Decision:**
_To be recorded._

---

## 5. `with { provider }` ambiguity

**Status: Open**

**Context:**
`provider` is both a reserved profile key (infrastructure) and a valid camelCase
identifier (domain variable). The current grammar cannot distinguish:

```fsharp
// Is "architect" a profile name or a domain variable value?
-> SecurityArchitect with { provider = "architect" }
```

One candidate fix — separate keyword for profile selection:
```fsharp
-> SecurityArchitect using "architect" with { style = "terse" }
```

`using` selects the provider profile. `with {}` remains purely for domain context.

**Questions to answer:**
- Is `using` the right keyword, or is there a better one?
- Should profile selection be in `with {}` with a reserved key, or a separate construct?
- Does this change affect the grammar in Spoke 1?

**Decision:**
_To be recorded._

---

## 6. Mission metadata

**Status: Open**

**Context:**
Expert markdown has structured frontmatter declaring `input` and `output`.
Missions have no equivalent — only parameter names in the declaration. This
asymmetry becomes a gap when composing missions or when the LSP needs to offer
completion for mission parameters.

**Questions to answer:**
- Should missions have frontmatter or structured metadata?
- If yes, where does it live — in `mission.mcl` or a separate file?
- Is this urgent for Phase 25 or deferred to a later phase?

**Decision:**
_To be recorded._

---

---

## 7. Anders Hejlsberg design review

**Status: For discussion**

**Context:**

Applying the lens of Anders Hejlsberg (Turbo Pascal, Delphi, C#, TypeScript) — one of the most pragmatic language designers alive — as a sanity check on MCL's current design.

### What holds up

- **Minimalism** — three primitives, nothing added without justification. He'd recognise the discipline and say most designers fail to maintain it within six months of the first external user.
- **Grammar-first** — ANTLR as the authoritative spec, everything derived from it.
- **Parse → Resolve → Execute** — textbook compiler boundary, correctly applied.
- **One mission per file** — he'd approve that this emerged from usage, not upfront design. That's how TypeScript evolved.

### What he'd push back on

**ANTLR as the permanent parser.**
TypeScript's parser is hand-written precisely because ANTLR gives limited control over error recovery and error messages. His take: *"ANTLR is fine for proving the grammar. But when error messages matter — and you said they do — you'll want to hand-write it. Plan for that transition."*

**The stringly-typed context bag.**
`Dictionary<string, object>` with `{{key}}` placeholders is where type safety goes to die. He invented C# generics and TypeScript's structural type system to kill this pattern. His take: *"This is fine today. But retrofitting a type system onto a stringly-typed runtime is the hardest thing you can do to yourself. Think about it now even if you don't implement it."*

**`loop N` belongs in the language.**
His question: *"Is looping a reasoning concern or an execution concern?"* A declarative language should express *what*, not *how*. He might argue the runtime should infer retry behaviour from the pass/fail signal, and the language shouldn't need to know about it.

**`parallel {}` is explicit where it could be implicit.**
If two steps don't share data dependencies, the runtime could infer parallelism. Making the user declare `parallel {}` asks them to think about execution, not reasoning. His question: *"Does the author of a thinking model need to know or care that these steps run in parallel?"*

### His one big question

> *"Why a language? Could this be a strongly-typed library with a fluent API instead?"*

The honest answer: a language enforces constraints a library can't — one mission per file, PascalCase experts, no lambdas or control flow. A library lets users do anything. But he'd make you articulate that defence clearly, because if you can't, you don't yet fully know why you're building a language.

### His verdict

The instincts are right and the discipline is admirable. Two things to lose sleep over:

1. **The stringly-typed context bag** — design at least a mental model for types now, even if you implement later
2. **ANTLR as the permanent parser** — great for now, plan the transition when error messages become a priority

His closing test: *"The test of a language isn't whether you can add things to it. It's whether you can remove things from it and still express everything you need to."*

By that test, MCL is in reasonable shape.

**Questions to answer:**
- Does `loop N` belong in the language or the runtime? If the runtime, how does an author express "retry until quality passes"?
- Does `parallel {}` belong in the language, or should the runtime infer it from data independence?
- What is the minimal type model for the context bag that doesn't foreclose future type safety?
- When does the ANTLR → hand-written parser transition happen, and what is the trigger?

**Decision:**
_To be recorded._

---

---

## 8. Conditional steps — `when { }` primitive

**Status: Open**

**Context:**
The classifier-router pattern (see [`docs/design/interaction-modes.md`](../design/interaction-modes.md)) requires steps that execute conditionally based on a context bag value set by a prior step:

```fsharp
-> Architect when { mode = "design" }
-> Developer when { mode = "task" }
```

This is a new language primitive not currently in the grammar. It is the minimal conditional needed to express routing — not general branching, just step-level guards on context values.

**Questions to answer:**
- Should `when { }` be added in Phase 25 Spoke 1 or as a separate phase?
- Does it evaluate against exact string match only, or support richer expressions?
- Who sets the routed value — the Classifier via structured `StepEnvelope` output, or a plain context bag key?
- Should an unmatched `when { }` step silently skip or emit a trace log entry?

**Decision:**
_To be recorded._

---

---

## 9. Loop context — deterministic convergence vs random retry

**Status: Open**

**Context:**
The current `loop N` implementation almost certainly resets context at the start of each iteration — each attempt runs with the same original input, with no memory of what failed or why. This makes looping random retry, not iterative improvement:

```
Attempt 1: E1(input) → E2 → E3 → Judge(fail: "too vague")
Attempt 2: E1(input) → E2 → E3 → Judge(fail: "still too vague")  ← same input
Attempt 3: E1(input) → E2 → E3 → Judge(pass?)                    ← luck
```

What is needed is accumulated history across iterations. The Judge's failure reason must be fed back into E1 on the next attempt so every expert in the chain knows what went wrong and can improve deliberately:

```
Attempt 1: E1(input) → E2 → E3 → Judge(fail: "too vague")
                                          ↓
Attempt 2: E1(input + "failed: too vague") → E2 → E3 → Judge
                                          ↓
Attempt 3: E1(input + attempt 1 + attempt 2 feedback) → E2 → E3 → Judge(pass)
```

This is the difference between a pipeline that retries and a pipeline that learns. Deterministic convergence requires feedback to flow backward across iteration boundaries.

**Proposed solution:**
Introduce `{{feedback}}` as a new reserved variable — the Judge's structured failure reason from the previous iteration, available to every expert at the start of the next attempt. Empty string on attempt 1.

| Variable | Value |
|----------|-------|
| `{{feedback}}` | Judge's failure reason from the prior iteration. Empty on attempt 1. |

Expert prompts can then explicitly act on it:

```markdown
Previous attempt failed because: {{feedback}}
Avoid repeating that mistake.
```

The Judge's failure reason already exists in `StepEnvelope` — the runtime change is surfacing it as `{{feedback}}` at the top of the next iteration rather than discarding it.

**Questions to answer:**
- Is `{{feedback}}` the right name, or is `{{prior_failure}}` / `{{judge_feedback}}` clearer?
- Should `{{feedback}}` contain only the Judge's failure reason, or the full output of the last failed iteration?
- Should accumulated history across all attempts be available, or only the most recent failure?
- Does this require a `StepEnvelope` schema change or is the failure reason already structured enough to surface directly?
- Is this a Phase 25 runtime change or a separate phase?

**Decision:**
_To be recorded._

---

## Completion gate

All decisions must be recorded above before Phase 25 Spoke 1 begins.
