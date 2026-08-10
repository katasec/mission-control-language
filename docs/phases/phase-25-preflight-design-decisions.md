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

**Status: Resolved**

**Context:**
Error message quality is a first-class language concern. Every failure mode should
tell the user exactly what to do next, not just what went wrong. This needs a
deliberate design pass before implementation, not a retrofit after.

**Questions to answer:**
- What are all the failure modes across parse, resolve, and execute phases?
- What is the format/structure of an MCL error message?
- Should errors include a link to docs or an error code for lookup?

**Decision:**

- **Error codes:** Phase-prefixed — `P` (parse), `R` (resolve), `X` (execute), `C` (config/CLI).
- **Doc links:** No URLs in the binary. Error codes are stable; URLs rot. `forge explain <code>` is the future lookup path (deferred — not Phase 25 scope).
- **Source positions:** Emit good messages now using phase + expert name as the anchor. Source positions (file, line, col) are a Phase 26 prerequisite — see note added to Phase 26 Spoke 1. Format is designed to accept them without shape change.
- **`help:` line:** Mandatory on every error. No error ships without a specific next action.
- **Format:** Rust-inspired. Structured `MclError` record (not string) carries: code, message, nullable `MclErrorLocation` (null until Phase 26), `Notes[]`, and `Help` (required). CLI renderer owns Spectre.Console — Core never calls Spectre. TTY detection and `NO_COLOR` support handled by Spectre automatically.
- **Color:** Spectre.Console (already in project from Phase 23). AOT-safe. Degrades to plain text when piped or `NO_COLOR=1`.

Example shape:
```
error[R002]: expert 'SecurityArchitect' not found
  = searched: ./experts/SecurityArchitect/expert.md
  = searched: ~/.forge/experts/SecurityArchitect/
  = help: run `forge init` to pull missing experts, or create ./experts/SecurityArchitect/expert.md
```
Phase 26 adds the line underline and caret — same `MclError` record, renderer upgraded.

---

## 2. File versioning / backwards compatibility

**Status: Resolved**

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

- **Version location:** `forge.toml`, not `.mcl` files. Source files are version-agnostic — this is the TypeScript/C# pattern. `tsconfig.json` carries language settings; individual `.ts` files do not. `.mcl` files follow the same principle.
- **Version scheme:** Semver, Go-style — `mcl = "1.0"` (major.minor, no patch component). One declaration in `forge.toml` covers both the grammar version and the `forge.toml` schema version — they are coupled and evolve together. The compiler carries the burden of knowing what each version means for every file format it touches. Minor bump (`1.0` → `1.1`) for additive grammar features; major bump (`1.0` → `2.0`) for breaking changes only. Patch component unused for grammar — patch versions are forge binary releases. This is the Type 2 (reversible) choice: integer forecloses semver later; semver does not foreclose simplification.
- **On mismatch:** Hard error, direction-aware, with specific next action per Decision 1 format:
  - File ahead of forge (`mcl = 2`, forge knows `mcl 1`): `help: upgrade forge — run brew upgrade forge`
  - File behind forge (`mcl = 1`, forge knows `mcl 2`): `help: run forge migrate to update your mission files to mcl 2`
- **`forge migrate`:** The resolution path for files behind forge. Auto-upgrades `.mcl` syntax between versions. Analogous to `go fix`. `forge init` writes the version; `forge migrate` updates it. Users never manually increment.
- **Backwards compatibility commitment (Anders Hejlsberg lens):** Additive grammar changes are always backwards compatible and never require a version bump. Version bumps only happen when syntax is removed or renamed. Breaking changes are defined narrowly — removal or rename only. The goal is for `forge migrate` to be a rarely-invoked emergency tool, not a routine upgrade path. Every use of it is an admission of a breaking change. **This commitment activates at `mcl = 1` public release.** Pre-1.0 grammar iteration (e.g. `|>` → `->`) is unconstrained — there is no external code to break.
- **`forge.toml` cognitive overhead:** Eliminated by the single-integer rule. One number, declared once, managed by tooling. Users never think about two separate version numbers.

```toml
# forge.toml
mcl = 1   # grammar version and manifest schema version — one number covers both
```

---

## 3. Parallel failure model

**Status: Resolved**

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

- **Model: A — fail the whole block.** If any expert in a `parallel {}` block fails, cancel all in-flight experts immediately via context propagation and stop the pipeline. Consistent with sequential fail-fast everywhere else. Either the parallel discussion happened or it didn't — there is no partial result worth synthesising.
- **Configurability: deferred indefinitely.** Best-effort and configurable modes are not added until a concrete use case appears that fail-fast cannot serve. Complexity can always be added later; it cannot be removed. (Rob Pike / Go `errgroup` rationale.)
- **Cancellation: immediate.** In-flight experts are cancelled via context the moment any peer fails. No point consuming tokens on results that will be discarded.
- **Downstream visibility:** the error names what failed and why. The succeeded list is `--verbose` only — the user's job is to fix the failure, not receive a consolation prize list.

```
error[X004]: parallel block failed — FactChecker returned status: fail
  = "source document contained no verifiable claims"
  = help: fix FactChecker or remove it from the parallel block
```

---

## 4. Context accumulation

**Status: Resolved**

**Context:**
Each expert receives all prior output via `{{output}}`. In a long pipeline this
context grows unboundedly and can exceed a model's context window — a silent
failure mode unique to this domain that no traditional language has to handle.

**Questions to answer:**
- Is this a language concern or a runtime concern?
- Should the language offer a construct to truncate/summarise context at a step?
- If deferred, should it be formally documented as a known gap in `language.md`?

**Decision:**

- **Language concern, not a runtime concern.** The runtime never invisibly truncates or summarises context. Hidden magic produces non-reproducible results — the same pipeline runs differently depending on what the runtime decided to cut. (Rob Pike rationale: identical to rejecting auto-parallelism inference.)
- **No new language primitive.** A `ContextSummariser` expert is sufficient forever. The author inserts it explicitly when they care about context pressure. This is a known pattern, not a gap.
- **Documented as a known pattern** in `docs/design/stdlib.md` — not a gap. The distinction matters: a gap implies something missing from the language; a pattern implies deliberate design. The language gives you the right tool — an expert.
- **`ContextSummariser` is a standard library expert.** It passes all four stdlib gates (see `docs/design/stdlib.md`) and ships embedded in the forge binary. No declaration needed in `forge.toml`.
- **Hard error on overflow** per Decision 1 — the safety net when no `ContextSummariser` is present:

```
error[X005]: context window exceeded at step 5 (Critic)
  = accumulated context: ~130 000 tokens (model limit: 128 000)
  = help: add a ContextSummariser step before Critic in your pipeline
```

**Standard library definition** formalised in this decision: see `docs/design/stdlib.md` for the four gates that govern all future stdlib inclusion decisions.

---

## 5. `with { provider }` ambiguity

**Status: Resolved**

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

- **`using <identifier>` is the per-step profile selector.** `with {}` becomes purely domain context — no reserved keys, no ambiguity. The two constructs are orthogonal and fully composable.
- **Identifier, not string literal.** Profile names are name references, not string values. `using architect` reads naturally; `using "architect"` treats a name like data.
- **`with {}` is clean from this point forward.** Any key inside `with {}` is a domain variable. No parser special-casing, no reserved key list to maintain.
- **Grammar change belongs in Phase 25 Spoke 1** — one new optional production on `step`:

```antlr
step        : UPPER_ID usingClause? withClause? ;
usingClause : 'using' LOWER_ID ;
```

Usage:
```fsharp
-> SecurityArchitect using architect with { style = "terse ADR" }  ← profile + domain context
-> SecurityArchitect using architect                                ← profile override only
-> SecurityArchitect with { style = "terse ADR" }                  ← domain context, default profile
-> SecurityArchitect                                                ← default profile, no context
```

---

## 6. Mission metadata

**Status: Resolved**

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

- **Deferred to Phase 26.** Mission parameter names are sufficient for everything Phase 25 needs — CLI invocation, `forge serve` endpoints, and LSP parameter completion. The gap only becomes blocking when mission-to-mission composition arrives.
- **Asymmetry with expert.md is intentional, not accidental.** Expert frontmatter is load-bearing interface specification — the resolver needs it to wire steps. Mission parameters are entry-point arguments — the CLI handles them by name. Different roles, different needs.
- **Intended design when added:** inline parameter annotations in `mission.mcl` — descriptions on the names themselves, no third file, additive minor grammar bump:

```fsharp
mission BuildOperatorDesign(goal: "the design objective", persona: "the intended audience") =
    ...
```

- **Not a separate `mission.md` file.** Experts are markdown because their content *is* a system prompt. Missions are code — a companion `.md` is unnatural and would introduce a third file per mission directory.
- **Phase 26 scope note:** add inline parameter annotations alongside source positions. Both are additive grammar changes; they arrive together as a planned extension.

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

**Status: Resolved**

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

- **Phase 25 — not a separate phase.** `when {}` is load-bearing for the `Classifier` stdlib expert and for `forge serve` interaction modes. It ships in Spoke 1 as a grammar change.
- **Exact string match only for Phase 25.** Richer expressions (`>`, `or`, `contains`) are deferred until the typed context bag arrives in Phase 22. Rushing expressions against a stringly-typed bag is dishonest — `priority > 5` where `priority = "7"` is string comparison dressed as numeric. The grammar is designed to be extensible: new expression types are additive `WhenExpression` subclasses, minor version bump, no existing syntax touched.
- **Plain context bag key.** `Classifier` writes a value into the context bag; `when {}` reads it. One mechanism, no special cases. Same path as every other expert-to-expert handoff.
- **Silent skip + `--verbose` trace per unmatched step.** Unmatched is the expected case in routing — only one branch fires by design.
- **`when { else }` is the explicit default branch.** Without it, an unmatched classifier is a silent hole. Hard error if no step matches and no `when { else }` is present:

```
error[X006]: no step matched for classifier output
  = Classifier returned: mode = "unknown"
  = guards checked: "design", "task", "review"
  = help: add `-> Fallback when { else }` to handle unmatched cases
```

**Grammar:**
```antlr
step       : UPPER_ID usingClause? whenClause? withClause? ;
whenClause : 'when' '{' whenExpr '}' ;
whenExpr   : LOWER_ID '=' STRING   # StringEquals
           | 'else'                # Else
           ;
```

**Implementation constraint — typed context bag seam (non-negotiable):**
The context bag must ship as `Dictionary<string, ContextValue>` where `ContextValue` is a wrapper record — not `Dictionary<string, string>`. This is the one-hour design choice that keeps the typed context bag pivot clean. When Phase 22 arrives, `ContextValue` expands to a discriminated union internally; no call sites change. Concreting `string` across the codebase now forecloses strong typing permanently.

```csharp
record ContextValue(string Raw);           // Phase 25
// Future — internal expansion only:
// abstract record ContextValue;
// record StringValue(string Raw) : ContextValue;
// record IntValue(int Value) : ContextValue;
```

`with {}` bindings and `let` bindings follow the same `ContextValue` wrapper — the typed context bag is a language-wide non-negotiable, not a context-bag-only concern. LLM expert outputs remain text at the LLM level; the runtime coerces to typed values via structured `StepEnvelope` output (the pattern already established for `status` and `reason`).

---

---

## 9. Loop context — deterministic convergence vs random retry

**Status: Resolved — partially superseded, see note below (2026-08-10)**

**Superseded note (2026-08-10):** the "Decision" recorded below — `{{feedback}}` removed as a
public developer API, automatic structured (criterion/reason/suggestion) injection to the first
expert only — is **not** what was actually built. What shipped, and what
[`docs/design/language.md`](../design/language.md#L330) documents as the canonical, current design,
is simpler: `{{feedback}}` **is** a public reserved runtime variable, a raw string (the failing
expert's `onFail`/`reason` message, not a structured critique), which any expert's prompt can
reference explicitly — not injection restricted to the first expert only. `language.md:332`
explicitly documents it as populated by a failing `role:judge` **or** `kind:rule` expert. This
section was never updated after that simplification happened, so it read as current design when it
is not — kept below for historical record, not as the live spec. `language.md` is the source of
truth for `{{feedback}}`'s actual behavior.

**Second superseded note (2026-08-10, same day, deeper finding):** investigating a live bug in
[missions/janus/](../../missions/janus/) (Phase 43.15) surfaced that `{{feedback}}` — Option A,
"most recent failure only" — was never actually a considered final answer either. Re-reading
[`docs/design/research.md`](../design/research.md#L181): *"The literature suggests Option B or C
produces the best results. Option A is the minimum viable implementation."* Option A shipped anyway
(Phase 28, for `kind:rule`) because it was what a different, unrelated feature needed at the time,
and nobody came back to revisit it. Full design conversation and final resolution recorded in
[phase-43.15-janus-inter-agent-mission.md](phase-43.15-janus-inter-agent-mission.md) (2026-08-10
session) — summary:

- **Not Option B/C as originally conceived either.** Both assumed a single generator + a judge
  (Self-Refine/Reflexion shape — one-directional, judge has no memory of its own past verdicts).
  Janus's actual purpose — *"Proposer/Approver negotiate... questions get answered on retry"* — is
  a genuine two-party dialogue, not a generator being critiqued. That shape needs both parties to
  remember, which neither Option A/B/C addresses.
- **The real bug wasn't only the missing `{{feedback}}` wiring — it was mission structure.** The
  original `mission Janus(task) loop(3) = { Proposer -> Approver -> Implementer }` put a one-shot
  execution step (`Implementer`) inside the same loop as the two negotiating parties. Once you ask
  "should this loop replay conversation history among its participants," `Implementer` was never a
  participant — it shouldn't have been in that loop at all.
- **Resolution: `loop(N)` replays full conversation history by default among LLM-backed
  participants only** (not `kind:rule`/`kind:exec`/`kind:onnx`/`kind:search` steps sharing the same
  loop) — no explicit opt-in flag. Justification: this is the universal norm for every consumer chat
  product (ChatGPT, Claude, Grok, Gemini, Perplexity) — a conversation replays silently by default;
  requiring an explicit flag to get that behavior back would be a knob for its own sake, against
  this project's own "fewer knobs" design philosophy. What already provides the necessary scoping
  and explicitness — matching this doc's own Decision #4 "no hidden magic" principle — is **mission
  composition** (Decision #11, already shipped): a mission's boundary is itself the "new
  conversation" signal, exactly like opening a new chat thread. `kind:rule`/`kind:exec` loops (e.g.
  `Drafter -> WordCountGate`, Phase 28's shipped pattern) are unaffected — no LLM-to-LLM replay
  applies there, today's single-string `{{feedback}}` behavior is unchanged.
- **Corrected Janus shape** — split into two missions along the negotiation/execution boundary,
  composed via the existing (already-shipped) sub-mission mechanism:
  ```fsharp
  mission Negotiate(task) loop(3) = {
      Proposer using implementer
      -> Approver using architect
  }
  mission Implement(plan) = {
      Implementer using implementer
  }
  mission Janus(task) = {
      Negotiate(task: task)
      -> Implement(plan: output)
  }
  ```
  `Negotiate` is 100% LLM participants → full replay applies automatically. `Implement` is a
  separate mission → structurally cannot see the negotiation, no special-case needed — mission
  composition's existing "no context inheritance" rule (Decision #11) already guarantees it.

**Third superseded note (2026-08-10, same day, engine-level design closed):** the note above left
one open question — `Conversation` (`src/ForgeMission.Core/Runtime/Conversation.cs`, used for
`role: agent` tool continuations) has fixed message roles from one client's point of view, but
Proposer and Approver each need the *opposite* role-view of the same shared history. Resolved as a
new, separate type — `Conversation` is untouched, this is not a variant of it:

- **New type `SpeakerTranscript`** (`src/ForgeMission.Core/Runtime/SpeakerTranscript.cs`) — an
  ordered list of `(Speaker: string, Text: string)` turns, where `Speaker` is the producing step's
  `ExpertName`. `AsMessages(string self)` re-tags each turn per recipient at read time: `Speaker ==
  self` → `ChatRole.Assistant`, otherwise → `ChatRole.User`. No fixed viewpoint is baked in — the
  same accumulator serves both parties, tagged differently on each read.
- **Eligibility (no opt-in flag, matches this decision's "no explicit flag" ruling above)** — a
  mission is negotiation-eligible when `MaxLoops > 1` and every `Pipeline.Elements` entry is a
  `StepElement` (no `ParallelElement` — concurrent turns break "who spoke when") resolving to an
  expert in the mission's own `experts` dict (excludes sub-mission steps) with `Kind == "llm"`
  (the default) and `Role != "agent"`. Computed once in `PipelineRunner.RunAsync`, before the
  `for (attempt ...)` loop.
- **Lifetime = one `RunAsync` call, not one attempt.** The `SpeakerTranscript` instance is created
  once, outside the attempt loop (same shape as the existing `loopFeedback` local), and the *same*
  instance is re-injected into `context["history"]` every attempt — so it accumulates across the
  loop's retries. A sub-mission call recurses into a fresh `RunAsync`, which allocates its own new
  instance — mission boundary = new conversation falls out for free, no special-casing needed, same
  as the `Negotiate`/`Implement` split above.
- **Turn recording** — in `PipelineRunner.ExecuteStepAsync`, right after `context["output"] =
  envelope.Text`, if `context["history"]` is present, append one turn: `Speaker = step.ExpertName`,
  `Text = envelope.Text` — plus `envelope.Reason` appended (`"{Text}\n\nReason: {Reason}"`) only
  when `Reason` is non-empty and differs from `Text` (Approver's own schema already duplicates the
  two on fail, so the common case stays a single clean turn).
- **Consumption** — `DirectExpertRunner.BuildMessages` (`RunAsync` and `StreamAsync` both call it)
  checks `context["history"]` first: if present and non-empty, the message list is `[System(own
  interpolated prompt)] + history.AsMessages(expert.Name)`, replacing today's single
  `[System, User(context["output"] or "Begin.")]` pair. Falls through to the existing single-turn
  behavior when no history is present (attempt 1's first speaker, and every non-negotiation
  mission) — this is additive, not a rewrite of the existing path. Mutually exclusive with the
  `role: agent` tools/`Conversation` branch already in `DirectExpertRunner` (that branch only
  triggers for `IsAgent` steps, which negotiation-eligibility excludes by construction).
- **Dead reference to remove**: `missions/janus/experts/Proposer/expert.md`'s `{{feedback}}` line —
  it was always empty (`DirectExpertRunner` never wrote `context["feedback"]`, only
  `RuleExpertRunner`/`ExecExpertRunner` do) and is superseded by Approver's actual turn now arriving
  via history directly.

Design is closed — no remaining open question. Task assignment covering this plus the mission split
together: [phase-43.15's spoke](phase-43.15-janus-inter-agent-mission.md).

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

- **`loop N` is replaced by `loop(N)`.** The judge is the last step in the pipeline — visible, explicit, not hidden in a parameter. The runtime loops until the last step returns `status: pass` or N attempts are exhausted. Any expert can be the judge; the loop doesn't care about the name, only the `StepEnvelope` status.

- **`{{feedback}}` is removed as a public developer API.** The platform manages feedback injection automatically — the developer declares `loop(N)`, the runtime handles convergence. No prompt engineering required.

- **Feedback injection: first expert only, automatic, structured.** The runtime prepends a structured critique to the first expert's context on each retry. Structured per Constitutional AI model (criterion, reason, suggestion) — not a raw failure string. No `feedback_target` parameter — platform decides, always first expert. The research confirms this is the most effective injection point.

- **Two distinct primitives — different topologies, different use cases:**

| Primitive | Shape | Research backing | Phase |
|---|---|---|---|
| `loop(N)` | Sequential convergence — full pipeline reruns with feedback | Self-Refine, Reflexion | Phase 25 |
| `debate(rounds: N) { }` | Parallel exploration — agents cross-critique for N rounds, synthesiser follows | Multi-Agent Debate, MoA | Separate phase |

- **Research-backed defaults:** `loop(2)` or `loop(3)` — 2-3 attempts is the sweet spot. `debate(rounds: 3)` — diminishing returns and degradation beyond round 5. Runtime warns if `rounds > 5`.

- **2 × 3 beats 6 flat rounds.** The retry reset gives agents fresh diversity; the judge's structured signal breaks plateau that peer critique cannot. `loop` and `debate` solve different failure modes and are composable:

```fsharp
mission DeepReview(input) loop(2) = {
    debate(rounds: 3) {
        SecurityExpert
        ArchitectExpert
        CriticalReviewer
    }
    -> Synthesiser
    -> QualityJudge
}
```

- **`debate` deferred to a separate phase** — requires round orchestration, per-round context summarisation, and cross-agent output wiring. Not Phase 25 scope.

---

## 10. Syntax consolidation

**Status: Resolved** *(emerged during Decision 8 and 9 discussion)*

Several syntax decisions were made that constitute a breaking change from prior phases. Pre-1.0, no migration required.

- **`with { key = value }` → `(key: value)`.** Named parameters with `:`. The `with` keyword is removed. Domain context is passed directly on the step using the same `(key: value)` syntax as execution config.
- **`when { key = value }` → `when(key: value)`.** Guard conditions use the same named parameter pattern.
- **`when { else }` → `when(else)`.** `else` is a keyword value inside the guard.
- **Braces everywhere.** Mission body uses `= { }`. Every scope has an explicit open and close. Not whitespace-sensitive. Rob Pike argued for this on consistency grounds; Anders Hejlsberg's counter (the `=` is semantically "is defined as") was considered but consistency wins at this stage of the language.
- **`:` is the universal named parameter separator.** Used in step context `(source: codebase)`, execution config `debate(rounds: 3)`, and guard conditions `when(mode: "design")`.

Full syntax reference:

```fsharp
mission SecurityAudit(codebase) loop(2) = {
    DataExtractor(source: codebase)
    -> debate(rounds: 3) {
        SecurityExpert using architect
        ArchitectExpert
        CriticalReviewer
    }
    -> Synthesiser
    -> QualityJudge
}

mission HandleRequest(input) = {
    Classifier
    -> Architect when(mode: "design")
    -> Developer when(mode: "task")
    -> Fallback  when(else)
}
```

---

## 11. Mission composition

**Status: Resolved** *(emerged during pre-flight discussion)*

**Core principle:** a mission and an expert are identical at the interface level. Both take input, both produce output. The caller never knows or cares which it is. The difference is internal structure only.

- **Missions are usable as steps in other missions.** Same syntax — no new keyword.
- **Explicit parameter binding.** Parameters are bound at the call site: `CodeReview(codebase: goal)`. Context inheritance (inner mission sees outer context bag) is rejected — leaky, implicit, harder to reason about.
- **Resolution order gains a new tier:**

```
1. ./experts/<Name>/expert.md    ← leaf: single LLM call
2. ./missions/<Name>.mcl         ← composite: sub-pipeline
3. ~/.forge/cache/<Name>/        ← OCI (expert or mission)
4. forge stdlib                  ← built-in experts only
5. error[R002]: not found
```

- **`forge.toml` declares missions under `[experts]`** — from the caller's perspective, it is an expert. No separate `[missions]` section.
- **OCI can publish missions.** A published mission is a reusable reasoning component, same artifact format as an expert.
- **Composition is arbitrarily deep.** Small missions compose into larger missions. The tree has no depth limit.

```fsharp
mission CodeReview(codebase) loop(2) = {
    Analyser
    -> SecurityChecker
    -> Synthesiser
    -> QualityJudge
}

mission FullDevelopmentCycle(goal) = {
    RequirementsAnalyst
    -> CodeReview(codebase: goal)       ← mission as step, explicit binding
    -> DeploymentPlanner
}

mission QuarterlyRelease(objectives) = {
    Planner
    -> FullDevelopmentCycle(goal: objectives)   ← two levels deep
    -> ReleaseNotesWriter
}
```

---

## Completion gate

All decisions must be recorded above before Phase 25 Spoke 1 begins.
