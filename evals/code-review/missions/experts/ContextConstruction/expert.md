---
name: ContextConstruction
kind: exec
command: python3
args: [../GitDiff/stage.py, ContextConstruction]
inputs: [git_diff, changed_files, languages, project]
outputKey: review_context
input: Diff, changed files, languages, and project metadata
output: Compact review context for the model
---

Constructs the complete deterministic context passed to the review model.
