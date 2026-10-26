# Phase 1 — run summary (playbook effectiveness)

Run: `2026-10-25-084621` · Target: `forge-mcl` / `src/ForgeMission.Cli` · Branch: `adeen/dotnet-deslop-test-1`
Mode: autonomous — one implementer sub-agent per issue, one local commit each (NOT pushed), supervisor
verifies + rates. Baseline before carve #1: `ad579d7` (clean tree). Final HEAD: `72e94d3`.

## Outcome per issue

| # | Issue | Outcome | Commit | Duration | Rating |
|---|---|---|---|---|---|
| 1 | Introduce DI composition root | **STOP** (new architecture) | — | 2m51s | 1/5 |
| 2 | Unify error/exit handling | FIXED (bundled behavior change) | c30ebf7 | 13m46s | 4/5 |
| 3a | Extract InitCommand | FIXED (no class; functions) | 2180943 | 16m28s | 3/5 |
| 3b | Extract Serve/exec | FIXED (absorbs only; class deferred) | 15a466e | 18m30s | 3/5 |
| 3c | Extract ClaudeCommand | FIXED (idiom only; class deferred) | 5737703 | 9m39s | 3/5 |
| 3d | Extract McpCommand | FIXED (real class via seam) | c118f52 | 20m56s | 4/5 |
| 3e | Extract provider commands | FIXED (clean ownership) | dcef9dc | 10m37s | 4/5 |
| 3f | Extract connect command | FIXED (real class via seam) | 1597f31 | 8m40s | 4/5 |
| 3g | Extract agent/webui/clean | FIXED (3 real classes) | a3723a9 | 14m44s | 5/5 |
| 4 | Move ForgeAPI wire protocol | **STOP** (cross-repo campaign) | — | 2m51s | 1/5 |
| 5 | HttpClient → IHttpClientFactory | FIXED (self-contained factory) | 0616a83 | 13m15s | 4/5 |
| 6 | Shared ensure-initialized | FIXED (+ caught mis-grouping) | 23d4fbe | 16m33s | 4/5 |
| 7 | Orphaned ForgeRun | **STOP** (wire/delete both behavior) | — | 1m54s | 1/5 |
| 8 | Folder layout | FIXED (git mv reorg) | 71390d3 | 9m08s | 4/5 |
| 9 | Document health-poll swallow | FIXED (comment-as-contract) | 72e94d3 | 11m27s | 5/5 |

## Aggregate

- **Shipped:** 12 commits. **Stopped:** 3 (#1, #4, #7). **Forced-behavior-change bundled:** 1 (#2).
- **Total sub-agent wall-clock:** ~171 min (≈2h51m), run sequentially. Mean ~11.4 min/issue.
  - Fixed carves (full build + ~1,090-test suite): ~9–21 min. STOPs (no build): ~2–3 min.
- **Code:** `Program.cs` 1,700 → 1,207 lines (−493). Net across 25 files: +1,092 / −795.
- **Zero warnings** (`dotnet build -warnaserror`) on every shipped carve.
- **Ratings:** avg 3.5/5 (fixed-only avg 3.9/5); STOPs dragged it down, correctly.

## Playbook findings (the point of the run)

### 1. No lane for behavior-changing items → STOP-or-ship is a coin flip (SEVERITY: HIGH)
Discovery emits architecture/behavior items (introduce DI #1, change exit semantics #2, wire/delete #7,
cross-repo move #4), but Remedy is "behavior-preserving ONLY → STOP, never bundle." The same class of
item was handled **three different ways**: #1 stopped, #2 bundled-and-shipped, #7 stopped-and-recommended.
That is non-deterministic delivery. FIX: add a second lane (feature/behavior-change workflow) and have
Discovery tag each item `preserving | behavior-change | cross-repo` so routing is decided, not improvised.

### 2. B-group had an unstated prerequisite enabler (SEVERITY: HIGH)
"Extract one command per carve into a class" is blocked by ~100 file-local shared helpers in Program.cs
(`Die`, `TryParse`, `ReportDiagnostic`, …) that a separate class can't reach. 4 separate agents
independently recommended the same missing carve: **extract a shared `internal static CliHelpers`
(via `using static`) FIRST.** D4 sequencing missed it. Result: 3a/3b/3c degraded to in-file function
extraction; only 3d/3f/3g (+#5) delivered real classes, via an ad-hoc delegate-seam workaround.
FIX: Discovery must detect the shared-seam prerequisite and sequence the enabler before dependents.

### 3. Rows bundle a behavior change into a "behavior-preserving extract" (SEVERITY: MED)
#3b ("+ consolidate run+exec") and #3d (both "absorbs") packed behavior changes into extract rows, so the
row can't land atomically. FIX: D3 split must separate the preserving part from the behavior-change part
into different rows.

### 4. Discovery accuracy misses (SEVERITY: MED)
- #6 bundled `ForgeChat.ParseHands` — a TEST-ONLY helper, not a re-parse-to-run site (false positive).
- #7 mis-described: `forge run` is MIS-WIRED to hosted ForgeExec (dupes `forge exec`); ForgeRun isn't
  merely orphaned. The real defect is more interesting than the row.
- #4's "slop" is a DELIBERATE AOT-boundary duplication the code documents — arguably not a defect.
- #5's provider catalog is duplicated a THIRD place (ForgeTomlReader) with a divergence (xai) the row missed.

### 5. The verify gate dominates cost (SEVERITY: MED, efficiency)
Every carve runs the full ~1,090-test suite (minutes), often more than once. FIX: scope `dotnet test` to
affected project(s) during the loop; run full suite + AOT publish once at phase end.

### 6. The suite is not deterministically green (SEVERITY: MED, affects every verify)
Baseline failures drifted 1–5 run-to-run: env-gated live-API Integration tests (401/500) plus 2
DETERMINISTIC branch-broken tests (`TuiColourLiteralTests`) and 1 env-sensitive unit test
(`PipelineRunnerTests.MissingApiKey`). Good agents captured a baseline and diffed; the playbook should
MANDATE "capture baseline, compare deltas" (only ~half the agents did) rather than "tests green."

### 7. Process not followed: the repo's own supervisor-workflow (SEVERITY: note)
This carve loop is a bespoke lighter process, not `docs/design/supervisor-workflow.md` (no design/plan
gates, no independent simplicity/ownership reviewers, personas linked not inlined, commit without a
review-code gate). Deliberate choice vs. gap should be decided.

## Supervisor deviations from the verbatim carve prompt (logged for honesty)
- #4: added "STOP if it needs another repo; don't vendor/fabricate" (owner was an external package).
- #7: added "wire/delete are both behavior changes; STOP-and-recommend" guard.
- #8: added a "layout changed during this run" note so stale backlog locations wouldn't mislead.
- Baseline: reverted a pre-existing uncommitted Program.cs edit (stray partial of 3g) before carve #1.
