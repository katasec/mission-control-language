---
name: ExtractFeatures
input: JSON object with float features
output: Individual context bag entries (word_count, avg_sentence_length, vocabulary_richness)
kind: json_extract
inputKeys:
  word_count: int
  avg_sentence_length: float
  vocabulary_richness: float
outputKeys:
  word_count: int
  avg_sentence_length: float
  vocabulary_richness: float
---
