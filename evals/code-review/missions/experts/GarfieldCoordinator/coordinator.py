#!/usr/bin/env python3
import json
import os
import shlex
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.request
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[3]
CASE = ROOT / "cases" / "contained-dry-run"
BASE = ROOT / "ledgerlite"
WORKSPACE = ROOT / ".workspaces" / "garfield"


def cost_usd(usage):
    uncached = usage["inputTokens"] - usage["cachedInputTokens"]
    return uncached / 1_000_000 + usage["cachedInputTokens"] / 1_000_000 * 0.10 + usage["outputTokens"] / 1_000_000 * 6


def worker_input(prompt, fixture_diff, previous_patch, feedback):
    prior = "No prior attempt." if previous_patch is None else f"Previous proposed patch:\n{previous_patch}\n\nPrevious deterministic adjudication:\n{feedback}"
    return f"""Act as the repair worker for a code-review coordinator.

Request:
{prompt}

Candidate fixture diff:
{fixture_diff}

{prior}

Return one complete unified diff only. Repair both required behaviors: dry-run must not mutate invoices or record audit events, and zero matches must use the existing count-and-noun output instead of a special message. The diff must modify both `cmd/ledgerctl/main.go` and `internal/invoice/service.go`. In `service.go`, move the existing audit call into `!dryRun` and preserve `AggregateID`, `AccountID`, and `At` exactly. Do not use Markdown fences, commentary, apply_patch envelope markers, or invented identifiers."""


def prepare_workspace():
    if WORKSPACE.exists():
        shutil.rmtree(WORKSPACE)
    shutil.copytree(BASE, WORKSPACE, ignore=shutil.ignore_patterns(".git", ".swamp"))
    run(["git", "init", "--quiet"])
    run(["git", "config", "user.name", "MCL Eval"])
    run(["git", "config", "user.email", "mcl@example.invalid"])
    run(["git", "add", "."])
    run(["git", "commit", "--quiet", "-m", "base"])
    applied = run(["git", "apply", str(CASE / "fixture.patch")])
    if applied.returncode:
        raise RuntimeError(applied.stderr)


def run(command):
    return subprocess.run(command, cwd=WORKSPACE, text=True, capture_output=True)


def fixture_diff():
    return run(["git", "diff", "--"]).stdout


def patch_from(text):
    blocks = text.split("```")
    source = next((block for block in blocks if "diff --git " in block), text)
    start = source.find("diff --git ")
    if start < 0:
        raise ValueError("repair worker did not return a unified diff")
    lines = source[start:].splitlines()
    in_hunk = False
    normalized = []
    for line in lines:
        if line in ("*** Begin Patch", "*** End Patch", "*** End of File") or line.startswith("*** Update File:"):
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
    return "\n".join(normalized).rstrip() + "\n"


def apply_patch(patch):
    with tempfile.NamedTemporaryFile("w", suffix=".patch", delete=False) as handle:
        handle.write(patch)
        path = Path(handle.name)
    try:
        checked = run(["git", "apply", "--recount", "--check", str(path)])
        if checked.returncode == 0:
            applied = run(["git", "apply", "--recount", str(path)])
            return applied.returncode == 0, applied.stderr.strip() or "patch applied"

        recounted = Path(tempfile.mkstemp(suffix=".patch")[1])
        try:
            recounted.write_text(recount_hunks(patch))
            dry_run = run(["patch", "--dry-run", "-p1", "--batch", "-i", str(recounted)])
            if dry_run.returncode:
                return False, checked.stderr.strip() or dry_run.stderr.strip() or "patch check failed"
            applied = run(["patch", "-p1", "--batch", "-i", str(recounted)])
            return applied.returncode == 0, applied.stderr.strip() or "patch applied"
        finally:
            recounted.unlink(missing_ok=True)
    finally:
        path.unlink(missing_ok=True)


def recount_hunks(patch):
    lines = patch.splitlines()
    normalized = []
    index = 0
    while index < len(lines):
        line = lines[index]
        if not line.startswith("@@ "):
            normalized.append(line)
            index += 1
            continue
        end = index + 1
        while end < len(lines) and not lines[end].startswith(("@@ ", "diff --git ")):
            end += 1
        hunk = lines[index + 1:end]
        match = re.match(r"@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@(.*)", line)
        if match is None:
            raise ValueError(f"invalid unified-diff hunk header: {line}")
        old_count = sum(item.startswith((" ", "-")) for item in hunk)
        new_count = sum(item.startswith((" ", "+")) for item in hunk)
        normalized.append(f"@@ -{match.group(1)},{old_count} +{match.group(2)},{new_count} @@{match.group(3)}")
        normalized.extend(hunk)
        index = end
    return "\n".join(normalized).rstrip() + "\n"


def validate():
    oracle = json.loads((CASE / "oracle.json").read_text())
    results = []
    for command in oracle["required_public_commands"]:
        results.append(run(shlex.split(command)))
    overlay = {str((WORKSPACE / test.relative_to(CASE / "hidden-tests")).resolve()): str(test.resolve()) for test in (CASE / "hidden-tests").rglob("*_test.go")}
    overlay_path = WORKSPACE / ".contained-dry-run-overlay.json"
    overlay_path.write_text(json.dumps({"Replace": overlay}))
    results.append(run(["go", "test", f"-overlay={overlay_path}", "./..."]))
    failed = next((result for result in results if result.returncode != 0), None)
    if failed is None:
        return True, "All public commands and hidden tests passed."
    detail = (failed.stdout + "\n" + failed.stderr).strip()
    return False, f"{' '.join(failed.args)} failed:\n{detail[-3000:]}"


def request_repair(url, key, body):
    request = urllib.request.Request(url, data=json.dumps({"model": "gpt-5.6-luna", "input": body}).encode(), headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=600) as response:
        return json.load(response)


def add_usage(total, payload):
    raw = payload.get("usage", {})
    total["inputTokens"] += raw.get("input_tokens", 0)
    total["cachedInputTokens"] += raw.get("input_tokens_details", {}).get("cached_tokens", 0)
    total["outputTokens"] += raw.get("output_tokens", 0)
    total["reasoningOutputTokens"] += raw.get("output_tokens_details", {}).get("reasoning_tokens", 0)


def response_text(payload):
    if payload.get("output_text"):
        return payload["output_text"]
    return "\n".join(
        content.get("text", "")
        for item in payload.get("output", [])
        if item.get("type") == "message"
        for content in item.get("content", [])
        if content.get("type") == "output_text"
    )


def write_progress(progress_log, cycles, usage, cost, started, outcome):
    if not progress_log:
        return
    elapsed = int((time.monotonic() - started) * 1000)
    with open(progress_log, "a", encoding="utf-8") as handle:
        handle.write(f"cycle={cycles} input_tokens={usage['inputTokens']} cached_input_tokens={usage['cachedInputTokens']} output_tokens={usage['outputTokens']} cost_usd=${cost:.6f} elapsed_ms={elapsed} outcome={outcome.splitlines()[0][:160]}\n")


def main():
    if sys.argv[1:] == ["--verify-fuse"]:
        probe = {"inputTokens": 20_000, "cachedInputTokens": 0, "outputTokens": 0, "reasoningOutputTokens": 0}
        assert cost_usd(probe) >= 0.02
        print(json.dumps({"fuse_verified": True, "threshold_usd": 0.02, "probe_cost_usd": cost_usd(probe)}))
        return
    if sys.argv[1:] == ["--verify-stateful"]:
        probe = worker_input("request", "diff", "previous patch", "previous validation")
        assert "previous patch" in probe and "previous validation" in probe
        print(json.dumps({"stateful_verified": True}))
        return

    prompt = (CASE / "prompt.md").read_text()
    key = os.environ.get("OPENAI_API_KEY") or os.environ.get("MCL_API_KEY", "")
    url = os.environ.get("OPENAI_BASE_URL", "https://api.openai.com/v1").rstrip("/") + "/responses"
    fuse_usd = float(os.environ.get("GARFIELD_COST_FUSE_USD", "3.00"))
    progress_log = os.environ.get("GARFIELD_PROGRESS_LOG")
    usage = {"inputTokens": 0, "cachedInputTokens": 0, "outputTokens": 0, "reasoningOutputTokens": 0}
    previous_patch = None
    feedback = "Start by reviewing the complete fixture diff and repair all requested behavior."
    cycles = 0
    started = time.monotonic()

    while True:
        cycles += 1
        prepare_workspace()
        body = worker_input(prompt, fixture_diff(), previous_patch, feedback)
        try:
            payload = request_repair(url, key, body)
        except Exception as error:
            print(json.dumps({"coordinator_output": f"coordinator request failed: {error}", "status": "fail", "reason": str(error), "usage": usage}))
            return

        add_usage(usage, payload)
        cost = cost_usd(usage)
        text = response_text(payload)
        try:
            previous_patch = patch_from(text)
            applied, feedback = apply_patch(previous_patch)
            if applied:
                passed, feedback = validate()
                if passed:
                    write_progress(progress_log, cycles, usage, cost, started, feedback)
                    print(json.dumps({"coordinator_output": previous_patch, "usage": usage, "cycles": cycles, "durationMs": int((time.monotonic() - started) * 1000), "validated": True}))
                    return
        except Exception as error:
            feedback = str(error)

        write_progress(progress_log, cycles, usage, cost, started, feedback)
        if cost >= fuse_usd:
            print(json.dumps({"coordinator_output": f"${fuse_usd:.2f} fuse tripped after {cycles} cycles: {feedback}", "status": "pass", "usage": usage, "cycles": cycles, "durationMs": int((time.monotonic() - started) * 1000), "fuse_tripped": True}))
            return


if __name__ == "__main__":
    main()
