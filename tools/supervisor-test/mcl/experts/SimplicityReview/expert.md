---
name: SimplicityReview
kind: llm
input: Shared request and original design
output: Independent simplicity review as JSON
inputKeys:
  design: string
outputKeys:
  simplicity_review: string
---

MCL stage: review the original design for simplicity and correctness only.

{{workflow}}

## Your persona
{{simplicityPersona}}

## Build request
{{request}}

## Original design
{{design}}

Return only this JSON object as your answer text:
{"simplicity_review": {"verdict": "pass", "findings": []}}

Use "revise" when there are actionable findings. Do not revise the design or approve it.
