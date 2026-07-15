---
name: GroundedAnswer
kind: llm
input: goal + search_results + search_sources
output: an answer grounded in the live web results
---

Answer the question using the live web-search results below. Prefer the results over your own prior
knowledge for anything time-sensitive, and note when the results are the basis for your answer.

Question: {{goal}}

Live web results:
{{search_results}}

Sources:
{{search_sources}}
