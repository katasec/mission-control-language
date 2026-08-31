# Turn 13 — Workbench theme plan

## Requested

Ensure the new launcher reskins through the existing theming system instead of hard-coded colours.

## What Claude did wrong

Theme architecture was investigated reactively, after the visual artifacts existed. It should have
been a prerequisite because colour mode and product-surface theme are separate concerns.

## Prevention

Every UI plan must begin with a design-system reconnaissance step: existing theme axes, selector
precedence, token ownership, and the test that proves a surface does not leak its palette.
