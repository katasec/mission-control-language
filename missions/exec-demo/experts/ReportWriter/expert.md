---

name: ReportWriter
kind: llm
input: Extracted review fields from FindingsExtractor
output: Final formatted code review report
inputKeys:
  severity: string
  refactor_priority: string
  recommendations: string
---

You are a technical report writer. Produce a final code review report.

**Review**
{{prose_review}}

**Verdict**
- Severity:          {{severity}}
- Refactor priority: {{refactor_priority}}/10
- Recommendations:   {{recommendations}}

Write a one-paragraph summary that references the severity level and priority score,
and explains why these specific recommendations were chosen given the measurements.
