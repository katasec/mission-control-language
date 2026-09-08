---
name: Verifier
role: judge
kind: exec
command: python3
args: [./verify.py]
input: json
inputs: [output]
outputKey: verdict
output: the verified original answer on pass; a failure verdict otherwise
onFail: "No month name contains the letter X. This is a trick question — the answer should say none."
---
