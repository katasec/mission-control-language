# Supervisor comparison test

Draft awaiting operator approval. The previous Codex-backed MCL experiment and its comparison
data have been discarded. Neither rewritten runner has accepted live execution evidence.

Run the same build request independently through native Codex orchestration and an MCL mission:

```powershell
./run-codex.ps1
./run-mcl.ps1
```

Scripts resolve everything relative to their location, so absolute script paths work from any cwd.
Edit **`prompt.md`** to change what needs to be built. This first slice produces a design and two
independent reviews, then a revised design/decision; it does not implement the requested product.

## Requirements

- PowerShell 7 and Python 3 for either script.
- Codex path: Codex CLI with saved authentication (`codex login`). Personal config is ignored.
- MCL path: installed `forge` and exported `MCL_API_KEY`; Codex is not required.
- `settings.json` supplies the shared model. The MCL child receives it through
  `MCL_HARNESS_MODEL` and calls OpenAI directly. Actual provider/account access is untested.
- This draft supports medium reasoning only. Codex explicitly requests it; MCL uses the provider
  default. The harness does not claim that both runtimes expose identical settings or tools.

## Flow and ownership

Native path: one Codex supervisor delegates to a simplicity reviewer, waits, delegates to an
ownership reviewer, waits, then revises. MCL path: four `kind: llm` experts call the provider
directly. Native `json_extract` steps retain named artifacts. Its final call receives those
artifacts explicitly, without a persistent supervisor session. Each LLM step clears implicit
previous output so the second reviewer cannot inherit the first review.
Both reviewers see the original design and request independently.

`harness.py` owns subprocess calls, snapshots, schemas and result files. The MCL mission owns the
MCL sequence and named handoffs. Its sole exec step validates and writes files; it never calls
Codex or a model. `workflow.md` and `personas/` hold shared role instructions; the requirement
lives only in `prompt.md`. Native Codex runs read-only in a temporary workspace outside Forge
checkouts. MCL receives supplied text with no tool/search steps. This tool changes no product source.

## Results

Each invocation creates a unique folder, preserving previous runs:

```text
results/
  codex/<UTC-run-id>/
  mcl/<UTC-run-id>/
```

Each folder contains `input.md`, `config.json` (settings, versions and input hashes), `design.md`,
`simplicity-review.json`, `ownership-review.json`, `final.json`, `final.md`, `run.json`, frozen
personas/workflow/mission/file writer, and subprocess logs. Codex's `stages/` includes exact
prompts, schemas, JSONL events and session/usage metadata. MCL includes init/validate/run output;
its direct-provider token usage is not collected by this draft.

`run.json` distinguishes completed execution from failed/interrupted execution. A completed run
may decide `needs_revision`: that is a design verdict, not a subprocess failure. Timeout, missing
CLI, invalid result or failed child returns nonzero and leaves partial artifacts. Run the script
again to recover into a new folder. POSIX timeout/interruption kills the owned process group.

After approval, compare matching request/persona hashes, then final designs, actionable findings
and remaining issues. The harness does not automatically grade delegation or output quality.
Different wording is expected. Native subagent usage is not assumed to be included in its
supervisor's CLI-reported token counts. No timing or behavioural equivalence is established yet.

## Verification

```powershell
python3 -m unittest discover -s ./tools/supervisor-test -p 'test_*.py'
```

These focused tests cover schema failures, false approvals, failed children, timeouts, MCL artifact
writing without Codex and preservation of previous results. No live model run is part of the suite.
Live default commands above remain pending operator approval.
Design and closure evidence: [Phase 72](../../docs/phases/phase-72-supervisor-comparison.md).
