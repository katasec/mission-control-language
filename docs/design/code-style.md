# Code Style — Progressive Disclosure

**Governing principle: Progressive Disclosure.** Code reveals intent in layers —
*what* at the top, *how* one level deeper. Complexity is layered behind named
functions, not presented as a wall of code the reader must parse upfront. Code
should read close to English intent.

A reader gets intent from the surface and only pays for detail when they choose
to drill in. This is the same instinct as the language's own design philosophy —
fewer knobs, simplicity over flexibility — aimed at how code reads.

---

## Rules

- **Outline-first.** The top 15–20 lines of any file or function must disclose
  intent and flow. If a reader must scroll to understand what a function does,
  refactor.
- **Small, composable functions.** Each function is one coherent named step; aim
  for ~20–40 lines or fewer. Preserve functional cohesion and single responsibility.
  Callers read step names; drilling in reveals implementation. Apply the extraction
  rule below rather than fragmenting a coherent step to meet a size target.
- **Top-down method ordering.** Entry points first, helpers below. A file reads
  like an outline.
- **Explicit error handling.** No `_ = fn()`. Propagate failures through the
  declared contract or handle them at the owning boundary with the specified
  visible result and recovery. Logging alone must not turn failure into apparent
  success. Apply the [failure-boundary gate](engineering-philosophy.md#failure-boundary-gate).
- **No deeply nested branching.** Max 2 levels. Use early returns.
- **Side effects isolated.** DB, network, file, process exec — in clearly named
  functions, not mixed with logic.
- **Zero warnings.** The build must pass with zero warnings; treat warnings as
  errors. (Reinforces the AOT-first rule in `CLAUDE.md` — an ILC warning is a
  real defect here.)
- **No speculative abstractions.** Build for what the task requires. Three similar
  lines of code are better than a premature abstraction.

---

## The tension worth naming

"Small composable functions" and "no speculative abstractions" pull against each
other. Apply the [governing extraction rule](engineering-philosophy.md#design-sensibilities):
extract only when it improves readability of intent, creates a real ownership,
side-effect, or failure boundary, or improves testability. Keep related decisions
together and separate unrelated responsibilities. A function earns its name through
that present benefit, not anticipated reuse, a line-count target, or a lower score.

## Complexity review gate

After reviewing ownership, separation of concerns, contracts and failure semantics,
structural containment, progressive disclosure, and verification, use cyclomatic
complexity as a lightweight final code-review sanity check. It is not an architectural
goal, a substitute for design, or proof of correctness. Low complexity does not replace
unit, contract, negative-path, or default-path verification.

Added or materially changed functions should normally have cyclomatic complexity
**at or below 15**; prefer **10 or below** where readability is not harmed. A breach
of 15 requires refactoring unless the active task records a narrow, justified exception
identifying the affected function, scope, and removal path.

Use a review/analyzer measure appropriate to the language and toolchain, primarily
C#/.NET in this repository. Record the measure used when reporting a score or exception;
this policy does not claim an analyzer is already configured. Complexity accounting
reflects independent control-flow paths, with syntax treatment varying by measure.
For classic McCabe accounting, start at 1 and add independent decisions; a final
`else` is not an independent decision.

Refactor with early returns and extract only coherent, meaningfully named decision
clusters, real side-effect boundaries, or independently testable logic that satisfy
the extraction rule above. Do not game the metric with trivial two-line helpers,
nested or inlined expressions that hide branching, or architecture created only to
lower a score. Speculative strategies, maps, wrappers, configuration knobs, and
defensive branches for states made impossible by authoritative boundaries remain
unjustified even when they produce a lower measured score.
