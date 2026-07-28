#!/usr/bin/env python3
import json, shutil, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CASE = ROOT / "cases" / "contained-dry-run"
BASE = ROOT / "ledgerlite"
WORK = ROOT / ".workspaces" / "code-review"

def run(*args):
    return subprocess.run(args, cwd=WORK, text=True, capture_output=True, check=False)

def prepare():
    if WORK.exists(): shutil.rmtree(WORK)
    WORK.parent.mkdir(parents=True, exist_ok=True)
    shutil.copytree(BASE, WORK, ignore=shutil.ignore_patterns(".git", ".swamp"))
    run("git", "init", "--quiet"); run("git", "config", "user.name", "MCL Eval")
    run("git", "config", "user.email", "mcl@example.invalid"); run("git", "add", ".")
    run("git", "commit", "--quiet", "-m", "base")
    applied = run("git", "apply", str(CASE / "fixture.patch"))
    if applied.returncode: raise RuntimeError(applied.stderr)

def output(value): print(json.dumps(value))

stage = sys.argv[1]
try:
    if stage == "GitDiff":
        prepare(); result = run("git", "diff", "--", ".") .stdout
        output({"git_diff": result})
    elif stage == "RepositoryInventory":
        files = [p.relative_to(WORK).as_posix() for p in WORK.rglob("*") if p.is_file() and ".git" not in p.parts]
        output({"inventory": "\n".join(sorted(files))})
    elif stage == "FileFiltering":
        output({"changed_files": run("git", "status", "--porcelain").stdout})
    elif stage == "LanguageDetection":
        output({"languages": "Go"})
    elif stage == "ProjectDetection":
        output({"project": "Go module; validate with go test ./..., go vet ./..., go run ./tools/generate -check"})
    elif stage == "ContextConstruction":
        prompt = (CASE / "prompt.md").read_text()
        diff = run("git", "diff", "--").stdout
        output({"review_context": f"Request:\n{prompt}\n\nFixture diff:\n{diff}\n"})
    else: raise RuntimeError(f"unknown stage {stage}")
except Exception as error:
    output({"status": "fail", "reason": str(error), {"GitDiff":"git_diff","RepositoryInventory":"inventory","FileFiltering":"changed_files","LanguageDetection":"languages","ProjectDetection":"project","ContextConstruction":"review_context"}[stage]: ""})
