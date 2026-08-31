# Turn 18 — Compact launcher plan

## Requested

Design a compact version after the packaged Desktop was visibly too tall at its default size.

## Prompt used

**Verbatim source:** [R19 in the Codex handoff transcript](claude-relay-transcript.md#r19).

This is the full relay text used for this turn, preserved without summary or reconstruction.

## Better prompt

“Measure `window.innerWidth × window.innerHeight` once in the packaged app, revert the probe, then
do all design and resize work in the browser-rendered Client Runtime. Define no-scroll primary actions.”

## What Claude did wrong

It returned to the native package to discover and iterate on layout, left the usable viewport as an
open question, and initially allowed tall error states to scroll primary actions out of view.

## Prevention

Measure the WebView content viewport once, then conduct all layout work in the browser-rendered
Client Runtime. Define the compact primary-workflow rule before creating artifacts.
