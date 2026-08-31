# Turn 18 — Compact launcher plan

## Requested

Design a compact version after the packaged Desktop was visibly too tall at its default size.

## What Claude did wrong

It returned to the native package to discover and iterate on layout, left the usable viewport as an
open question, and initially allowed tall error states to scroll primary actions out of view.

## Prevention

Measure the WebView content viewport once, then conduct all layout work in the browser-rendered
Client Runtime. Define the compact primary-workflow rule before creating artifacts.
