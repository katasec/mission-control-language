#!/usr/bin/env python3
"""
ControlChecker — scans a YAML/JSON config for compliance control gaps.

Reads JSON from stdin ({"input": "<path>"}), writes JSON to stdout.

Checks four control domains and scores each 0.0–1.0:
  encryption_score    — TLS, at-rest encryption, key management
  access_score        — MFA, RBAC, least-privilege, service accounts
  logging_score       — audit logging, log retention, SIEM integration
  patch_score         — dependency versions, CVE mentions, update policy

A score of 1.0 means all controls present; 0.0 means all absent.
"""
import sys
import json
import re
import os

def load_config(path: str) -> str:
    # Try the path as-is, then relative to the mission root (two levels up from this script).
    candidates = [
        path,
        os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), path),
    ]
    for p in candidates:
        try:
            return open(p).read().lower()
        except FileNotFoundError:
            continue
    return ""

def score_encryption(text: str) -> float:
    signals = [
        bool(re.search(r'\btls\b|\bssl\b|\bhttps\b', text)),
        bool(re.search(r'encrypt.{0,20}(at.rest|storage|disk)', text)),
        bool(re.search(r'\bkms\b|\bkey.manag', text)),
        bool(re.search(r'encrypt.{0,10}transit', text)),
    ]
    return round(sum(signals) / len(signals), 2)

def score_access(text: str) -> float:
    signals = [
        bool(re.search(r'\bmfa\b|\b2fa\b|\bmulti.factor', text)),
        bool(re.search(r'\brbac\b|\brole.based', text)),
        bool(re.search(r'least.privile', text)),
        bool(re.search(r'service.account|iam.role', text)),
    ]
    return round(sum(signals) / len(signals), 2)

def score_logging(text: str) -> float:
    signals = [
        bool(re.search(r'audit.log|access.log', text)),
        bool(re.search(r'log.retain|retention', text)),
        bool(re.search(r'\bsiem\b|\bsplunk\b|\belastic\b|\bcloudwatch\b', text)),
        bool(re.search(r'monitor|alert', text)),
    ]
    return round(sum(signals) / len(signals), 2)

def score_patching(text: str) -> float:
    signals = [
        bool(re.search(r'patch|update.polic', text)),
        bool(re.search(r'dependabot|renovate|snyk', text)),
        not bool(re.search(r'\bcve-\d{4}', text)),   # CVE mentions = gap
        bool(re.search(r'version.polic|pin.version', text)),
    ]
    return round(sum(signals) / len(signals), 2)

if __name__ == "__main__":
    data = json.load(sys.stdin)
    path = data.get("input", "")
    text = load_config(path)

    enc   = score_encryption(text)
    acc   = score_access(text)
    log   = score_logging(text)
    patch = score_patching(text)

    print(json.dumps({
        "controls": {
            "encryption_score": enc,
            "access_score":     acc,
            "logging_score":    log,
            "patch_score":      patch,
            "config_path":      path,
            "controls_found":   round((enc + acc + log + patch) / 4, 2),
        }
    }))
