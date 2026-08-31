# Turn 09 — Task 1 visual re-plan

## Requested

Recover from the visible mismatch between the running launcher and the journey mock.

## Prompt used

**Verbatim source:** [R09 in the Codex handoff transcript](claude-relay-transcript.md#r09).

This is the full relay text used for this turn, preserved without summary or reconstruction.

## Better prompt

“Create an owned/deferred visual slice and define acceptance order exactly: Claude PASS, Codex
PASS, then operator acceptance. A failure remains internal and returns to design or implementation.”

## What Claude did wrong

The first visual re-plan still put the operator too early in the acceptance sequence. It had not
made Claude and Codex visual PASS prerequisites for operator review.

## Prevention

The workflow must state one fixed order: approved references, implementation, Claude comparison,
Codex evidence review, then operator acceptance. A failed internal comparison never reaches the operator.
