# Turn 19 — Duplicate compact launcher plan

## Requested

Provide the compact-launcher plan.

## Prompt used

**Verbatim source:** [R20 in the Codex handoff transcript](claude-relay-transcript.md#r20).

This is the full relay text used for this turn, preserved without summary or reconstruction.

## Better prompt

“Attach a revision delta. If the measurement, decisions, artifacts, and evidence are unchanged,
do not resend the plan—say that no new response is required.”

## What Claude did wrong

This was a literal duplicate of turn 18. It repeated the unresolved measurement and scrolling
assumptions instead of progressing the design.

## Prevention

Require revision fingerprints: changed decisions, changed files, and changed evidence. If none
changed, the sender must report “no revision” rather than resubmit the same plan.
