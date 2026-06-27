---

name: FindingsExtractor
kind: json_extract
input: Mixed prose review and structured JSON findings from CodeReviewer
output: Structured findings unpacked into context bag, prose preserved in output
inputKeys:
  severity: string
  refactor_priority: string
outputKeys:
  severity: string
  refactor_priority: string
  recommendations: string
---

Extracts the structured JSON block from the reviewer's mixed output.
The prose review stays in context["output"] for the next step.
The JSON fields (severity, refactor_priority, recommendations) flow into the context bag.
