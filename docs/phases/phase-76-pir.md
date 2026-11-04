# Phase 76 — post-incident review: where the time went

Phase 76 took **13 h 34 min** of wall time (2026-11-03 12:12 UTC, forge-client PR 17 opened, to
2026-11-04 01:46 UTC, mission-control-language PR 412 merged). The product code was small. This
review says where the time went and what to change. Sources: `gh pr view` for 28 merged PRs,
`gh run list` for every Actions run, and the session transcript's agent-completion records.
Stage times were not recorded live as [task-timing](../../tools/task-timing/README.md) requires;
that is itself finding 6.

## Wall time by part

| Part | UTC | Wall | Product PRs | What happened |
|---|---|---|---|---|
| Step 1: delete pinning | 12:12–13:33 | 1 h 21 | forge-client 17–19, forge-mcl 89 | PR 18 broke chat reconnect and was published before forge-mcl built against it; PR 19 restored it and republished. |
| Design clarification | 13:33–16:05 | 2 h 32 | docs only (402–409) | The step-2 design was not settled before work started. I proposed scope from wrong assumptions (that chat was not in the cloud, that `forge chat` needed a mission argument, a `--hands` mode); the operator corrected each. |
| Step 2: one client path | 16:05–21:31 | 5 h 26 | forge-client 20, forge-mcl 90 | 12 rows run one at a time, each implement → verify → check. 7 STOPs (4 caused by gaps in my briefs), 6 Check fails, a TUI rework (M1 r2–r5). |
| Config defaults | 21:31–22:43 | 1 h 12 | forge-mcl 91 | Three rows; one operator decision (fail on an unwritable config). |
| Delete `Executable` | 22:43–00:49 | 2 h 06 | 6 PRs in 5 repos, 2 image builds, 2 deploys | 58 lines of product code. The time was the release chain: Contracts → Core → Client → Host/Runner → images → deploy → CLI, each waiting on the previous publish. Two order problems needed operator calls. |
| Verbose flag, two fixes, whole-phase check | 00:49–01:46 | 0 h 57 | forge-mcl 94–95, forge-platform 19 | The check found two faults that predated today (ForgeAPI dropped package files; OCI pull rejected tar directory entries). Both fixed and deployed in this window. |

## Where the minutes were spent

| Bucket | Minutes | Note |
|---|---|---|
| Implementer agent rounds | 229 (50 rounds) | Includes repeated full-suite runs to rule out flakes (10–20 runs per row at times). |
| Check agent rounds | 45 (30 rounds) | Each round re-reads the full diff. |
| Lens agent rounds | 21 (4 rounds) | |
| GitHub Actions | 158 (48 runs) | Release workflows (they also run PR CI in forge-mcl) 87, package publishes 41, images 18, other PR CI 12. |
| Deploys | ≈ 30 | `make 525-conversation-app`, `500-app`, `550-api`, each with a what-if and a revision wait. |
| Operator conversation | the rest | About 217 operator messages: corrections, decisions and status requests. |

Agent and CI minutes overlap with each other and with conversation, so they do not add to the wall
total.

## Findings

| # | Finding | Cost | Change |
|---|---|---|---|
| 1 | Work started before the design was settled; I filled gaps with assumptions the operator then had to correct. | ≈ 2.5 h of clarification plus rework in step 2 | Before a step, list every open question from the spoke and ask them in one batch; build nothing that depends on an unanswered one. |
| 2 | Rows ran strictly one at a time, each with a full verify and check, even for one-line rows. | ≈ 5 h for step 2 | Scale the loop to the row: a version bump or doc line gets build + one test run and a short check; keep the full loop for behaviour changes. Run independent repos' rows in parallel. |
| 3 | Same-repo steps were run as separate cycles (config defaults and the verbose flag are both forge-mcl CLI). | One extra lens, CI and release (≈ 30 min) | Group remaining steps by repo and release at Select. |
| 4 | Cross-repo package changes were released one link at a time, and two order problems were found mid-chain. | ≈ 2 h for 58 lines | Before a cross-repo change, map the package graph and publish order and check it for cycles; include the release chain in the estimate. Root fix: the forge-mcl Core/Client cycle ([backlog](../backlog.md)). |
| 5 | My briefs were wrong or incomplete: 8 STOP or FAIL rounds traced to the brief (missing expert text, a field nobody needed, a test that proved nothing, a script left un-bumped). | ≈ 1 h | Before sending a brief, check each named symbol, file and value exists and each requested test would fail on the old code. |
| 6 | Steps were not sized in wall time up front, and stage times were not recorded live. | The operator could not see the cost early enough to change the process. | Give one wall-clock estimate, including releases and deploys, before each step; record stage start and end as task-timing requires. |
| 7 | The default-path check came last, so two faults that predated the phase (ForgeAPI on Contracts 0.10.0; OCI tar directory entries) were found at the end. | ≈ 40 min, late | Run one real `forge run` of a script mission and an OCI mission on the default path at the start of a phase, to find standing faults before building on them. |

## What worked

- Reproducing before fixing: the ForgeAPI and OCI faults were each pinned to one cause with a
  local probe before any change, and each fix was one small PR.
- Live checks on the installed binary caught what unit tests could not (the ForgeAPI drop, the
  OCI bundle shape).
- Every product change landed through PRs with green CI, and every deploy went through what-if.
