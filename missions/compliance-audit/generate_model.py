"""
Generate compliance-risk.onnx for the compliance-audit mission.

The model is a logistic regression trained on synthetic compliance data.
Features: encryption_score, access_score, logging_score, patch_score (all 0.0–1.0)
Label:    1 = high risk, 0 = low risk

The OnnxExpertRunner reads the four named floats from the context bag and
outputs risk_score (probability of class 1). Scores >= 0.6 route to
HighRiskAnalyst; scores < 0.6 route to LowRiskSummary.

Usage:
    pip install scikit-learn skl2onnx
    python generate_model.py
"""

import os
import numpy as np
from sklearn.linear_model import LogisticRegression
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType

# Synthetic training data: [encryption, access, logging, patch]
# High risk (label 1): multiple weak controls
# Low risk  (label 0): most controls present
X = np.array([
    # low risk — well-controlled configs
    [0.9, 0.9, 0.8, 0.8],
    [1.0, 1.0, 1.0, 1.0],
    [0.8, 0.8, 0.9, 0.9],
    [0.7, 0.9, 0.8, 0.8],
    [0.9, 0.7, 0.9, 0.8],
    [0.8, 0.8, 0.8, 0.7],
    [0.9, 0.9, 0.7, 0.9],
    [1.0, 0.8, 0.8, 1.0],
    # high risk — significant gaps
    [0.0, 0.0, 0.0, 0.0],
    [0.2, 0.1, 0.0, 0.3],
    [0.0, 0.5, 0.2, 0.0],
    [0.3, 0.0, 0.1, 0.2],
    [0.1, 0.2, 0.0, 0.1],
    [0.5, 0.0, 0.0, 0.5],
    [0.2, 0.3, 0.1, 0.0],
    [0.0, 0.4, 0.3, 0.1],
], dtype=np.float32)

y = np.array([0, 0, 0, 0, 0, 0, 0, 0,
              1, 1, 1, 1, 1, 1, 1, 1], dtype=np.int64)

model = LogisticRegression(max_iter=1000)
model.fit(X, y)

from skl2onnx.common.data_types import FloatTensorType
from skl2onnx import to_onnx

initial_type = [("input", FloatTensorType([None, 4]))]
onnx_model = convert_sklearn(model, initial_types=initial_type,
                             options={type(model): {'zipmap': False}})

os.makedirs("models", exist_ok=True)
output_path = os.path.join("models", "compliance-risk.onnx")
with open(output_path, "wb") as f:
    f.write(onnx_model.SerializeToString())

print(f"Model written to {output_path}")
print(f"Training accuracy: {model.score(X, y):.2f}")

# Sanity checks
good = np.array([[0.9, 0.9, 0.8, 0.8]], dtype=np.float32)
bad  = np.array([[0.1, 0.0, 0.0, 0.2]], dtype=np.float32)
print(f"Low-risk sample  (should be < 0.6): {model.predict_proba(good)[0][1]:.4f}")
print(f"High-risk sample (should be >= 0.6): {model.predict_proba(bad)[0][1]:.4f}")
