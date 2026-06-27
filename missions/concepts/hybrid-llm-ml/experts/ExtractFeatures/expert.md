---
name: ExtractFeatures
input: JSON object with float quality signal features
output: Individual context bag entries (specificity_score, completeness_score, risk_signal)
kind: json_extract
inputKeys:
  specificity_score: float
  completeness_score: float
  risk_signal: float
outputKeys:
  specificity_score: float
  completeness_score: float
  risk_signal: float
---
