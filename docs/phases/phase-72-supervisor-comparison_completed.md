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

## Per-stage timing verification — 2026-10-07

One deterministic Timestamp expert now runs before the pipeline and after every model,
extraction and save step. It uses host UTC/monotonic clocks and preserves the prior output.

| Check | Observation |
|---|---|
| Focused tests | Eight tests pass; the new test checks cumulative/interval arithmetic and exact preservation of a mixed prose/JSON-fence payload |
| Default action | Zero-argument run-mcl.ps1 from `/tmp`, installed Forge and normal provider credentials; no new setting/tool/provider override |
| Run | `tools/supervisor-test/results/mcl/20261007T193442Z-c4f57320`: completed, 94.219 seconds, final approved, both reviews pass |
| Timing records | Ten ordered markers: Start, each of four LLM calls, each of four Remember steps, SaveResults. Nonnegative intervals and ordered monotonic timestamps checked. |
| Payload boundary | Parsed Forge stdout equals saved final.json after the final Timestamp; all original/review/final artifacts validate |
| Reports | timings.jsonl contains real UTC/monotonic timestamps; timings.md and console output report intervals and cumulative time |

| MCL interval | Seconds |
|---|---:|
| SupervisorDesign | 57.914905 |
| SimplicityReview | 12.366021 |
| OwnershipReview | 7.108771 |
| SupervisorDecision | 14.380517 |
| Four extraction intervals plus saving | 0.276391 |
| Full marker window | 92.046605 |
| Full script including time outside markers | 94.219 |

These are stage wall-time intervals, including adjacent marker-process overhead and
provider/runtime latency. They are not provider compute-only timings or token measurements.
The approximately 2.17 seconds outside the marker window include CLI/harness startup and teardown;
the instrumentation does not break those costs down. This new MCL sample is distinct from the
earlier 83.423-second sample and does not replace its recorded result.

The existing native trace also gives useful intervals: task start to first reviewer launch
73.321s; first launch to returned review 10.995s; first review return to second launch 45.080s;
second launch to review return 9.972s; second return to task completion 59.640s. Launch receipts
arrived in 0.088s and 0.074s. Thus the 45-second interval is before launching the next reviewer,
not a 45-second launch operation. The trace does not distinguish model processing, backend latency
and scheduling within that supervisor interval, or establish a Desktop-app defect.
