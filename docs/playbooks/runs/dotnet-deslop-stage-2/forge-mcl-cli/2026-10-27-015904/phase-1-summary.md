# Stage 2 · Phase 1 — run 4 summary (stopped early to apply learnings)

Run: `2026-10-27-015904` · forge-mcl / src/ForgeMission.Cli · base `fd50eef` · stopped during row 8.
Backup of the 7 commits: local branch `dotnet-deslop-stage-2-run4-backup`.

| # | Row | Commit | Carve + correction | Check |
|---|---|---|---|---|
| 1 | One host composition root | 73dd03b | 12m00 + 5m13 | FAIL → PASS (logging pipeline with no consumer = factory noise) |
| 2 | (Q1) config.json via host JSON provider | ce7901b | 18m39 | PASS |
| 3 | Consumers take IHttpClientFactory | 01529bb | 11m18 + 6m19 | FAIL → PASS (third copy of test harness) |
| 4 | conversation-host once | f352059 | 8m24 + 4m37 | FAIL → PASS (pre-empted row 6) |
| 5 | forge mcp on the one host | ac16b18 | 12m13 | PASS |
| 6 | (Q1) endpoint via IOptions | d1e3042 | 10m46 + 4m23 | FAIL → PASS (redundant tests) |
| 7 | (Q1) theme via [OptionsValidator] | 7e19008 | 8m35 | PASS |

## What run 4 showed about the playbook (behavior, not solutions)

- **Discovery revisions worked**: no composition conflict, no redundant build-setting row, one genuine
  open question, rules settled the rest. Open questions collected without blocking.
- **The Check step works**: 4/7 rows failed first pass on real slop; one correction fixed each; FAILs
  shifted from product to test bloat as rules were added (shared harness, lane list, pin budget).
- **Rules added mid-run cut corrections**: rows 5 and 7 passed first time; per-row time ~20 → ~10 min.
- **Too slow**: ~15–20 min/row → ~7 h for 24 rows. Causes and fixes in `efficiency-notes.md` (E1–E10).
- **Prompt-only safety rule failed twice** (live calls) → needs a mechanical guard (E9).

Decision (operator): stop, fold learnings into the playbook, reset, run again (run 5).
