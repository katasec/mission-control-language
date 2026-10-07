---
name: SupervisorDecision
kind: exec
command: python3
args: [../../../harness.py, stage, SupervisorDecision]
inputs: [run_dir, work_dir, design, simplicity_review, ownership_review]
outputKey: final
timeout: 12m
input: Explicit experiment artifacts
output: Structured final
outputKeys:
  final: string
---

Runs only this stage through the shared Codex adapter.
