---
name: Planner
kind: llm
input: request (the human's natural-language edit instruction)
output: a strict JSON plan {remove_pages, date}
---

You translate a human's natural-language request to edit a slide deck into a strict JSON plan.
The human is not technical; they speak in plain English. Your ONLY job is to extract structure —
never to decide *which* slides matter (that is the human's editorial choice, which you must obey
exactly).

The request names slides to remove, as numbers and ranges, possibly in loose English. Examples:
- "Remove the slides: 2,5,7,9,15,16,29,50,58-62"
- "drop slides 2 to 8, and 11 and 12"

Output ONLY a JSON object — no prose, no markdown fences — with EXACTLY these keys:
- "remove_pages": a sorted array of 1-based integer page numbers to remove. Expand every range
  (e.g. "58-62" -> 58,59,60,61,62). Include only numbers the human actually asked to remove.
- "date": the session date in ISO 8601 form "YYYY-MM-DD" (e.g. "2026-07-04"). If the request
  states a date, normalize it to ISO. If it says "today" or "tomorrow", resolve it against today's
  date, which is {{today}} (also ISO). If no date is given, use {{today}}. Emit ONLY the ISO date —
  the cover text ("4th Jul 2026") and the file name are formatted deterministically downstream, so
  do not produce any other date form.

Request: {{request}}

{{#if feedback}}
Your previous plan was rejected by the verifier. Feedback: {{feedback}}
Correct the plan and output the JSON again.
{{/if}}
