# Stage 2 · Phase 1 — run 3 summary

Run: `2026-10-26-213840` · Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `dotnet-deslop-stage-2` (base `fd50eef`, HEAD `ebaf2ca`)
Playbook: revised Stage 2 (app's own behavior retained; platform semantics adopted; run-start baseline; supervisor owns git state).
Nothing pushed.

## Outcome per row

| # | Row | Commit | Duration | Rating |
|---|---|---|---|---|
| 1 | Single `ServiceCollection` composition root | 232811f | 6m44s | 4/5 |
| 2 | `conversation-host` registered once | d34a892 | 8m05s | 4/5 |
| 3 | Health probe via named client | baf446f | 6m36s | 5/5 |
| 4 | `forge mcp` on the Generic Host | da178fc | 9m25s | 5/5 |
| 5 | Binding source generator (redundant: SDK default) | 7ee34d6 | 10m34s | 3/5 |
| 6 | `config.json` via JSON provider + options class | b809feb | 7m18s | 5/5 |
| 7 | Settings bound once (`IOptions`) | 8aeec4b | 23m22s | 5/5 |
| 8 | Theme validation via `IValidateOptions` (net complexity up) | 2fe6424 | 6m33s | 3/5 |
| 9 | Login env vars via config provider (STOP in run 2) | a6db645 | 9m03s | 4/5 |
| 10 | Web-search keys from configuration | 613f3a5 | 9m12s | 4/5 |
| 11 | Log MCP tool failures (`[LoggerMessage]`) | 8f42953 | 6m53s | 5/5 |
| 12 | Explicit `ProcessTerminationTimeout` (30s) | d0b3c82 | 7m49s | 4/5 |
| 13 | Program.cs actions flow `ct` | 84ae34a | 7m21s | 5/5 |
| 14 (+15) | exec/run flow `ct` (absorbed #15) | 2f58c7d | 9m02s | 4/5 |
| 16 | login/whoami flow `ct` | 1002a7e | 5m32s | 4/5 |
| 17 | Sign-in wait linked deadline | 7315120 | 7m20s | 4/5 |
| 18 | project create flows `ct` | 2faf530 | 5m59s | 4/5 |
| 19 | Chat line mode uses the action's `ct` | 146c90a | 15m03s | 5/5 |
| 20 | `--print-env` waits on `ct` | b525fe8 | 5m45s | 4/5 |
| 21 | Health poll linked deadline (retry after API/TLS error) | e5dcde0 | 11m00s | 5/5 |
| 22 | serve/connect: host owns shutdown | 825e69e | 7m54s | 4/5 |
| 23 | `forge mcp` honors cancellation | ebaf2ca | 10m41s | 4/5 |

## Aggregate vs run 2

| Metric | Run 2 | Run 3 |
|---|---|---|
| Backlog rows | 11 | 23 (1 absorbed) |
| Shipped / STOP | 9 / 2 | **22 / 0** |
| Avg rating | 4.4 | 4.3 |
| Wall-clock (carves) | ~107 min | ~197 min (mean ~9 min/carve) |
| Build warnings | 0 | 0 |
| Final gate vs baseline | n/a (no baseline) | **no new failures** (1095 → 1188 tests) |
| Git-state incidents | 1 (stash dropped) | 0 |

## Playbook findings (run 3)

| # | Finding | Fix |
|---|---|---|
| 1 | **Rule change worked.** Zero STOPs; run 2's STOPs (env keys, termination handling) shipped with platform semantics recorded; no edge cases handed to the supervisor. | Keep. |
| 2 | Lenses **conflicted on composition shape** (DI lens: Host for CLI root; config lens: no Host). | D2: one composition decision per workload, applied across lenses. |
| 3 | **Config fragmented** into three roots (env @ root, env `FORGE_` in login options, JSON per load). Each carve correct alone. | D4: emit "one configuration root (JSON + env)" as the config group's foundation row. |
| 4 | **Net complexity up** on #8 (named options + `IOptionsMonitor` for a two-value check). No step weighs simplicity; the `/code-review` Check step was never run. | Add a simplicity gate per carve (simplicity-reviewer), or route facilities that add net complexity. |
| 5 | **Redundant row** #5 (setting the SDK already defaults). | Discovery checks effective defaults (`dotnet msbuild -getProperty`) before prescribing. |
| 6 | **Overlapping rows** #14/#15 (one flow split into two sub-steps). | D3: split by owner/unit, not by sub-step of one flow. |
| 7 | **Sequencing**: lengthening the grace window (#12) before tokens were wired made the interim state worse. | D4: "honor the token" before "lengthen the window". |
| 8 | Impact labels needed supervisor reclassification against the rule (2 rows). | Keep the D2 check; impact column stays advisory. |
| 9 | **Run-start baseline + git rule worked**: every gate measured against it; agents compared to HEAD via `git show`/`git archive`, never touched state. | Keep. |
| 10 | Environment failure (API/TLS) killed one carve mid-swap. | Supervisor: on agent failure, discard the partial tree and retry once; stop if it repeats. |
| 11 | Lens Learn references were sometimes the wrong page; carve-time `microsoft-docs` corrected them. | Keep grounding at carve time. |
| 12 | Pre-existing nondeterminism (`forge login` exits 1 or 130 on SIGINT) — pins can only assert non-zero. | Record races; don't pin them. |

## Routed (see routed.md)

6 to the later stage (incl. the `forge claude` three-signal-owners bug and TUI SIGTERM path), 2 to Stage 1
(dead `FORGE_API_ENDPOINT` plumbing; Stage-1 #8 test-path regression), 1 to Stage 2 Phase 2 (JSON source-gen),
3 Stage-1 product/cross-repo STOPs to the later stage.
