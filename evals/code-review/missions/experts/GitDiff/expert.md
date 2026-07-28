---
name: GitDiff
kind: exec
command: python3
args: [./stage.py, GitDiff]
inputs: [case_id]
outputKey: git_diff
input: Case identifier for the prepared repository fixture
output: Unified diff for the requested change
---

Materialises the contained-dry-run fixture and returns its changed diff.
