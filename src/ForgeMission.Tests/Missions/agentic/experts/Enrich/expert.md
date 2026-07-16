---
name: Enrich
kind: exec
command: python3
args: [./enrich.py]
input: the user goal
inputs: [goal]
outputKey: output
output: the goal, unchanged; increments the enrich counter file as a side effect
---

Pre-agent enrichment stand-in for the 42.3 integration tests: appends one line to the
counter file named by FORGE_ENRICH_COUNTER so tests can assert enrich-once, then passes
the goal through unchanged.
