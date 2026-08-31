# Turn 04 — Duplicate revised plan v3

## Requested

Provide the updated Task 1 plan.

## Prompt used (reconstructed)

“Send the revised v3 Task 1 plan for review.”

## Better prompt

“Before sending, compare the proposed reply with the previous relay. If unchanged, report that no
revision was made; otherwise begin with the exact decisions, files, and evidence that changed.”

## What Claude did wrong

This was a literal duplicate of turn 03. It consumed a relay/review turn without incorporating a
new decision or identifying that the content had not changed.

## Prevention

Require each revision to lead with a short changed-since-last-version list and refuse to resend an
unchanged plan unless the operator explicitly requests a copy.
