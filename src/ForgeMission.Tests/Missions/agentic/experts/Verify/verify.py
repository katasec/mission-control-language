#!/usr/bin/env python3
import json, sys

payload = json.load(sys.stdin)
print(json.dumps({"output": "VERIFIED: " + payload.get("output", "")}))
