#!/usr/bin/env python3
"""Run the one-case Phase 37.1 comparison without adding a forge eval command."""
import json, shutil, subprocess, sys, time
from pathlib import Path

ROOT = Path(__file__).resolve().parent
CASE = ROOT / "cases" / "contained-dry-run"
MISSIONS = ROOT / "missions"
run_id = time.strftime("%Y%m%d-%H%M%S")
RESULT = ROOT / "results" / run_id
RESULT.mkdir(parents=True)

def materialize(name):
    workspace = ROOT / ".workspaces" / name
    if workspace.exists(): shutil.rmtree(workspace)
    shutil.copytree(ROOT / "ledgerlite", workspace, ignore=shutil.ignore_patterns(".git", ".swamp"))
    subprocess.run(["git", "init", "--quiet"], cwd=workspace, check=True)
    subprocess.run(["git", "config", "user.name", "MCL Eval"], cwd=workspace, check=True)
    subprocess.run(["git", "config", "user.email", "mcl@example.invalid"], cwd=workspace, check=True)
    subprocess.run(["git", "add", "."], cwd=workspace, check=True)
    subprocess.run(["git", "commit", "--quiet", "-m", "base"], cwd=workspace, check=True)
    subprocess.run(["git", "apply", str(CASE / "fixture.patch")], cwd=workspace, check=True)
    return workspace

def grade(name, workspace):
    hidden = RESULT / f"{name}-hidden"
    overlay = {}
    for test in (CASE / "hidden-tests").rglob("*_test.go"):
        target = workspace / test.relative_to(CASE / "hidden-tests")
        overlay[str(target.resolve())] = str(test.resolve())
    overlay_path = RESULT / f"{name}-overlay.json"
    overlay_path.write_text(json.dumps({"Replace": overlay}))
    commands = [["go","test","./..."], ["go","vet","./..."], ["go","run","./tools/generate","-check"], ["go","test",f"-overlay={overlay_path}","./..."]]
    results=[]
    for command in commands:
        start=time.monotonic(); p=subprocess.run(command,cwd=workspace,text=True,capture_output=True)
        results.append({"command":command,"exit_code":p.returncode,"duration_ms":int((time.monotonic()-start)*1000),"stdout":p.stdout,"stderr":p.stderr})
    grade={"passed":all(item["exit_code"] == 0 for item in results),"commands":results}
    (RESULT / f"{name}-grade.json").write_text(json.dumps(grade,indent=2))
    return grade

def run_mission(name, file):
    measurements = RESULT / f"{name}-measurements.json"
    start=time.monotonic()
    command=["dotnet","run","--project",str(ROOT.parents[1] / "src" / "ForgeMission.Cli"),"--","run",str(file),"--measurements-file",str(measurements)]
    environment = dict(**__import__("os").environ)
    if name == "CodeReview":
        environment["CODE_REVIEW_VALIDATION_FILE"] = str(RESULT / "CodeReview-grade.json")
    if name == "GarfieldSkill":
        environment["GARFIELD_PROGRESS_LOG"] = str(RESULT / "GarfieldSkill-progress.log")
    p=subprocess.run(command,cwd=ROOT.parents[1],text=True,capture_output=True,env=environment)
    (RESULT / f"{name}-stdout.txt").write_text(p.stdout)
    (RESULT / f"{name}-stderr.txt").write_text(p.stderr)
    return {"exit_code":p.returncode,"duration_ms":int((time.monotonic()-start)*1000),"measurements":json.loads(measurements.read_text()) if measurements.exists() else []}

code = run_mission("CodeReview", MISSIONS / "code-review.mcl")
code["grade"] = json.loads((RESULT / "CodeReview-grade.json").read_text()) if (RESULT / "CodeReview-grade.json").exists() else {"passed":False,"reason":"Validator did not produce a grade"}
materialize("garfield")
garfield = run_mission("GarfieldSkill", MISSIONS / "garfield-skill.mcl")
garfield["grade"] = grade("GarfieldSkill", ROOT / ".workspaces" / "garfield")
payload={"run_id":run_id,"CodeReview":code,"GarfieldSkill":garfield}
(RESULT / "results.json").write_text(json.dumps(payload,indent=2))

def totals(run):
    m=run["measurements"]
    return (
        sum(x.get("InputTokens", x.get("inputTokens", 0)) for x in m),
        sum(x.get("CachedInputTokens", x.get("cachedInputTokens", 0)) for x in m),
        sum(x.get("OutputTokens", x.get("outputTokens", 0)) for x in m),
        sum(x.get("DurationMs", x.get("durationMs", 0)) for x in m),
    )
lines=[f"# Phase 37.1 — {run_id}","", "| Treatment | Grade | Tokens (input/cached/output) | Cost | Step latency |", "|---|---|---:|---:|---:|"]
for name, run in payload.items():
    if name == "run_id": continue
    inp,cached,out,latency=totals(run)
    cost=(inp-cached)/1e6+cached/1e6*.10+out/1e6*6
    lines.append(f"| {name} | {'pass' if run['grade'].get('passed') else 'fail'} | {inp:,} / {cached:,} / {out:,} | ${cost:.6f} | {latency:,} ms |")
lines += ["", "N=1 raw comparison. GarfieldSkill is valid when it completes or reports its $3 fuse outcome."]
(RESULT / "report.md").write_text("\n".join(lines)+"\n")
print(RESULT / "report.md")
