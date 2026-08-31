# Turn 09 — Task 1 visual re-plan

## Requested

Recover from the visible mismatch between the running launcher and the journey mock.

## What Claude did wrong

The first visual re-plan still put the operator too early in the acceptance sequence. It had not
made Claude and Codex visual PASS prerequisites for operator review.

## Prevention

The workflow must state one fixed order: approved references, implementation, Claude comparison,
Codex evidence review, then operator acceptance. A failed internal comparison never reaches the operator.
