# Supervisor comparison test

Run the same build request independently through native Codex orchestration and an MCL mission:

```powershell
./run-codex.ps1
./run-mcl.ps1
```

Scripts resolve everything relative to their location, so absolute script paths work from any cwd.
Edit **`prompt.md`** to change what needs to be built. This first slice produces a design and two
independent reviews, then a revised design/decision; it does not implement the requested product.

## Requirements

- PowerShell 7, Python 3, Codex CLI with saved authentication (`codex login`).
- Installed `forge` for the MCL runner, plus the normal exported `MCL_API_KEY`.
  The current CLI initializes a provider even for exec-only missions. That provider is unused;
  all reasoning is performed through Codex CLI, and no Ollama or local service is required.
- `settings.json` pins model and reasoning effort for both paths. Access to that model is required.
  Personal Codex config is ignored for reproducibility; saved CLI authentication is still used.

## Flow and ownership

Native path: one Codex supervisor delegates to a simplicity reviewer, waits, delegates to an
ownership reviewer, waits, then revises. MCL path: the declared mission schedules those four
stages through `kind: exec`; the final stage resumes its supervisor's exact session ID.
Both reviewers see the original design and request independently.

`harness.py` owns subprocess calls, snapshots, schemas and result files. The MCL mission owns the
MCL sequence and named handoffs. `workflow.md` and `personas/` hold shared role instructions;
the requirement lives only in `prompt.md`. Agents receive text in a temporary read-only workspace
outside the Forge checkouts. This tool changes no Forge product source.

## Results

Each invocation creates a unique folder, preserving previous runs:

```text
results/
  codex/<UTC-run-id>/
  mcl/<UTC-run-id>/
```

Each folder contains `input.md`, `config.json` (settings, versions and input hashes), `design.md`,
`simplicity-review.json`, `ownership-review.json`, `final.json`, `final.md`, `run.json`, frozen
personas/workflow/mission/adapter, and subprocess logs. `stages/` includes exact prompts, schemas,
Codex JSONL events and session/usage metadata. MCL also includes init/validate/run output.

`run.json` distinguishes completed execution from failed/interrupted execution. A completed run
may decide `needs_revision`: that is a design verdict, not a subprocess failure. Timeout, missing
CLI, invalid result or failed child returns nonzero and leaves partial artifacts. Run the script
again to recover into a new folder. POSIX timeout/interruption kills the owned process group.

Compare matching input/settings hashes first, then final designs, actionable findings and remaining
issues. The installed Codex CLI's JSONL stream exposes waits but omits some spawn calls. For native
delegation details, use the session ID in `stages/workflow.metadata.json` to locate its normal
`~/.codex/sessions/` rollout (or under `$CODEX_HOME`). This initial harness does not automatically
grade delegation or output quality. Different wording is expected;
similarity in requirements, decisions and evidence is the useful measure. Native subagent usage
is not assumed to be included in its supervisor's CLI-reported token counts.

## Verification

```powershell
python3 -m unittest discover -s ./tools/supervisor-test -p 'test_*.py'
```

These focused tests cover schema failures, false approvals, failed children, timeouts and preservation
of previous results. Live default commands above separately establish actual Codex/MCL execution.
Design and closure evidence: [Phase 72](../../docs/phases/phase-72-supervisor-comparison.md).
