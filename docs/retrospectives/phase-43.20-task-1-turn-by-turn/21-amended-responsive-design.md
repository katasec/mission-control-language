# Turn 21 — Amended responsive design

## Requested

Make the compact plan browser-first, fluid, token-based, and TUI-safe.

## What Claude did wrong

It improved the method substantially but tested a width/height diagonal while declaring a rectangular
supported range, and it introduced an unjustified 320px lower-bound policy.

## Prevention

Acceptance matrices must test all boundary corners, not only proportional resize paths. Formal
support floors require a user/host rationale; familiar web numbers are not design decisions.
