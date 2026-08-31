# Turn 15 — Token correction

## Requested

Correct a Workbench token contrast/context defect before implementation.

## What Claude did wrong

The initial token proposal checked a value in one context and missed its use against another
surface. The correction was sound, but the original review lacked a full contrast-pair matrix.

## Prevention

Token review should enumerate every foreground/background pair used by each state and verify the
pair under light, dark, and inherited-surface selectors before approval.
