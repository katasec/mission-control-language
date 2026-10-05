#!/usr/bin/env python3
"""Stage timing for one task, from design to merge.

Reads stage-tagged subagent session logs from Claude Code (`[stage] <task> ...` descriptions) and
Codex (`stage__task` task names), and the task's merged pull requests, and prints a Markdown table
for the task's completion record.

Usage: timing.py <task> [--pr owner/repo#123 ...]
"""
import argparse
import json
import re
import subprocess
import sys
from datetime import datetime
from pathlib import Path

CLAUDE_LOGS = Path.home() / ".claude" / "projects"
CODEX_LOGS = Path.home() / ".codex" / "sessions"
TAG = re.compile(r"^\[([^\]]+)\]\s+(.*)$")


def main():
    args = parse_args()
    runs = [run for run in find_runs(args.task)]
    if not runs:
        sys.exit(f"No subagent runs tagged for task '{args.task}'.")
    prs = [pull_request(ref) for ref in args.pr]
    print(render(args.task, runs, prs))


def parse_args():
    parser = argparse.ArgumentParser(description="Stage timing for one task, from design to merge.")
    parser.add_argument("task", help="task id as written after the stage tag, e.g. '69' or '64.2 task 3'")
    parser.add_argument("--pr", action="append", default=[], help="merged PR as owner/repo#number")
    return parser.parse_args()


# --- Subagent runs -------------------------------------------------------------------------------

def find_runs(task):
    yield from claude_runs(task)
    yield from codex_runs(task)


# Claude Code: one meta file (description) and one transcript per subagent.

def claude_runs(task):
    for meta_path in CLAUDE_LOGS.glob("*/*/subagents/*.meta.json"):
        tag = claude_tag(meta_path, task)
        if tag is None:
            continue
        start, end, tokens = claude_facts(meta_path.with_name(meta_path.name.replace(".meta.json", ".jsonl")))
        if start is not None:
            yield {"stage": tag, "start": start, "end": end, "tokens": tokens}


def claude_tag(meta_path, task):
    description = json.loads(meta_path.read_text()).get("description", "")
    match = TAG.match(description)
    if match is None:
        return None
    tag, rest = match.groups()
    if rest != task and not rest.startswith(task + " "):
        return None
    return tag


def claude_facts(jsonl_path):
    """First and last timestamp, and the agent's final context size (what the harness reports)."""
    if not jsonl_path.exists():
        return None, None, 0
    stamps, last_usage = [], None
    for line in jsonl_path.read_text().splitlines():
        entry = json.loads(line)
        if "timestamp" in entry:
            stamps.append(parse_time(entry["timestamp"]))
        message = entry.get("message") or {}
        if message.get("usage"):
            last_usage = message["usage"]
    tokens = total_tokens(last_usage) if last_usage else 0
    return (min(stamps), max(stamps), tokens) if stamps else (None, None, 0)


# Codex: one session file per subagent; its first line names it (`agent_path` ends in the task_name).

def codex_runs(task):
    wanted = codex_name(task)
    for path in CODEX_LOGS.glob("*/*/*/*.jsonl"):
        meta = first_entry(path).get("payload") or {}
        if meta.get("thread_source") != "subagent":
            continue
        tag = codex_tag(meta.get("agent_path", "").rsplit("/", 1)[-1], wanted)
        if tag is not None:
            yield {"stage": tag, **codex_facts(path, meta)}


def codex_tag(task_name, wanted):
    """`review_plan__ownership__r2__64_2_task_3` -> `review-plan:ownership:r2` when the task matches."""
    parts = task_name.split("__")
    if not 2 <= len(parts) <= 4:
        return None
    task = parts[-1]
    if task != wanted and not task.startswith(wanted + "_"):
        return None
    return ":".join([parts[0].replace("_", "-")] + parts[1:-1])


def codex_facts(path, meta):
    """Spawn time to last log line, and the context size of the last model call."""
    end, last_usage = None, None
    for line in path.read_text().splitlines():
        entry = json.loads(line)
        end = entry.get("timestamp", end)
        info = (entry.get("payload") or {}).get("info") or {}
        last_usage = info.get("last_token_usage", last_usage)
    tokens = (last_usage["input_tokens"] + last_usage["output_tokens"]) if last_usage else 0
    return {"start": parse_time(meta["timestamp"]), "end": parse_time(end), "tokens": tokens}


def codex_name(task):
    return re.sub(r"[^a-z0-9]+", "_", task.lower()).strip("_")


def first_entry(path):
    with path.open() as handle:
        line = handle.readline()
    return json.loads(line) if line.strip() else {}


def total_tokens(usage):
    keys = ("input_tokens", "cache_creation_input_tokens", "cache_read_input_tokens", "output_tokens")
    return sum(usage.get(key) or 0 for key in keys)


# --- Pull requests -------------------------------------------------------------------------------

def pull_request(ref):
    repo, number = ref.split("#")
    result = subprocess.run(
        ["gh", "pr", "view", number, "--repo", repo, "--json", "createdAt,mergedAt"],
        capture_output=True, text=True, check=True)
    facts = json.loads(result.stdout)
    if not facts.get("mergedAt"):
        sys.exit(f"{ref} is not merged.")
    return {"ref": ref, "opened": parse_time(facts["createdAt"]), "merged": parse_time(facts["mergedAt"])}


# --- Rendering -----------------------------------------------------------------------------------

def render(task, runs, prs):
    cutoff = max(pr["merged"] for pr in prs) if prs else None
    runs, excluded = within(runs, cutoff)
    lines = [f"Task `{task}`", "", "| Stage | Agents | Start | End | Wall | Tokens |", "|---|---|---|---|---|---|"]
    for stage, group in grouped_by_stage(runs):
        start, end = min(r["start"] for r in group), max(r["end"] for r in group)
        tokens = sum(r["tokens"] for r in group)
        lines.append(f"| `{stage}` | {len(group)} | {clock(start)} | {clock(end)} | {span(start, end)} | {tokens:,} |")
    for pr in prs:
        lines.append(f"| PR {pr['ref']} | — | {clock(pr['opened'])} | {clock(pr['merged'])} | {span(pr['opened'], pr['merged'])} | — |")
    first = min(r["start"] for r in runs)
    last = cutoff or max(r["end"] for r in runs)
    rounds = sum(1 for r in runs if stage_row(r["stage"]) != stage_row(r["stage"]).split(":")[0])
    lines += ["", f"**End to end:** {clock(first)} → {clock(last)} = {span(first, last)}; "
                  f"{sum(r['tokens'] for r in runs):,} subagent tokens; {rounds} revision round(s)."]
    if not prs:
        lines.append("No product PR given: the end point is the last subagent run (documentation-only task).")
    if excluded:
        lines.append(f"{excluded} run(s) starting after the last product merge are excluded.")
    return "\n".join(lines)


def within(runs, cutoff):
    """Runs up to the last product merge, with any run still open at that moment cut off there."""
    if cutoff is None:
        return runs, 0
    kept = [{**r, "end": min(r["end"], cutoff)} for r in runs if r["start"] <= cutoff]
    if not kept:
        sys.exit("Every tagged run starts after the last product merge.")
    return kept, len(runs) - len(kept)


def grouped_by_stage(runs):
    """Parallel agents of one stage share a row; each revision round gets its own row."""
    stages = {}
    for run in sorted(runs, key=lambda r: r["start"]):
        stages.setdefault(stage_row(run["stage"]), []).append(run)
    return stages.items()


def stage_row(tag):
    """`review-plan:ownership:r2` -> `review-plan:r2`; `review-plan:ownership` -> `review-plan`."""
    segments = tag.split(":")
    revision = segments[-1] if len(segments) > 1 and re.fullmatch(r"r\d+", segments[-1]) else None
    return f"{segments[0]}:{revision}" if revision else segments[0]


def parse_time(value):
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def clock(moment):
    return moment.astimezone().strftime("%m-%d %H:%M:%S")


def span(start, end):
    seconds = int((end - start).total_seconds())
    hours, rest = divmod(seconds, 3600)
    minutes, seconds = divmod(rest, 60)
    return f"{hours}h {minutes:02d}m" if hours else f"{minutes}m {seconds:02d}s"


if __name__ == "__main__":
    main()
