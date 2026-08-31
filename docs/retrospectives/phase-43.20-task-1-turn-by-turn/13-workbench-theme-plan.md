# Turn 13 — Workbench theme plan

## Requested

Ensure the new launcher reskins through the existing theming system instead of hard-coded colours.

## Prompt used (reconstructed)

“Use the existing design system to reskin the Workbench; do not add a colour hack.”

## Better prompt

“Inspect the current theme system first. Document mode versus product-surface axes, selector
precedence, token additions, contrast pairs, and the automated proof that ForgeUI does not inherit them.”

## What Claude did wrong

Theme architecture was investigated reactively, after the visual artifacts existed. It should have
been a prerequisite because colour mode and product-surface theme are separate concerns.

## Prevention

Every UI plan must begin with a design-system reconnaissance step: existing theme axes, selector
precedence, token ownership, and the test that proves a surface does not leak its palette.
