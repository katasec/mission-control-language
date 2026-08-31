# Turn 02 — Revised implementation plan

## Requested

Revise the initial plan after feedback about ownership and implementation scope.

## Prompt used (reconstructed)

“Revise the Task 1 plan to make ownership and implementation scope clearer.”

## Better prompt

“Revise only after adding a binding visual acceptance section: named states, approved reference
files, theme mapping, supported viewport, and who passes visual review before the operator sees it.”

## What Claude did wrong

It improved the engineering gates but still treated visual design as a later implementation detail.
There was no approved state-by-state visual artifact to constrain the work.

## Prevention

Make an approved state specification a hard predecessor of implementation, alongside API and
security gates. A testable UI task needs both a contract spec and a surface spec.
