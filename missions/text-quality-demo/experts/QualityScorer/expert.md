---
name: QualityScorer
input: Numeric text features (word_count, avg_sentence_length, vocabulary_richness)
output: quality_score written to context bag; pass if score <= 0.5, fail otherwise
kind: onnx
model: ../../models/quality-scorer.onnx
inputs: [word_count, avg_sentence_length, vocabulary_richness]
outputKey: quality_score
threshold: 0.5
inputKeys:
  word_count: int
  avg_sentence_length: float
  vocabulary_richness: float
outputKeys:
  quality_score: float
---
