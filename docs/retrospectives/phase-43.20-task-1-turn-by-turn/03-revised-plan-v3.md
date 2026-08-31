# Turn 03 — Revised plan v3

## Requested

Ensure Desktop and a future TUI expose the same underlying capability.

## Prompt used (reconstructed)

“Add the non-negotiable rule that every Desktop capability must also be possible through the TUI.”

## Better prompt

“For every owned action, name the existing Client Runtime request, outcome, authorization, and
failure contract. State that Desktop contributes presentation only; reject any Desktop-only action.”

## What Claude did wrong

The response described parity mostly as a test/proof. It did not yet make the shared Client Runtime
action contract the non-negotiable design boundary for every Desktop action.

## Prevention

The handoff template should ask: “Which shared action contract performs this?” and reject a plan
that answers with a Desktop, host, or UI-specific mechanism.
