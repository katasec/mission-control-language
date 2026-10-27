# Stage 2 · Phase 1 — run 5 summary

Run: `2026-10-27-042106` · forge-mcl / src/ForgeMission.Cli · base `fd50eef` → `0be6a51` (22 local commits, none pushed).
Live integration tests (run once, with the operator's OK): 1 passed / 3 failed / 6 skipped. The 3 failures are pre-existing (a Claude credential sent to OpenAI → 401; R16), not caused by run 5.

## Run 5 against run 4

| | Run 4 (stopped at row 8) | Run 5 |
|---|---|---|
| Rows done | 7 of 24 | **20 of 20 + row 0** (16 folded into 18) |
| Time per row (supervisor) | ~15–20 min | **~6.6 min** (138 min / 21) |
| Wall-clock, fix pass | ~2 h for 7 rows | **1 h 53 m for 21 rows** (B ‖ D in parallel saved ~19 min) |
| Check passed first time | 3 of 7 | **17 of 21** (row 0, 2, 3 and 12 failed; one correction fixed each) |
| Mean rating | ~4.3 | **4.65** (thirteen 5s, seven 4s) |
| Live-service breaches | 2 (real API call; placeholder key to OpenAI) | **0**. The proxy refused `api.x.ai` with a test key, and the harness blocks browser launches |
| STOPs | 0 | 4, all handled by the rules: row 6 (race, Q4), row 16 (sequencing, folded), row 18 partial (R11), row 20 partial (R14) |

## What worked (playbook behavior)

- **Row 0's harness and non-forwarding proxy.** Once it absorbed the old runners, every CLI process test runs isolated. This is the safety fix run 4 needed.
- **Speed changes E1–E10:** scoped tests in each carve, one implementer per group, batched trivial rows, lane lists, and the known-Check-failure list. They brought time per row down from ~17 to ~7 min without lowering quality.
- **"The rule beats the row" decided real cases:** the options split per consumer (row 3), Ctrl+C still exits 130 (row 18), and STOP instead of a bridge (row 16).
- **The Check still catches real slop:** a copied runner, a near-copy test, a test that reached the code only by reflection, and a second host.

## What to change in the playbook (`playbook-notes.md` P1–P8)

- **P1:** row 0 absorbs the repo's existing ad-hoc runners into the harness.
- **P2:** a carve may merge a test that its own change turned into an exact copy.
- **P3:** the configuration lens decides whether settings bind from the root configuration or from one named section (see Q3).
- **P4:** before adding validation to an options type, check which consumers read it; use one type per consumer set.
- **P5:** a difference that only shows up in a race is recorded, never a STOP.
- **P6:** when the swap changes a signature, pin through the process so the pin runs on HEAD first.
- **P7 (sequencing):** a row that *removes* a hand-rolled source of a value goes after the row that supplies the platform source. Rows that only add are safe before it.
- **P8:** the host owns shutdown only for a server the app's one host runs. A server a subcommand starts uses the command's token; never build a second host for it.

## Open for the operator

- `questions.md` Q1–Q5, each with a default taken. If an answer differs from its default and touches rows, the playbook re-runs.
- `routed.md` R1–R15. Real bugs found along the way: chat crashes on a non-JSON 500 (R10), `forge claude` loses claude's exit code (R12), and the container leak on `forge claude` Ctrl+C (R5).
- R16 (security, pre-existing): a Claude `sk-ant-o…` credential is sent to the OpenAI API by mission steps behind `forge serve`/`forge claude`.
