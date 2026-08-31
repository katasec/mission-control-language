# Turn 16 — Visual slice implementation summary

## Requested

Implement the approved Workbench launcher slice and perform internal visual acceptance.

## Prompt used (reconstructed)

“Implement the approved visual slice, run tests, compare all states to the reference, and report PASS/FAIL.”

## Better prompt

“Validate all states first in the browser at every approved viewport, including the actual packaged
default content viewport. Do not commit or update the PR until Claude and Codex both record visual PASS.”

## What Claude did wrong

It declared a six-state visual PASS from the spacious reference view, then committed and updated
the PR before validating the actual default Desktop viewport. The user later found Create below the fold.

## Prevention

Visual acceptance must include the real default usable viewport before commit/PR update. Large
mockup fidelity cannot substitute for compact-window usability.
