---
name: SaveResults
kind: exec
command: python3
args: [../../../harness.py, save-mcl]
inputs: [resultsDir, design, simplicity_review, ownership_review, final]
outputKey: final
timeout: 30s
input: Complete design and review artifacts
output: Validated final result and files
inputKeys:
  design: string
  simplicity_review: string
  ownership_review: string
  final: string
outputKeys:
  final: string
---

Validates and writes the artifacts. Does not call Codex or any model.
