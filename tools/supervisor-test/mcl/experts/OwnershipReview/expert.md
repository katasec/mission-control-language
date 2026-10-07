---
name: OwnershipReview
kind: llm
input: Shared request and original design
output: Independent ownership review as JSON
inputKeys:
  design: string
outputKeys:
  ownership_review: string
---

MCL stage: review behaviour ownership and failure boundaries only.

{{workflow}}

## Your persona
{{ownershipPersona}}

## Build request
{{request}}

## Original design
{{design}}

Return only this JSON object as your answer text:
{"ownership_review": {"verdict": "pass", "findings": []}}

Use "revise" when there are actionable findings. Do not revise the design or approve it.
