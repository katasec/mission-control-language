# Turn 22 — Build-ready responsive design

## Requested

Correct the four-corner and lower-bound gaps, then provide a build-ready plan.

## What Claude did wrong

No new defect was identified in this response. It incorporated the missing corners, removed the
arbitrary floor, and found the width-driven vertical-padding flaw before implementation.

## Prevention

The lesson is positive: a rectangular viewport matrix and axis-specific sizing should have been in
the first responsive spec, where they would have caught the flaw without the preceding iterations.
