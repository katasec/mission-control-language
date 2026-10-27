# Stage 2 · Phase 1 — run summary

Run: `2026-10-26-184706` · Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `dotnet-deslop-stage-2` (base `fd50eef`)
Mode: carve prompt verbatim (agent doesn't commit); supervisor verifies, checks the diff, commits locally. Nothing pushed.

## Outcome per row

| # | Row | Outcome | Commit | Duration | Rating |
|---|---|---|---|---|---|
| 1 | Single composition root (replace static `CliHttpClients` locator) | FIXED | 1cfc247 | 6m41s | 4/5 |
| 2 | Register `conversation-host` once | FIXED | 1a2e87c | 11m33s | 4/5 |
| 3 | Health probe via named client | FIXED | e70fede | 6m28s | 4/5 |
| 4 | Web-search connection recycling | FIXED | 7f4e791 | 9m00s | 5/5 |
| 5 | Config bound once as `IOptions<ForgeCliOptions>` | FIXED | f6dd973 | 13m06s | 5/5 |
| 6 | Env vars via configuration provider | **STOP** (case-insensitive keys) | stash | 19m53s | 4/5 work |
| 10 | Platform logging for `forge mcp` (stderr) | FIXED | fbddcef | 7m27s | 5/5 |
| 11 | Log MCP tool-call failures (`[LoggerMessage]`) | FIXED | 571357b | 10m09s | 5/5 |
| 7 | Sync actions → async with token | **STOP** (Ctrl-C handling) | stash | 7m07s | 4/5 work |
| 8 | `ForgeExec` linked timeout CTS | FIXED | baf0986 | 8m01s | 3/5 (stash incident) |
| 9 | Sign-in wait linked deadline CTS | FIXED | cdfb077 | 7m50s | 5/5 |

## Aggregate

| Metric | Value |
|---|---|
| Fixed / STOP | 9 commits / 2 STOPs (work stashed, recoverable) |
| Build warnings | 0 on every carve |
| Pinned tests added | ~50 characterization tests; all passed before and after each swap |
| Wall-clock | ~107 min sequential (mean ~9.75 min/row) |
| Avg rating | 4.4/5 on fixes |
| Discovery | 4 area lenses, ~6 min parallel; 28 findings → 11 rows + 17 routed |

## Playbook findings

| # | Finding | Fix for the playbook |
|---|---|---|
| 1 | A lens's prescription can itself break behavior (#5: eager `Options.Create` would change error ordering). The carve correctly overrode it. | State it: **the behavior rule beats the backlog row.** |
| 2 | Discovery's "behavior impact" column can be wrong (#7 was rated "none"). | The carve-time STOP is the real gate; keep it, don't trust the column. |
| 3 | Two agents (#6, #7) flagged gray-zone edge cases to the supervisor instead of STOPping. | Decide whether a facility's **inherent semantics** (case-insensitive env keys, termination-token handling) count as behavior — then say so. |
| 4 | Agent ran `git stash`/`pop` and dropped a supervisor stash (#8; recovered). | Carve prompt: **no state-changing git commands**; compare via `git show HEAD:<path>`. |
| 5 | New tests swap global `Console.SetError` under parallel xUnit (#8, #9) → can interleave. | Rule: tests touching global console state go in one non-parallel collection. |
| 6 | Learn grounding at carve time caught a wrong lens reference (#4) and an unsafe Learn snippet (#9: per-call `WaitAsync(TimeSpan)` would restart the deadline). | Keep `microsoft-docs` in the carve prompt. |
| 7 | **Stage-1 regression found:** S1 #8 (folder layout) broke `TuiColourLiteralTests` (hard-coded path); every carve since called them "pre-existing". | Both stages: supervisor captures a **test baseline at run start**; any new failure vs that baseline fails the gate. |
| 8 | Cancellation can only be half-adopted under strict retention: `ct` parameters and linked CTSs went in, but wiring the action token changes Ctrl-C exit codes, so it's routed. | Expected under "behavior retained"; the later stage owns the wiring. |
| 9 | Backlog line numbers drift as earlier carves edit the same files; agents relocated by name. | Backlog `{locations}` should name the symbol, not just lines. |

## Routed (see routed.md)

17 from discovery + 2 STOPped at carve (#6, #7) → later stage; 1 → Stage 1 (dead `FORGE_API_ENDPOINT`
plumbing); 1 → Stage 2 Phase 2 (JSON source-gen).
