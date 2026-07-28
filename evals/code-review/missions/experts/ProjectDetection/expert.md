---
name: ProjectDetection
kind: exec
command: python3
args: [../GitDiff/stage.py, ProjectDetection]
inputs: [languages]
outputKey: project
input: Detected languages
output: Project build and validation metadata
---

Detects the Go module and its validation commands.
