# Turn 01 — Initial implementation plan

## Requested

Produce a build-ready plan for Phase 43.20 Task 1: create/open a project through the shared Client
Runtime, with Desktop as a presentation surface.

## Prompt used (reconstructed)

“Design Phase 43.20 Task 1 and return an implementation plan. Keep Desktop over the Client Runtime.”

## Better prompt

“Before planning, inventory the shared action contracts, binding visual references, theme tokens,
owned/deferred UI, and acceptance viewports. Return a file-count/scope check and a plan that is not
build-ready until both contract and visual specifications are approved.”

## What Claude did wrong

The plan concentrated on contracts and file operations but did not make the visual mock binding,
theme inheritance, compact-window behaviour, or browser-first acceptance explicit.

## Prevention

The task template should require a visual-reference inventory, the supported viewport range, a
token/theme mapping, and a Desktop/TUI parity statement before a plan can be called build-ready.
