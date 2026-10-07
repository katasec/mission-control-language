---
name: SimplicityReview
kind: exec
command: python3
args: [../../../harness.py, stage, SimplicityReview]
inputs: [run_dir, work_dir, design]
outputKey: simplicity_review
timeout: 12m
input: Explicit experiment artifacts
output: Structured simplicity_review
outputKeys:
  simplicity_review: string
---

Runs only this stage through the shared Codex adapter.
