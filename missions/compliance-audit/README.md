# compliance-audit

Proves the full MCL capability set introduced in v0.7.0 working end-to-end:

| Capability | Expert |
|---|---|
| `kind:exec` — process execution | `ControlChecker` runs `audit.py` against a config file |
| `kind:json_extract` — unpack JSON | `ExtractControls` puts four scores into the context bag |
| `kind:onnx` — ML scoring | `RiskScorer` produces a `risk_score` from the four features |
| `when()` numeric routing | `>= 0.6` → `HighRiskAnalyst`, `< 0.6` → `LowRiskSummary` |
| Typed context contracts | All experts annotated with `inputKeys`/`outputKeys` |
| `forge validate` MCL011/MCL012 | Validates the full data contract chain before run |

## Setup

```bash
pip install scikit-learn skl2onnx
python generate_model.py
forge init
```

## Usage

```bash
# High-risk path (missing controls → risk_score >= 0.6 → HighRiskAnalyst)
forge run --mission ComplianceAudit --input sample-weak.yaml

# Low-risk path (controls present → risk_score < 0.6 → LowRiskSummary)
forge run --mission ComplianceAudit --input sample-strong.yaml
```

## What the audit checks

`audit.py` scans a YAML/JSON config file for four control domains:

- **Encryption** — TLS, at-rest encryption, KMS key references
- **Access** — MFA, RBAC, least-privilege, service accounts
- **Logging** — audit logs, retention policy, SIEM integration
- **Patching** — dependency update tooling, CVE absence, version policy

Each domain scores 0.0–1.0. The ONNX model combines them into a single
`risk_score`; above 0.6 routes to detailed gap analysis, below 0.6 to a
passing summary.
