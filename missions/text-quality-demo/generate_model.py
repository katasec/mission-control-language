"""
Generate quality-scorer.onnx for the text-quality-demo mission.

The model is a logistic regression trained on synthetic text quality data.
Features: word_count, avg_sentence_length, vocabulary_richness
Label:    0 = low quality, 1 = high quality (inverted: score > 0.5 => fail)

The OnnxExpertRunner reads three named floats from the context bag and runs
this model. The output probability for class 1 is stored as `quality_score`.
Scores above the threshold (0.5) cause the QualityScorer step to fail.

Usage:
    pip install scikit-learn skl2onnx
    python generate_model.py
"""

import os
import numpy as np
from sklearn.linear_model import LogisticRegression
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType

# Synthetic training data: [word_count, avg_sentence_length, vocabulary_richness]
# Label 0 = high quality (low anomaly score), 1 = low quality (high anomaly score).
# The model outputs a probability — we treat it as a badness score.
X = np.array([
    # high quality (label 0): rich vocab, moderate sentence length, decent word count
    [200, 15.0, 0.85],
    [150, 12.5, 0.80],
    [300, 14.0, 0.78],
    [250, 16.0, 0.82],
    [180, 13.5, 0.88],
    [220, 11.0, 0.76],
    [170, 14.5, 0.84],
    [280, 15.5, 0.79],
    # low quality (label 1): low vocab, very long or very short sentences, few words
    [ 30,  3.0, 0.30],
    [ 20,  2.0, 0.25],
    [ 50, 40.0, 0.20],
    [ 10,  1.0, 0.10],
    [ 40, 35.0, 0.35],
    [ 25,  2.5, 0.28],
    [ 60, 45.0, 0.22],
    [ 15,  1.5, 0.15],
], dtype=np.float32)

y = np.array([0, 0, 0, 0, 0, 0, 0, 0,
              1, 1, 1, 1, 1, 1, 1, 1], dtype=np.int64)

model = LogisticRegression(max_iter=1000)
model.fit(X, y)

initial_type = [("input", FloatTensorType([None, 3]))]
onnx_model = convert_sklearn(model, initial_types=initial_type,
                             options={type(model): {'zipmap': False}})

os.makedirs("models", exist_ok=True)
output_path = os.path.join("models", "quality-scorer.onnx")
with open(output_path, "wb") as f:
    f.write(onnx_model.SerializeToString())

print(f"Model written to {output_path}")
print(f"Training accuracy: {model.score(X, y):.2f}")

# Quick sanity check
sample = np.array([[200, 14.0, 0.82]], dtype=np.float32)
prob = model.predict_proba(sample)[0][1]
print(f"High-quality sample score (should be < 0.5): {prob:.4f}")

sample_bad = np.array([[20, 2.0, 0.20]], dtype=np.float32)
prob_bad = model.predict_proba(sample_bad)[0][1]
print(f"Low-quality sample score (should be > 0.5):  {prob_bad:.4f}")
