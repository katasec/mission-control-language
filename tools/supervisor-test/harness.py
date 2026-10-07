#!/usr/bin/env python3
"""Run one shared design request through native Codex or an explicit MCL pipeline."""

import hashlib
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timezone
from uuid import uuid4

ROOT = Path(__file__).resolve().parent
STAGES = {
    "SupervisorDesign": ("design", "supervisor", "design"),
    "SimplicityReview": ("simplicity_review", "simplicity", "review"),
    "OwnershipReview": ("ownership_review", "ownership", "review"),
    "SupervisorDecision": ("final", "supervisor", "final"),
}


def main():
    if len(sys.argv) == 3 and sys.argv[1] == "stage":
        return run_stage(sys.argv[2], json.load(sys.stdin))
    if len(sys.argv) == 2 and sys.argv[1] in ("codex", "mcl"):
        return run_experiment(sys.argv[1])
    raise ValueError("Usage: harness.py codex|mcl, or stage <expert-name> with JSON stdin")


def run_experiment(engine):
    run_dir = ROOT / "results" / engine / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid4().hex[:8])
    run_dir.mkdir(parents=True)
    record = {"engine": engine, "state": "running", "started_at": utc_now()}
    started = time.monotonic()
    print(f"Results: {run_dir}", flush=True)
    write_json(run_dir / "run.json", record)
    try:
        snapshot_inputs(run_dir, engine)
        with tempfile.TemporaryDirectory(prefix="mcl-supervisor-test-") as work_dir:
            if engine == "codex":
                run_codex_workflow(run_dir, work_dir)
            else:
                run_mcl_workflow(run_dir, work_dir)
        record["state"] = "completed"
        print(f"Decision: {read_json(run_dir / 'final.json')['decision']}")
        return 0
    except (OSError, ValueError, RuntimeError, KeyError) as error:
        record.update(state="failed", error=str(error))
        print(f"Experiment failed: {error}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        record.update(state="interrupted", error="Interrupted by operator")
        return 130
    finally:
        record.update(ended_at=utc_now(), elapsed_seconds=round(time.monotonic() - started, 3))
        write_json(run_dir / "run.json", record)


def run_codex_workflow(run_dir, work_dir):
    prompt = shared_context(run_dir) + "\n\nRun the complete native Codex workflow now.\n"
    prompt += "\n\n".join(f"## {name} persona\n{path.read_text()}" for name, path in persona_paths(run_dir).items())
    answer, _ = call_codex(run_dir, work_dir, "workflow", prompt, "workflow", allow_agents=True)
    for key in ("design", "simplicity_review", "ownership_review", "final"):
        save_artifact(run_dir, key, answer[key])


def run_mcl_workflow(run_dir, work_dir):
    mission_dir = run_dir / "mcl"
    run_process(["forge", "init"], mission_dir, run_dir, "forge-init", timeout=30)
    run_process(["forge", "validate"], mission_dir, run_dir, "forge-validate", timeout=30)
    output = run_process(
        ["forge", "run", "--var", f"run_dir={run_dir}", "--var", f"work_dir={work_dir}"],
        mission_dir, run_dir, "forge-run", timeout=2700,
    )
    final = json.loads(output)
    validate_schema(final, read_json(run_dir / "schemas.json")["final"])
    if final != read_json(run_dir / "final.json"):
        raise ValueError("forge output differs from the final stage artifact")


def run_stage(name, context):
    key, persona, schema_name = STAGES[name]
    run_dir = Path(context["run_dir"])
    artifacts = {}
    if name != "SupervisorDesign":
        artifacts["design"] = context["design"]
    if name == "SupervisorDecision":
        artifacts.update({key: json.loads(context[key]) for key in ("simplicity_review", "ownership_review")})
    prompt = shared_context(run_dir) + f"\n\nAssigned MCL stage: {name}. Perform only this stage.\n"
    prompt += f"\n## Persona\n{persona_paths(run_dir)[persona].read_text()}\n"
    prompt += "\n## Supplied artifacts\n" + json.dumps(artifacts, ensure_ascii=False)
    session = None
    if name == "SupervisorDecision":
        session = read_json(run_dir / "stages" / "design.metadata.json")["session_id"]
    answer, _ = call_codex(run_dir, context["work_dir"], key, prompt, schema_name, session=session)
    value = answer["design"] if name == "SupervisorDesign" else answer
    save_artifact(run_dir, key, value)
    print(json.dumps({key: value}, ensure_ascii=False))
    return 0


def call_codex(run_dir, work_dir, label, prompt, schema_name, session=None, allow_agents=False):
    stages = run_dir / "stages"
    stages.mkdir(exist_ok=True)
    schema = read_json(run_dir / "schemas.json")[schema_name]
    schema_path = stages / f"{label}.schema.json"
    answer_path = stages / f"{label}.answer.json"
    write_json(schema_path, schema)
    (stages / f"{label}.prompt.md").write_text(prompt, encoding="utf-8")
    command = codex_command(read_json(run_dir / "settings.json"), work_dir, schema_path, answer_path, session, allow_agents)
    events_text = run_process(command, work_dir, stages, label, prompt, timeout=660)
    events = [json.loads(line) for line in events_text.splitlines() if line.strip()]
    session_ids = [event["thread_id"] for event in events if event.get("type") == "thread.started"]
    session_id = session_ids[0] if session_ids else session
    if not session_id:
        raise ValueError(f"{label}: Codex returned no session identifier")
    answer = read_json(answer_path)
    validate_schema(answer, schema)
    write_json(stages / f"{label}.metadata.json", {
        "session_id": session_id,
        "usage": [event.get("usage") for event in events if event.get("type") == "turn.completed"],
        "usage_scope": "CLI-emitted turn usage; native subagent totals are not assumed",
    })
    return answer, session_id


def codex_command(settings, work_dir, schema_path, answer_path, session, allow_agents):
    command = ["codex", "exec"]
    if session:
        command += ["resume", session]
    else:
        command += ["--cd", str(work_dir), "--sandbox", "read-only"]
    return command + [
        "--ignore-user-config", "--skip-git-repo-check", "--json",
        "--model", settings["model"],
        "-c", "model_reasoning_effort=" + json.dumps(settings["reasoning_effort"]),
        "-c", "sandbox_mode=\"read-only\"", "-c", "approval_policy=\"never\"",
        "-c", "agents.enabled=" + str(allow_agents).lower(),
        "--output-schema", str(schema_path), "--output-last-message", str(answer_path), "-",
    ]


def shared_context(run_dir):
    return "## Workflow\n" + (run_dir / "workflow.md").read_text() + "\n## Build request\n" + (run_dir / "input.md").read_text()


def snapshot_inputs(run_dir, engine):
    for name in ("settings.json", "schemas.json", "workflow.md"):
        shutil.copyfile(ROOT / name, run_dir / name)
    shutil.copyfile(ROOT / "prompt.md", run_dir / "input.md")
    if not (run_dir / "input.md").read_text().strip():
        raise ValueError("prompt.md is empty")
    shutil.copytree(ROOT / "personas", run_dir / "personas")
    # Copy the mission and adapter together: each expert's relative argv stays valid, and init
    # writes its lock inside this run rather than mutating the checked-in experiment.
    shutil.copytree(ROOT / "mcl", run_dir / "mcl", ignore=shutil.ignore_patterns("mcl.lock"))
    shutil.copyfile(ROOT / "harness.py", run_dir / "harness.py")
    versions = {}
    for command in (["codex", "--version"], ["python3", "--version"]):
        versions[command[0]] = run_process(command, run_dir, run_dir, command[0] + "-version", timeout=30).strip()
    if engine == "mcl":
        versions["forge"] = run_process(["forge", "--version"], run_dir, run_dir, "forge-version", timeout=30).strip()
    sources = ["input.md", "settings.json", "schemas.json", "workflow.md", "harness.py"]
    sources += [str(path.relative_to(run_dir)) for path in sorted((run_dir / "personas").glob("*.md"))]
    sources += [str(path.relative_to(run_dir)) for path in sorted((run_dir / "mcl").rglob("*")) if path.is_file()]
    write_json(run_dir / "config.json", {
        "settings": read_json(run_dir / "settings.json"), "versions": versions,
        "sha256": {name: hashlib.sha256((run_dir / name).read_bytes()).hexdigest() for name in sources},
    })


def save_artifact(run_dir, key, value):
    if key == "design":
        (run_dir / "design.md").write_text(value + "\n", encoding="utf-8")
        return
    write_json(run_dir / (key.replace("_", "-") + ".json"), value)
    if key == "final":
        (run_dir / "final.md").write_text(value["design"] + "\n", encoding="utf-8")


def run_process(command, cwd, log_dir, label, input_text=None, timeout=30):
    if not shutil.which(command[0]):
        raise RuntimeError(f"Required executable is not on PATH: {command[0]}")
    started = time.monotonic()
    process = subprocess.Popen(command, cwd=cwd, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                               stderr=subprocess.PIPE, text=True, encoding="utf-8", start_new_session=os.name != "nt")
    try:
        stdout, stderr = process.communicate(input_text, timeout=timeout)
    except (subprocess.TimeoutExpired, KeyboardInterrupt) as error:
        terminate_process(process)
        stdout, stderr = process.communicate()
        save_process_logs(log_dir, label, command, stdout, stderr, process.returncode, started)
        if isinstance(error, KeyboardInterrupt):
            raise
        raise RuntimeError(f"{label}: timed out after {timeout}s; logs: {log_dir}") from error
    save_process_logs(log_dir, label, command, stdout, stderr, process.returncode, started)
    if process.returncode:
        raise RuntimeError(f"{label}: exited {process.returncode}; see {log_dir / (label + '.stderr.log')}")
    return stdout


def terminate_process(process):
    if os.name == "nt":
        subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True, check=False)
    else:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass  # The owned process group already exited.


def save_process_logs(directory, label, command, stdout, stderr, exit_code, started):
    (directory / f"{label}.events.jsonl").write_text(stdout, encoding="utf-8")
    (directory / f"{label}.stderr.log").write_text(stderr, encoding="utf-8")
    write_json(directory / f"{label}.process.json", {
        "command": command, "exit_code": exit_code, "elapsed_seconds": round(time.monotonic() - started, 3),
    })


def validate_schema(value, schema, path="result"):
    kind = schema["type"]
    expected = {"object": dict, "array": list, "string": str}[kind]
    if not isinstance(value, expected):
        raise ValueError(f"{path}: expected {kind}")
    if "enum" in schema and value not in schema["enum"]:
        raise ValueError(f"{path}: invalid value {value!r}")
    if kind == "object":
        if set(value) != set(schema["required"]):
            raise ValueError(f"{path}: missing or unexpected fields")
        for key, child in schema["properties"].items():
            validate_schema(value[key], child, path + "." + key)
    if kind == "array":
        for index, child in enumerate(value):
            validate_schema(child, schema["items"], f"{path}[{index}]")
    if kind == "object" and "decision" in value and value["decision"] == "approved" and value["remaining_issues"]:
        raise ValueError(f"{path}: approved decision still has remaining issues")


def persona_paths(run_dir):
    return {name: run_dir / "personas" / f"{name}.md" for name in ("supervisor", "simplicity", "ownership")}


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def utc_now():
    return datetime.now(timezone.utc).isoformat()


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, RuntimeError, KeyError) as error:
        print(f"Harness error: {error}", file=sys.stderr)
        sys.exit(1)
