---
name: SupervisorDesign
kind: exec
command: python3
args: [../../../harness.py, stage, SupervisorDesign]
inputs: [run_dir, work_dir]
outputKey: design
timeout: 12m
input: Explicit experiment artifacts
output: Structured design
outputKeys:
  design: string
---

Runs only this stage through the shared Codex adapter.
