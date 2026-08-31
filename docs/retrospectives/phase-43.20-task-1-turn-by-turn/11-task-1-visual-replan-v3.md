# Turn 11 — Task 1 visual re-plan v3

## Requested

Amend the visual plan to avoid inert deferred UI and match the approved visual language.

## What Claude did wrong

The needed correction arrived only after two earlier re-plans. The design had not initially
distinguished a deliberately deferred capability from a misleading inert visual placeholder.

## Prevention

Require every visual specification to classify each reference element as owned, deferred, or a
decision needed before build; deferred controls must be omitted, not simulated.
