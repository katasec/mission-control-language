---
name: ApplyFix
role: judge
kind: exec
command: python3
args: [./apply_fix.py]
inputs: [output]
outputKey: apply_result
input: Unified diff proposed by LLMReview
output: Result of applying that diff to the prepared working tree
---

Applies the review's proposed patch to the prepared fixture working tree.
