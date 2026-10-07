# Phase 72 — Completed baseline evidence

## Independent runners — 2026-10-07

Two no-argument PowerShell scripts in `tools/supervisor-test/` independently read `prompt.md`
and produce unique directories under `results/codex/` and `results/mcl/`. The first case is a
design-only Python retry-delay helper: design, two independent sequential reviews, one revision
and decision. No claim of implementation or full supervisor-workflow parity.

| Observation | Native Codex | MCL |
|---|---|---|
| Run ID | `20261007T173146Z-68d6f0f4` | `20261007T173954Z-5a52cb48` |
| Start/end UTC | 17:31:46.605 / 17:35:32.550 | 17:39:54.416 / 17:42:36.567 |
| End-to-end wall time | 225.943s (3m 46s) | 162.150s (2m 42s) |
| Execution state / verdict | completed / approved | completed / approved |
| Simplicity review | pass, no findings | revise, numeric type contract and missing edge tests |
| Ownership review | pass, no findings | pass, no findings |
| Final design | Exact rational arithmetic with bit-length cap detection; int/float/Fraction/Decimal inputs | Exact Fraction arithmetic with bounded doubling; int/float/Fraction inputs |

This sample's MCL run was 28% faster. One completed run each cannot establish a consistent speed
advantage or quality equivalence. The observed ownership boundaries agree; accepted numeric types,
algorithm and review findings differ. The revised MCL design addresses its two simplicity findings.

## Default-path and test evidence

Both scripts were invoked by absolute path from `/tmp`, without arguments or injected URLs/stubs.
Installed CLIs and saved Codex authentication were used. MCL uses the normal exported `MCL_API_KEY`
only to satisfy forge's default provider initialization; its mission is entirely `kind: exec`.

- `python3 -m unittest discover -s tools/supervisor-test -p 'test_*.py'`: **6 passed**. Five failure-boundary
  tests plus a controlled installed-forge JSON exec probe (the probe is not default-path acceptance).
- `forge init tools/supervisor-test/mcl/mission.mcl`: four local experts resolved.
- `forge validate tools/supervisor-test/mcl/mission.mcl`: `OK — mission is valid.`
- Real script exits: **0** for native and corrected MCL; `run.json` reports completed and `final.json`
  reports approved for each. Complete design/review/final artifacts and process logs inspected.
- SHA-256 checks: shared input, settings, schemas, workflow, three personas, mission, manifest and
  four expert definitions all match across completed runs. Native's adapter snapshot precedes the
  MCL-only `--steps` correction, so the adapter hash differs; native execution code was unchanged.
- Native session `01a1176b-da74-76a0-80c7-5552ba952bc0`: saved Codex rollout records simplicity spawn
  17:33:18.540Z, its completion before ownership spawn 17:34:22.104Z, ownership completion
  17:34:27.513Z. Both spawns use `fork_turns: none`; two actual independent reviews, not simulated text.
- MCL design/final session: `01a11773-4d17-7ef1-826d-3d5f0a9ae42d` (same ID proves exact resume).
  Simplicity: `01a11774-b3b5-7401-8e67-23ff80aab2a9`; ownership:
  `01a11775-0ff8-7ce3-a444-47a06fc24acb` (distinct sessions). Review prompt files contain the original
  design and only their applicable persona, never the other review.
- Versions: Codex `0.160.0`, Python `3.14.6`, forge
  `1.0.0+f3b49448813947d6863bd1b30b3a0860ec63108b`; both use `gpt-6.1-sol`, medium effort.

Local outputs remain under
`/Users/ameerdeen/.codex/worktrees/supervisor-test-harness/mission-control-language/tools/supervisor-test/results/`.
They are ignored by Git; the committed record preserves the named observations and run/session IDs.
CLI-emitted turn usage is preserved without assuming that native subagent usage is included.

## Resolved harness integration failure

Initial MCL run `20261007T173531Z-c95306ed` executed all stages but exited **1** when the wrapper
parsed stdout. With installed `forge run --steps`, stdout contained only a newline, although the
stage's final artifact was valid and stage content appeared on stderr. The failed run record remains
untouched. Removing `--steps` preserves the JSON result; the controlled exec probe and fresh live
MCL run both prove this path. All stage artifacts remain independently saved by the adapter.
No forge-mcl source change or output-scraping fallback was introduced.

Installed Codex emits a PowerShell shell-snapshot warning (unsupported shell snapshot), without
preventing either run. Its JSONL event stream omits some spawn calls; normal saved session rollouts
provided the native delegation observation above. The harness does not automatically grade it.

## Session closure

Operator waived the repository supervisor workflow for this session. No implementation subagents
were launched by this chat. The native test's two reviewer agents are part of the experiment itself.
Security/engineering/default-path answers remain in the active design. Desktop/ForgeUI visual,
Native AOT, hosted deploy and product implementation gates are N/A because no product source changed.

All changes were made in the requested isolated worktree on `adeen/supervisor-test-harness`.
The concurrent “Fix multi-line TUI selection” checkout, branch, files and memory were left alone.
No session-owned project memory was created. Keep this attached worktree and its ignored results
for the operator's comparison and follow-up refinement.
