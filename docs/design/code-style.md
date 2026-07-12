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
- **Small, composable functions.** Each function is one named step (~20–40 lines
  max). Callers read step names; drilling in reveals implementation.
- **Top-down method ordering.** Entry points first, helpers below. A file reads
  like an outline.
- **Explicit error handling.** No `_ = fn()`. No silent swallowing. Return errors
  up the chain or log them at the boundary.
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
other. Resolve it this way: **extract for readability of intent, not for
anticipated reuse.** A function earns its existence by giving a step a name that
makes the caller read like an outline — not by guessing at future callers.
