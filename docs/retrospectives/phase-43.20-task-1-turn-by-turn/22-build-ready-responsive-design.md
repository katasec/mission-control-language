# Turn 22 — Build-ready responsive design

## Requested

Correct the four-corner and lower-bound gaps, then provide a build-ready plan.

## Prompt used (reconstructed)

“Correct the rectangular-range proof and remove the unjustified 320px policy; return a build-ready plan.”

## Better prompt

“Show the four-corner sizing model, actual browser acceptance matrix, and the exact implementation
boundary. Only then state that the plan is build-ready and wait for explicit implementation approval.”

## What Claude did wrong

No new defect was identified in this response. It incorporated the missing corners, removed the
arbitrary floor, and found the width-driven vertical-padding flaw before implementation.

## Prevention

The lesson is positive: a rectangular viewport matrix and axis-specific sizing should have been in
the first responsive spec, where they would have caught the flaw without the preceding iterations.
