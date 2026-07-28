---
name: LanguageDetection
kind: exec
command: python3
args: [../GitDiff/stage.py, LanguageDetection]
inputs: [changed_files]
outputKey: languages
input: Changed files
output: Languages present in the changed files
---

Detects languages from changed-file extensions.
