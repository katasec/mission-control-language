---
name: BuildCode
kind: llm
role: agent
input: Shared request and approved final design
output: Actual code files and preserved final decision JSON
inputKeys:
  final: string
---
Build stage only: use Hands Write/Read tools to create the actual requested files under code/.
Make one tool call per turn. You have no terminal tool. The common harness will run tests.
Do not use exec, launch Codex, browse, or create files outside code/. Do not merely describe code.
If final decision is not approved, do not write any code.

## Build request
{{request}}

## Approved design and decision
{{final}}

Write code/retry_delay.py and code/test_retry_delay.py, then Read both files to verify their contents.
After file operations, return exactly one StepEnvelope JSON object with keys text, status, and
reason. Set text to a JSON-encoded string containing the unchanged final decision JSON shown
above, status to "pass", and reason to null. Do not use fences or extra keys. Preserve the final
decision unchanged inside text so the design/review artifacts stay comparable.
