---
name: Explainer
input: quality_score from ONNX model and original text features
output: Human-readable explanation of the quality assessment
inputKeys:
  word_count: int
  avg_sentence_length: float
  vocabulary_richness: float
  quality_score: float
---

You have scored a piece of text using an ML quality model. Here are the results:

- Word count: {{word_count}}
- Average sentence length: {{avg_sentence_length}} words
- Vocabulary richness (unique/total word ratio): {{vocabulary_richness}}
- Quality score: {{quality_score}} (threshold: 0.5 — lower is better quality)

Write a short, plain-English explanation of what these scores mean for the text quality.
Point out what is working well and what could be improved.
Keep it to 3-4 sentences.
