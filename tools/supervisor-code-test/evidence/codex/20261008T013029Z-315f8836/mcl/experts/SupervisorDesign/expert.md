---
name: SupervisorDesign
kind: llm
input: Shared build request and supervisor persona
output: Original design as JSON
outputKeys:
  design: string
---

MCL stage: write the original design only.

{{workflow}}

## Your persona
{{supervisorPersona}}

## Build request
{{request}}

Return only this JSON object as your answer text:
{"design": "<original design>"}

Do not review or revise yet. Do not implement or access external tools.
