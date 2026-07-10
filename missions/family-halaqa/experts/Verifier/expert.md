---
name: Verifier
role: judge
kind: exec
command: python3
args: [./verify.py]
input: json
inputs: [output]
outputKey: verdict
output: pass or fail — whether the produced PDF faithfully removed the requested pages, kept every other page byte-identical to the source, and prepended a cover
onFail: "The produced PDF failed integrity verification. Re-check the removal list against the request and ensure only the named pages were removed and no kept page was altered."
timeout: 60s
---
