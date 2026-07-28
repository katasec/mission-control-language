#!/usr/bin/env python3
import json, subprocess, sys, tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
WORKSPACE = ROOT / ".workspaces" / "code-review"

def patch_from(text):
    fenced = text.split("```")
    candidates = [block for block in fenced if "diff --git " in block or ("--- " in block and "+++ " in block)]
    source = candidates[0] if candidates else text
    source = "\n".join(
        line for line in source.splitlines()
        if line not in ("*** Begin Patch", "*** End Patch")
        and not line.startswith("*** Update File:")
    )
    start = source.find("diff --git ")
    if start < 0 and "--- " in source and "+++ " in source:
        start = source.find("--- ")
    if start >= 0:
        patch = source[start:]
        lines = patch.rstrip().splitlines()
        in_hunk = False
        normalized = []
        for line in lines:
            if line.startswith("*** End of File"):
                continue
            if line.startswith("diff --git "):
                in_hunk = False
            elif line.startswith(("index ", "--- ", "+++ ")):
                in_hunk = False
            elif line.startswith("@@"):
                in_hunk = True
            elif in_hunk and line and not line.startswith((" ", "+", "-", "\\")):
                line = " " + line
            normalized.append(line)
        return "\n".join(normalized) + "\n"
    raise ValueError("LLMReview did not return a unified diff")

def main():
    review = json.load(sys.stdin).get("output", "")
    patch = patch_from(review)
    with tempfile.NamedTemporaryFile("w", suffix=".patch", delete=False) as handle:
        handle.write(patch)
        patch_file = Path(handle.name)
    result = subprocess.run(["git", "apply", "--recount", "--check", str(patch_file)], cwd=WORKSPACE, text=True, capture_output=True)
    if result.returncode == 0:
        result = subprocess.run(["git", "apply", "--recount", str(patch_file)], cwd=WORKSPACE, text=True, capture_output=True)
    patch_file.unlink(missing_ok=True)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or "git apply failed")
    print(json.dumps({"apply_result": patch}))

if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(json.dumps({"apply_result": "", "status": "fail", "reason": str(error)}))
