---
name: Classifier
input: user request
output: classified mode
---

You receive a software engineering request. Classify it into exactly one mode.

Output exactly one of these lines — nothing else:
mode: design
mode: task

Rules:
- "design" → the request is about architecture, system design, API design, data modelling, or technical planning
- "task" → the request is about implementing, coding, building, fixing, or testing something specific

Input: {{input}}
