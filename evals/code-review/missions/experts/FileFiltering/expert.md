---
name: FileFiltering
kind: exec
command: python3
args: [../GitDiff/stage.py, FileFiltering]
inputs: [inventory]
outputKey: changed_files
input: Repository inventory
output: The complete changed-files list without filtering
---

Passes through every changed file, matching Garfield's workspace behaviour.
