"""
Generate quality-signal-scorer.onnx for the hybrid-llm-ml concept mission.

The model is a logistic regression trained on synthetic professional communication
quality data. Features extracted by the LLM Analyst:

  specificity_score    — how concrete and specific the content is (0.0–1.0)
  completeness_score   — how self-contained the information is (0.0–1.0)
  risk_signal          — how much unresolved uncertainty is present (0.0–1.0)

Label: 1 = high quality communication, 0 = low quality communication.
Score interpretation: higher = better communication quality.
Threshold: 0.5 — scores above pass, scores below fail.

Usage:
    pip install scikit-learn skl2onnx numpy
    python generate_model.py
"""

import os
import numpy as np
from sklearn.linear_model import LogisticRegression
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType

# Synthetic training data: [specificity_score, completeness_score, risk_signal]
# Label 1 = high quality (specific, complete, low risk signal)
# Label 0 = low quality (vague, incomplete, high risk signal)
X = np.array([
    # High quality: specific, complete, low risk
    [0.85, 0.90, 0.10],
    [0.80, 0.85, 0.15],
    [0.90, 0.80, 0.05],
    [0.75, 0.88, 0.20],
    [0.82, 0.92, 0.08],
    [0.78, 0.82, 0.12],
    [0.88, 0.86, 0.10],
    [0.83, 0.89, 0.07],
    # Low quality: vague, incomplete, high risk signal
    [0.25, 0.30, 0.80],
    [0.20, 0.25, 0.85],
    [0.30, 0.20, 0.75],
    [0.15, 0.35, 0.90],
    [0.28, 0.22, 0.78],
    [0.18, 0.28, 0.88],
    [0.32, 0.18, 0.72],
    [0.22, 0.32, 0.82],
], dtype=np.float32)

y = np.array([1, 1, 1, 1, 1, 1, 1, 1,
              0, 0, 0, 0, 0, 0, 0, 0], dtype=np.int64)

model = LogisticRegression(max_iter=1000)
model.fit(X, y)

initial_type = [("input", FloatTensorType([None, 3]))]
onnx_model = convert_sklearn(model, initial_types=initial_type,
                             options={type(model): {'zipmap': False}})

os.makedirs("models", exist_ok=True)
output_path = os.path.join("models", "quality-signal-scorer.onnx")
with open(output_path, "wb") as f:
    f.write(onnx_model.SerializeToString())

print(f"Model written to {output_path}")
print(f"Training accuracy: {model.score(X, y):.2f}")

# Sanity checks
high_quality = np.array([[0.80, 0.85, 0.10]], dtype=np.float32)
prob_hq = model.predict_proba(high_quality)[0][1]
print(f"High-quality sample score (should be > 0.5): {prob_hq:.4f}")

low_quality = np.array([[0.20, 0.25, 0.85]], dtype=np.float32)
prob_lq = model.predict_proba(low_quality)[0][1]
print(f"Low-quality sample score (should be < 0.5):  {prob_lq:.4f}")
