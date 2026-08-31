# Turn 11 — Task 1 visual re-plan v3

## Requested

Amend the visual plan to avoid inert deferred UI and match the approved visual language.

## Prompt used (reconstructed)

“Amend the visual plan so deferred capabilities are not represented by misleading inert controls.”

## Better prompt

“For each reference element, state its owning task. Omit anything deferred unless it has a real
current capability; do not use decorative placeholders to make an incomplete screen look complete.”

## What Claude did wrong

The needed correction arrived only after two earlier re-plans. The design had not initially
distinguished a deliberately deferred capability from a misleading inert visual placeholder.

## Prevention

Require every visual specification to classify each reference element as owned, deferred, or a
decision needed before build; deferred controls must be omitted, not simulated.
