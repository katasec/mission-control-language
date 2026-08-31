# Turn 14 — Workbench token tables

## Requested

Supply concrete token tables and the implementation file list for the Workbench surface.

## Prompt used (reconstructed)

“Provide the Workbench token values, selector plan, and affected-file list for approval.”

## Better prompt

“Return one complete token table in the first theme plan: semantic token, light value, dark value,
reference evidence, contrast background, owning selector, and affected consumer.”

## What Claude did wrong

The tables arrived as a separate corrective artifact because token ownership and contrast evidence
were not demanded in the first visual plan. This added another approval loop.

## Prevention

Make token tables, contrast pairs, and selector ownership required fields of the first visual spec,
not an optional theme follow-up.
