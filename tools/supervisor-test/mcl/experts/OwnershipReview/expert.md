---
name: OwnershipReview
kind: exec
command: python3
args: [../../../harness.py, stage, OwnershipReview]
inputs: [run_dir, work_dir, design]
outputKey: ownership_review
timeout: 12m
input: Explicit experiment artifacts
output: Structured ownership_review
outputKeys:
  ownership_review: string
---

Runs only this stage through the shared Codex adapter.
