# Efficiency notes — for designing the next run

Purpose: capture where time goes per carve, so the next run can be faster without losing quality.
Updated as the run proceeds.

## Per-carve timings

| Run | Carves | Mean per carve | Notes |
|---|---|---|---|
| 2 | 11 | ~9.75 min | no Check step |
| 3 | 22 | ~9.0 min (5m32s–23m22s) | no Check step; total ~197 min |
| 4 | in progress | see below | Check step added |

Run 4 so far:

| Row | Carve | Correction | Check(s) | Total |
|---|---|---|---|---|
| 1 one host | 12m00s | 5m13s | 2m05s + 0m58s | ~20m |
| 2 config root | 18m39s | — | 1m28s | ~20m |
| 3 IHttpClientFactory | 11m18s | 6m19s | 1m06s + 0m52s | ~20m |
| 4 conversation-host | 8m24s | 4m37s | 0m35s + ~1m | ~15m |
| 5 mcp on host | 12m13s | — | ~1.5m | ~14m |
| 6 endpoint IOptions | 10m46s | 4m23s | ~1m + ~1m | ~17m |
| 7 theme validator | 8m35s | — | ~1m | ~10m |

Projected: 24 rows × ~18–20 min ≈ **7–8 hours** sequential.

## Where the time goes (from carve reports)

1. **Full test suite, several times per carve.** `dotnet test` is ~1m40s–4m. Agents run it before
   the swap (pins on baseline), after the swap, and often again after fixes. When the live
   integration tests aren't skipped, each takes **~2m50s–3m20s** on its own.
2. **Re-reading the same context every carve.** Each fresh agent re-reads 3 persona/design docs,
   the backlog row, and the same ~1200-line `Program.cs` before touching anything.
3. **Building test scaffolding.** Process-level pins need a runner, HOME isolation and HTTP
   stubs; until row 3 every carve built its own (now a shared `ForgeProcess` helper).
4. **Strictly sequential.** Every row waits for the previous one, even when they touch
   different files.
5. **Corrections.** The Check step fails ~2/3 rows on first pass so far (real findings), each
   costing a correction (~5–6 min) + re-check (~1 min). Worth it; but cheaper if carves avoid
   the failure up front.

## Improvement hypotheses for next run

| # | Idea | Expected saving | Risk |
|---|---|---|---|
| E1 | **Scope tests in-carve**: agents run only affected test classes + new pins; the supervisor runs the full suite once at Verify (or once per group) | large (~half of carve time) | a cross-cutting break surfaces one row later; caught by the per-row Verify |
| E2 | **Exclude env-dependent live tests** from carve/verify runs (test trait/filter); run them once at run end | ~3–6 min whenever they run | none for Stage 2 (they never pass in this env anyway) |
| E3 | **Reuse one implementer per group** (supervisor-workflow: reuse role threads) instead of a fresh agent per row | re-reading context each row | context grows; mitigate by group-sized threads |
| E4 | **Row 0: shared test harness** before any carve (process runner, isolation, stubs) | avoids per-carve scaffolding + Check FAILs on copies | none |
| E5 | **Parallel rows on disjoint files** (separate worktrees, merged in order); D4 marks which rows share files | wall-clock, possibly 2–3× on later groups | merge conflicts in shared files (Program.cs) — only for genuinely disjoint rows |
| E6 | **Pre-empt common Check FAILs** in the carve prompt (e.g. "no consumer-less pipelines", "no default log noise", "don't duplicate harness") | fewer corrections | prompt grows |
| E7 | **Batch trivial rows** (one-line settings) under one carve while keeping one commit each | agent startup overhead | blurs "one practice per carve" — keep commits separate |
| E9 | **Mechanical no-live-calls guard** — a prompt rule wasn't enough (row 1: real request with stored creds; row 5: placeholder key sent to OpenAI). The shared harness should force every probe/test through `HTTP(S)_PROXY` = a non-forwarding local proxy | safety, not speed | none |
| E10 | **Pin budget.** Check FAILs are shifting from product code to tests (rows 3, 6: copied harness, near-copy tests, theories re-proving rules already covered). Carve prompt: pin only behavior this carve touches; one test per new path; extend existing test files, never near-copy | fewer corrections; leaner suite | under-pinning — the Check still reviews coverage |
| E8 | **Carves pre-empt later rows** (row 4 started row 6's IOptions move). Give the carve prompt the next rows so it stays in its lane; give the Check the row sequence so it can tell scope-bleed from transitional state | fewer Check FAILs/corrections | prompt grows slightly |

To revisit at run end with full per-row data.
