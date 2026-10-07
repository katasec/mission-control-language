# Phase 72 — Initial harness verification

## Native MCL / native Codex run — 2026-10-07

This evidence belongs to the approved native MCL rewrite. It does not reuse the discarded
Codex-backed MCL experiment.

| Check | Observation |
|---|---|
| Focused tests | `python3 -m unittest discover -s ./tools/supervisor-test -p 'test_*.py'`: seven tests pass, including invalid MCL-artifact retention and a writer that cannot call Codex/processes |
| Default actions | Both absolute PowerShell script paths ran with zero arguments from `/tmp` using installed CLIs and normal exported credentials |
| Artifacts | Native MCL: installed Forge `1.0.0+f3b49448813947d6863bd1b30b3a0860ec63108b`; native Codex `codex-cli 0.160.0`; Python 3.14.6 |
| Model and route | Both use saved `gpt-6.1-sol` settings; MCL invokes OpenAI directly with normal MCL_API_KEY, Codex uses saved CLI authentication. No substituted endpoint/service/stub. MCL_HARNESS_MODEL is the harness's documented child-process wiring. |
| Starting state | Dedicated timestamped folders in the isolated harness worktree; Codex reasoning executes read-only outside Forge checkouts; no product Project or source mutation |
| Shared inputs | SHA-256 equality checked for input.md, settings.json, schemas.json, workflow.md and all three personas |
| MCL completed run | `tools/supervisor-test/results/mcl/20261007T180741Z-ccb96dcd/run.json`: completed, 83.423 seconds, exit 0; final approved; both reviews pass with no findings |
| Codex completed run | `tools/supervisor-test/results/codex/20261007T180605Z-7da688c1/run.json`: completed, 200.104 seconds, exit 0; final approved; both reviews pass with no findings |
| Native delegation | Rollout `01a1178b-459c-72d0-a58e-e3c9186cd614` contains two actual spawn_agent calls, named simplicity_review and ownership_review, both fork_turns=none, with a wait between them |
| MCL provenance | Saved mission uses four native llm experts, native json_extract handoffs and one deterministic Python result writer. Its recorded process commands are Forge and Python only. No Codex call path in MCL stages. |
| Files | Each completed run preserves original/final designs, reviews, frozen inputs, configuration hashes, process logs and final JSON; MCL also preserves declared incoming writer artifacts |
| Failure containment | First new MCL run (`20261007T180431Z-c04cd04b`) failed at SaveResults JSON parsing, returned nonzero and retained failed run/logs. The final handoff now also goes through json_extract under a final key. Fresh complete run passed; failed run excluded from timing comparison. |

## Output comparison

Both designs select exact rational arithmetic for int/float/Decimal/Fraction inputs, exclude bool,
validate before capping, avoid attempt-sized shifts for huge attempts and retain the runner/helper
ownership boundary. Neither review raises findings.

The main API difference is the uncapped return type: native Codex always returns Fraction; MCL
returns int for integral values and Fraction otherwise. Native specifies more concrete numeric
test examples and Decimal signaling NaN; MCL explicitly mentions subnormal floats. The algorithms
use different bit-length comparisons to implement the same requested capped doubling behaviour.

Results are designs, not implemented modules or algorithm test evidence. One sample cannot establish
timing/quality equivalence. Successful runs overlapped; native delegation overhead and authentication
routes differ. Codex explicitly requests medium reasoning while MCL leaves the provider default.
The no-findings sample does not establish behaviour after actionable review findings.

Local inspectable comparison: `tools/supervisor-test/results/comparison.md`. Results are ignored
local artifacts, not committed or published. This compact record preserves the observations across
clones; exact saved output files remain on the operator's machine. Operator output review and any
subsequent refinement remain open in the active spoke.
