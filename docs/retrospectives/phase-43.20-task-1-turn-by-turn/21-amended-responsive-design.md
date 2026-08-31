# Turn 21 — Amended responsive design

## Requested

Make the compact plan browser-first, fluid, token-based, and TUI-safe.

## Prompt used (reconstructed)

“Amend the responsive design for browser-first validation, fluid layout, named tokens, and TUI parity.”

## Better prompt

“Declare the supported range as a rectangle and test every corner plus a continuous resize. Do not
invent a lower support floor; any such number must cite a Forge host or user requirement.”

## What Claude did wrong

It improved the method substantially but tested a width/height diagonal while declaring a rectangular
supported range, and it introduced an unjustified 320px lower-bound policy.

## Prevention

Acceptance matrices must test all boundary corners, not only proportional resize paths. Formal
support floors require a user/host rationale; familiar web numbers are not design decisions.
