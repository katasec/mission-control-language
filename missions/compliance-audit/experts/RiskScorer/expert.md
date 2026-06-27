---
name: RiskScorer
kind: onnx
model: ../../models/compliance-risk.onnx
inputs: [encryption_score, access_score, logging_score, patch_score]
outputKey: risk_score
threshold: 0.6
input: Numeric compliance control scores
output: risk_score written to context bag; >= 0.6 triggers high-risk path
inputKeys:
  encryption_score: float
  access_score: float
  logging_score: float
  patch_score: float
outputKeys:
  risk_score: float
---
