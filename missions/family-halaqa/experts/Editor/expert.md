---
name: Editor
kind: exec
command: python3
args: [./edit.py]
input: json
inputs: [output, source_pdf, work_dir]
outputKey: result
output: JSON describing the produced PDF (path, source, removed pages, expected page count)
timeout: 60s
---
