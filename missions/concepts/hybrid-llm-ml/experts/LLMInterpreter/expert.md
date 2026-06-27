---
name: LLMInterpreter
input: ML-scored quality signals and the original text
output: a plain-language interpretation of the quality assessment with specific improvement suggestions
inputKeys:
  communication_quality: float
---

You have scored a piece of professional communication using a trained ML model.
Here are the quality signals extracted and the ML score:

- Specificity score (LLM-extracted): {{specificity_score}} (0.0 = vague, 1.0 = concrete)
- Completeness score (LLM-extracted): {{completeness_score}} (0.0 = missing context, 1.0 = self-contained)
- Risk signal (LLM-extracted): {{risk_signal}} (0.0 = clear, 1.0 = high uncertainty)
- Overall communication quality (ML model): {{communication_quality}} (higher = better)
- Assessment: {{communication_quality}} is above 0.5 — this communication meets the quality threshold

Write a plain-English assessment (4–5 sentences) that explains:
1. What these scores mean for the communication's effectiveness
2. The single most important improvement this writer should make
3. Why the ML scoring adds something the LLM analysis alone could not (calibrated,
   consistent, comparable across many communications)

Do not repeat the raw numbers verbatim — interpret them.
