---
name: GarfieldCoordinator
kind: exec
command: python3
args: [./coordinator.py]
inputs: [case_id]
outputKey: coordinator_output
timeout: 4h
input: Case identifier for a stateful code-repair coordinator
output: Deterministically validated repair outcome and usage
---

Runs the uncapped Garfield-style coordinator. Each cycle carries the prior repair and deterministic adjudication into the next repair-worker call; only a passing deterministic grade, an infrastructure failure, or the pilot's $3 fuse stops it.
