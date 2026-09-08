#!/usr/bin/env python3
import json, sys

data   = json.load(sys.stdin)
answer = data.get("output", "").lower()

MONTHS = [
    "january", "february", "march", "april", "may", "june",
    "july", "august", "september", "october", "november", "december"
]

months_with_x = [m for m in MONTHS if "x" in m]  # -> []

# Check if the answer names a month that actually contains X
named_correctly = any(m in answer for m in months_with_x)

# Check if the answer correctly says there are none
says_none = any(p in answer for p in ["no month", "none", "trick", "no english month", "no standard"])

if months_with_x:
    status = "pass" if named_correctly else "fail"
    reason = f"Correct months with X: {months_with_x}. Answer named: none of them." if not named_correctly else None
else:
    # No month contains X — the correct answer is "none"
    status = "pass" if says_none else "fail"
    reason = f"No month name contains the letter X. Correct answer: none. Got: {data.get('output', '')[:120]}"

verdict = data.get("output", "") if status == "pass" else status
print(json.dumps({"verdict": verdict, "status": status, "reason": reason}))
