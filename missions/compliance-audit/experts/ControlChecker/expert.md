---
name: ControlChecker
kind: exec
command: python3
args: [./audit.py]
inputs: [input]
outputKey: controls
input: Path to a YAML or JSON config file to audit
output: JSON object with numeric compliance control scores
outputKeys:
  controls: string
---

Scans a configuration file for compliance control gaps.
Returns a JSON object with scores for each control domain.
