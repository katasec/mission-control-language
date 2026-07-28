#!/usr/bin/env python3
import json
import os
import shlex
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CASE = ROOT / "cases" / "contained-dry-run"
WORKSPACE = ROOT / ".workspaces" / "code-review"


def run(command):
    completed = subprocess.run(command, cwd=WORKSPACE, text=True, capture_output=True)
    return {
        "command": command,
        "exit_code": completed.returncode,
        "stdout": completed.stdout,
        "stderr": completed.stderr,
    }


def hidden_overlay():
    replacements = {}
    for test in (CASE / "hidden-tests").rglob("*_test.go"):
        target = WORKSPACE / test.relative_to(CASE / "hidden-tests")
        replacements[str(target.resolve())] = str(test.resolve())
    return replacements


def changed_path_result(oracle):
    paths = subprocess.run(["git", "diff", "--name-only"], cwd=WORKSPACE, text=True, capture_output=True).stdout.splitlines()
    allowed = tuple(oracle["allowed_path_prefixes"])
    violations = [path for path in paths if not path.startswith(allowed)]
    if len(paths) > oracle["max_changed_files"]:
        return f"changed {len(paths)} files, exceeding oracle limit {oracle['max_changed_files']}"
    if violations:
        return f"changed paths outside oracle allowlist: {', '.join(violations)}"

    numstat = subprocess.run(["git", "diff", "--numstat"], cwd=WORKSPACE, text=True, capture_output=True).stdout.splitlines()
    lines = sum(int(part) for row in numstat for part in row.split("\t")[:2] if part.isdigit())
    if lines > oracle["max_changed_lines"]:
        return f"changed {lines} lines, exceeding oracle limit {oracle['max_changed_lines']}"
    return None


def failure_reason(results):
    failed = next(result for result in results if result["exit_code"] != 0)
    detail = (failed["stdout"] + "\n" + failed["stderr"]).strip()
    return f"{' '.join(failed['command'])} failed:\n{detail[-3000:]}"


def write_grade(grade):
    path = os.environ.get("CODE_REVIEW_VALIDATION_FILE")
    if path:
        Path(path).write_text(json.dumps(grade, indent=2))


def main():
    oracle = json.loads((CASE / "oracle.json").read_text())
    path_failure = changed_path_result(oracle)
    if path_failure:
        grade = {"passed": False, "commands": [], "reason": path_failure}
        write_grade(grade)
        print(json.dumps({"validation_result": path_failure, "status": "fail", "reason": path_failure}))
        return

    results = [run(shlex.split(command)) for command in oracle["required_public_commands"]]
    overlay_path = WORKSPACE / ".contained-dry-run-overlay.json"
    overlay_path.write_text(json.dumps({"Replace": hidden_overlay()}))
    results.append(run(["go", "test", f"-overlay={overlay_path}", "./..."]))
    grade = {"passed": all(result["exit_code"] == 0 for result in results), "commands": results}
    write_grade(grade)

    if grade["passed"]:
        print(json.dumps({"validation_result": "All public commands and hidden tests passed.", "status": "pass"}))
        return

    reason = failure_reason(results)
    print(json.dumps({"validation_result": reason, "status": "fail", "reason": reason}))


if __name__ == "__main__":
    main()
