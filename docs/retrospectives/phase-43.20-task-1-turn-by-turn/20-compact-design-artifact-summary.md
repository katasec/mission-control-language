# Turn 20 — Compact design artifact summary

## Requested

Measure the real viewport, create compact SVGs, and specify the responsive launcher before code.

## Prompt used (reconstructed)

“Create compact 800×568 references and a fluid responsive specification before implementation.”

## Better prompt

“Treat 800×568 and 1536×1024 as boundary checkpoints, not layouts. Specify browser-first evidence
for all states, four corners, continuous resize, long content, zoom, themes, and token audit.”

## What Claude did wrong

The artifacts correctly measured 800×568, but the specification still optimized two endpoints and
used packaged-host assertions as the primary proof. It lacked continuous browser resize, long-content,
zoom, and full-range acceptance.

## Prevention

Treat reference sizes as boundary checkpoints on a fluid range. Browser-first evidence must cover
corners, continuous resize, long values, zoom, tokens, and theme modes.
