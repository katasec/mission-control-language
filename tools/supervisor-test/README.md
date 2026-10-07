# Supervisor comparison test

The approved native MCL and Codex runners completed independently on 2026-10-07. The previous
Codex-backed MCL experiment and its comparison data remain discarded. This first sample does not
establish timing or quality equivalence; see the [verification record](../../docs/phases/phase-72-supervisor-comparison_completed.md).

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
  `MCL_HARNESS_MODEL` and calls OpenAI directly. The installed provider completed the real mission.
- This harness supports medium reasoning only. Codex explicitly requests it; MCL uses the provider
  default. The harness does not claim that both runtimes expose identical settings or tools.

## Flow and ownership

Native path: one Codex supervisor delegates to a simplicity reviewer, waits, delegates to an
ownership reviewer, waits, then revises. MCL path: four `kind: llm` experts call the provider
directly. Native `json_extract` steps retain all four named artifacts, including the final result.
Its final call receives the original design and both reviews explicitly, without a persistent
supervisor session. Each LLM step clears implicit
previous output so the second reviewer cannot inherit the first review.
Both reviewers see the original design and request independently.

`harness.py` owns subprocess calls, snapshots, schemas and result files. The MCL mission owns the
MCL sequence and named handoffs. Its exec steps record timestamps or validate and write files;
they never call Codex or a model. `workflow.md` and `personas/` hold shared role instructions; the requirement
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
MCL also includes `timings.jsonl` and `timings.md`: one starting timestamp and one after each
reasoning, extraction and save step. The reusable `Timestamp` expert preserves the previous
output unchanged. Times come from the host's UTC and monotonic clocks, with no model call.
Each interval includes adjacent timestamp-process overhead and provider/runtime latency.

`run.json` distinguishes completed execution from failed/interrupted execution. A completed run
may decide `needs_revision`: that is a design verdict, not a subprocess failure. Timeout, missing
CLI, invalid result or failed child returns nonzero and leaves partial artifacts. Run the script
again to recover into a new folder. POSIX timeout/interruption kills the owned process group.

Compare matching request/persona hashes, then final designs, actionable findings
and remaining issues. The harness does not automatically grade delegation or output quality.
Different wording is expected. Native subagent usage is not assumed to be included in its
supervisor's CLI-reported token counts. No timing or behavioural equivalence is established yet.
MCL preserves its declared writer inputs as `mcl-artifacts.raw.json`, including on validation failure.

## Verification

```powershell
python3 -m unittest discover -s ./tools/supervisor-test -p 'test_*.py'
```

These focused tests cover schema failures, false approvals, failed children, timeouts, MCL artifact
writing without Codex and preservation of previous results. No live model run is part of the suite.
Eight focused tests, both original live default commands and the instrumented MCL command passed.
The first new MCL attempt failed at the
final JSON handoff; the final answer now passes through native JSON extraction like the other stages.
Design and closure evidence: [Phase 72](../../docs/phases/phase-72-supervisor-comparison.md).
