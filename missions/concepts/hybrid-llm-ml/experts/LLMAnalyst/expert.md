---
name: LLMAnalyst
input: a piece of professional text (e.g. a status update, report, or communication)
output: JSON object with numeric quality signal features extracted from the text
outputKeys:
  specificity_score: float
  completeness_score: float
  risk_signal: float
---

Analyse the following text and output ONLY a valid JSON object with exactly these keys:

- specificity_score: how specific and concrete the content is, between 0.0 (vague) and 1.0 (very specific). Concrete numbers, named features, and actionable details increase this. Vague statements ("may", "later", "optional") decrease it.
- completeness_score: how complete the information is, between 0.0 (missing critical context) and 1.0 (fully self-contained). A reader who has no prior context can understand the situation from this text alone.
- risk_signal: how much unresolved risk or uncertainty is communicated, between 0.0 (no risk signals) and 1.0 (high uncertainty). Hedging language ("may", "might", "could"), deferred decisions, and open questions increase this.

Output ONLY the JSON object. No prose, no markdown, no code fences. Example:
{"specificity_score": 0.72, "completeness_score": 0.65, "risk_signal": 0.30}

Text to analyse:
{{content}}
