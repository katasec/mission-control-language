---
name: Timestamp
kind: exec
command: python3
args: [../../../harness.py, mark-time]
inputs: [resultsDir, timingLabel, output]
outputKey: output
timeout: 30s
input: Run folder, stage label and optional previous output
output: Previous output unchanged; real timestamps saved to timings.jsonl
---

Records host-clock timestamps without a model call. Preserves the incoming output exactly so
the next JSON extraction or final output receives the original payload.
