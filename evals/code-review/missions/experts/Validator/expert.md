---
name: Validator
role: judge
kind: exec
command: python3
args: [./validator.py]
inputs: [output]
outputKey: validation_result
input: Repaired contained-dry-run workspace
output: Deterministic public and hidden-test grade
---

Runs the contained-dry-run oracle's public validation commands and hidden tests.
On rejection, returns the exact actionable failure detail as loop feedback.
