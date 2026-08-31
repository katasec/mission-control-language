# Turn 08 — Implementation summary v2

## Requested

Correct the discovered empty-goal validation flaw and report the updated implementation.

## Prompt used

**Verbatim source:** [R08 in the Codex handoff transcript](claude-relay-transcript.md#r08).

This is the full relay text used for this turn, preserved without summary or reconstruction.

## Better prompt

“Map every named precondition to a positive and negative test before changing code; include empty,
whitespace-only, and overridden-derived-field cases in the implementation checklist.”

## What Claude did wrong

The original implementation allowed title input to mask an empty goal. The missing negative case
was found only after the first summary.

## Prevention

Derive negative tests directly from each named precondition in the spoke before coding: a goal is
required regardless of derived or overridden display fields.
