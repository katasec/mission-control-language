# Turn 23 — Duplicate build-ready design

## Requested

Provide the final build-ready responsive design.

## Prompt used

**No new relay was sent.** This was a literal duplicate of Turn 22's response to [R23 in the Codex handoff transcript](claude-relay-transcript.md#r23).

## Better prompt

“Include the version/hash of the last approved artifact. If the outgoing response matches it,
report a duplicate-send error rather than consume another human relay and review turn.”

## What Claude did wrong

This was a literal duplicate of turn 22, accidentally relayed again. It created review churn but
did not add a new design or implementation defect.

## Prevention

Track the last accepted relay hash/title in the handoff record and flag an identical resubmission
before it is sent for review.
