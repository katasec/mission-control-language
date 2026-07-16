#!/usr/bin/env python3
import json, os, sys

payload = json.load(sys.stdin)

counter = os.environ.get("FORGE_ENRICH_COUNTER")
if counter:
    with open(counter, "a") as fh:
        fh.write("enriched\n")

print(json.dumps({"output": payload.get("goal", "")}))
