---
name: RepositoryInventory
kind: exec
command: python3
args: [../GitDiff/stage.py, RepositoryInventory]
inputs: [git_diff]
outputKey: inventory
input: The changed diff
output: Repository inventory relevant to the review
---

Lists the repository files relevant to the requested change.
