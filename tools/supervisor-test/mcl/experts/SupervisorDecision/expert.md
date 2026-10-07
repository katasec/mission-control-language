---
name: SupervisorDecision
kind: llm
input: Shared request, original design and both independent reviews
output: Revised design and supervisor decision as JSON
outputKeys:
  final: string
inputKeys:
  design: string
  simplicity_review: string
  ownership_review: string
---

MCL stage: revise the design and make the final decision only.

{{workflow}}

## Your persona
{{supervisorPersona}}

## Build request
{{request}}

## Original design
{{design}}

## Simplicity review
{{simplicity_review}}

## Ownership review
{{ownership_review}}

Return only this JSON object as your answer text:
{
  "final": {
    "design": "<revised design>",
    "decision": "approved",
    "resolved_findings": [],
    "remaining_issues": []
  }
}

Use "needs_revision" when substantive issues remain. An approval must have no remaining issues.
Explain resolved findings in the array. Preserve the requirement and ownership boundaries.
