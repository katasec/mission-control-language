---
name: FeatureExtractor
input: A passage of text
output: JSON object with numeric quality features
outputKeys:
  word_count: int
  avg_sentence_length: float
  vocabulary_richness: float
---

Analyse the following text and output ONLY a valid JSON object with exactly these keys:

- word_count: total number of words (integer)
- avg_sentence_length: average number of words per sentence (float, one decimal place)
- vocabulary_richness: ratio of unique words to total words, between 0.0 and 1.0 (float)

Output ONLY the JSON object. No prose, no markdown, no code fences. Example:
{"word_count": 42, "avg_sentence_length": 14.0, "vocabulary_richness": 0.81}

Text to analyse:
{{text}}
