# Turn 17 — Visual slice implementation summary v2

## Requested

Refine theme-owned geometry and repeat the visual report.

## Prompt used

**Verbatim source:** [R18 in the Codex handoff transcript](claude-relay-transcript.md#r18).

This is the full relay text used for this turn, preserved without summary or reconstruction.

## Better prompt

“Use a viewport acceptance matrix, not a single spacious screenshot: record each required state at
the actual default viewport and large reference viewport before claiming a visual PASS.”

## What Claude did wrong

The second self-PASS still used the 1536×1024 evidence path and therefore repeated the same
unmeasured default-window risk. Geometry ownership improved, viewport acceptance did not.

## Prevention

The acceptance checklist should make every required viewport a separate PASS row; a result cannot
be called complete while any required row is absent.
