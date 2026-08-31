# Turn 10 — Task 1 visual re-plan v2

## Requested

Correct the visual acceptance order and define the launcher slice.

## Prompt used

**Verbatim source:** [R10 in the Codex handoff transcript](claude-relay-transcript.md#r10).

This is the full relay text used for this turn, preserved without summary or reconstruction.

## Better prompt

“Return a decision-complete slice: every mock element marked owned/deferred/blocked, no open
composition questions, and one approved set of state references before a build handoff.”

## What Claude did wrong

This was corrective rather than a new defect, but it shows that scope decisions such as the sparse
canvas and deferred rail were being resolved during repeated re-planning instead of before the
first implementation handoff.

## Prevention

The visual spec must include an owned/deferred inventory and answer all composition questions
before it is approved. “Resolve during implementation” is not a closed design decision.
