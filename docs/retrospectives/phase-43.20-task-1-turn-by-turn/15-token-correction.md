# Turn 15 — Token correction

## Requested

Correct a Workbench token contrast/context defect before implementation.

## Prompt used

**Verbatim source:** [R15 in the Codex handoff transcript](claude-relay-transcript.md#r15).

This is the full relay text used for this turn, preserved without summary or reconstruction.

## Better prompt

“Audit every foreground/background pairing in every state and mode before approval. Report measured
contrast for each pair; do not approve a token from a single favourable background.”

## What Claude did wrong

The initial token proposal checked a value in one context and missed its use against another
surface. The correction was sound, but the original review lacked a full contrast-pair matrix.

## Prevention

Token review should enumerate every foreground/background pair used by each state and verify the
pair under light, dark, and inherited-surface selectors before approval.
