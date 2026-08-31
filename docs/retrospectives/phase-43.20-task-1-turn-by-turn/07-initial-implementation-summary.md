# Turn 07 — Initial implementation summary

## Requested

Implement Task 1 from the approved contract plan and report the result.

## Prompt used

**Verbatim source:** [R07 in the Codex handoff transcript](claude-relay-transcript.md#r07).

This is the full relay text used for this turn, preserved without summary or reconstruction.

## Better prompt

“Do not write source yet: first create and obtain approval for state-by-state visual references and
the acceptance matrix. After implementation, report functional and visual evidence separately.”

## What Claude did wrong

Implementation reached PR #78 before a binding visual artifact and internal visual comparison
existed. Functional and parity gates passed, but the rendered launcher was later rejected.

## Prevention

Do not start a presentation task until its state references are approved. A green test suite proves
behaviour, not fidelity to the intended surface.
