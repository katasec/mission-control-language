# Turn 08 — Implementation summary v2

## Requested

Correct the discovered empty-goal validation flaw and report the updated implementation.

## What Claude did wrong

The original implementation allowed title input to mask an empty goal. The missing negative case
was found only after the first summary.

## Prevention

Derive negative tests directly from each named precondition in the spoke before coding: a goal is
required regardless of derived or overridden display fields.
