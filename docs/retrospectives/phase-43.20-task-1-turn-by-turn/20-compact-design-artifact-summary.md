# Turn 20 — Compact design artifact summary

## Requested

Measure the real viewport, create compact SVGs, and specify the responsive launcher before code.

## What Claude did wrong

The artifacts correctly measured 800×568, but the specification still optimized two endpoints and
used packaged-host assertions as the primary proof. It lacked continuous browser resize, long-content,
zoom, and full-range acceptance.

## Prevention

Treat reference sizes as boundary checkpoints on a fluid range. Browser-first evidence must cover
corners, continuous resize, long values, zoom, tokens, and theme modes.
