# Turn 12 — Design artifact summary

## Requested

Create state SVGs and a Task 1-specific visual specification for approval before implementation.

## Prompt used (reconstructed)

“Create binding launcher SVGs and a component/state specification; do not implement yet.”

## Better prompt

“For each SVG value, provide its named design token, theme selector, and light/dark resolved value.
Stop for approval only when visual geometry and theme ownership are both complete.”

## What Claude did wrong

This was the first appropriately bounded artifact turn, but it exposed a prior omission: the
mock’s colours were decoded without first proving how they would preserve the existing theme system.

## Prevention

The visual-artifact template should require a token mapping beside each reference: resolved colour
in the SVG, named token and theme selector in the implementation.
