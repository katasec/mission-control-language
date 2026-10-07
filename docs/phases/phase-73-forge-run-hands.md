# Phase 73 — Hands in every local run

## Status

Product merged in [forge-mcl PR68](https://github.com/katasec/forge-mcl/pull/68).
Managed checks, complete independent reviews, canonical zero-warning Native AOT and installed
real-provider default acceptance PASS. [Full evidence and timing](phase-73-forge-run-hands_completed.md).
This documentation closure is merged through its own PR; task worktrees are then removed.

## Supported contract

Every forge run creates one scoped Hands session rooted at canonical cwd, grants Read/Write/Edit,
and uses the existing Core pause/execute/resume contract with one outstanding tool call. CLI owns
composition/cancellation/output; Bob owns policy and containment; Core owns interpretation and
checkpoint filtering. Existing ContextObjects preserves CLI global overrides across resumes.
There is no mode flag, tool-free alternative or terminal grant. Trusted exec experts and explicit
output declarations retain their authority. Parallel calls, MXC and Forge Chat reconciliation
are deferred in [backlog](../backlog.md).

[Default facts](../design/default-path-acceptance.md#forge-run-hands-default--phase-73),
[accepted contract](phase-73-forge-run-hands_completed.md#accepted-runtime-contract), and
[installed evidence](phase-73-forge-run-hands_completed.md#installed-default-path-acceptance).

## Stage timing

Scope/design start: 2026-10-07 21:20:41 UTC (first recorded clock observation; earlier scope
message timestamp unavailable). Subsequent stages record actual assignment/verdict boundaries.

### Live workflow table (Dubai UTC+4, 2026-10-08)

| Stage / event | Work actually performed | Status / result | Time started | Time finished |
|---|---|---|---|---|
| 0 | Scope | Done; precise times unavailable | Not recorded | Not recorded |
| 1 | Initial design and sequential reviews | PASS | 01:20:41 | 01:25:25 |
| 2 | Initial plan and sequential reviews | APPROVED | 01:25:25 | 01:30:18 |
| 3 | Initial implementation and managed checks | Done; 49 focused, 939 full, 10 skips | 01:30:18 | 01:41:47 |
| 3.1 | Initial code reviews and local AOT | Source PASS; AOT six warnings, gate blocked | 01:41:47 | 01:48:55 |
| 3.2 | First canonical native CI | Cancelled after regression made tree obsolete | 01:49:15 | 02:04:38 |
| 3.3 | Real-provider native file probe | FAIL: global --var lost; formal-parameter diagnostic later passed | Not recorded | Not recorded |
| 3.4 | RETURN TO DESIGN: investigate --var, revise contract, sequential reviews | Revised design LOCKED | 01:55:24 | 01:59:58 |
| 3.5 | Revised implementation plan and sequential reviews | APPROVED | 01:59:58 | 02:03:14 |
| 3.6 | CLI correction and OAI package-fixture attempt | Global fix works; fixture discards tool calls | 02:03:14 | 02:07:16 |
| 3.7 | RETURN TO PLAN: Anthropic fixture proposal and sequential reviews | APPROVED | Not recorded | 02:08:24 |
| 3.8 | Anthropic package-fixture attempt and diagnosis | BLOCKED: SDK requires missing caller field | 02:08:24 | 02:11:38 |
| 3.9 | RETURN TO PLAN: bounded wire fixture and sequential reviews | APPROVED | 02:12:17 | 02:15:38 |
| 3.10 | Wire fixture, valid before/after regression, final managed checks | PASS: 52 focused; 951 full, 10 skips; builds zero warnings | 02:15:38 | 02:20:36 |
| 3.11 | Corrected full simplicity/style code review | Source PASS; final native gate open | 02:20:36 | 02:23:02 |
| 3.12 | Corrected canonical CI: managed/package and native verification | PASS: artifact independently inspected | 02:20:24 | 02:50:11 |
| 3.12a | CI managed and package checks | PASS | 02:20:44 | 02:25:09 |
| 3.12b | CI Native AOT publish and warning check | CI PASS | 02:25:09 | 02:49:47 |
| 3.13 | Corrected full ownership code review | PASS | 02:23:34 | 02:24:40 |
| 3.14 | Final full simplicity/style review with native logs | PASS; start observation after assignment | 02:53:29 | 02:54:11 |
| 3.15 | Branch real-provider --steps compatibility probe | PASS: new file and Read return hands-stream-ok | 02:29:43 | 02:29:51 |
| 3.16 | Record operator's managed local-cycle / final AOT direction | Done: workflow and style docs updated | 02:31:59 | 02:33:50 |
| 3.17 | Prepare installed-default acceptance probes | Prepared; syntax checked; execution waits for merge/install | 02:38:54 | 02:41:29 |
| 3.18 | Reconcile resolved planning/investigation history | Done: history moved; current contracts/gates retained | 02:42:38 | 02:43:07 |
| 3.19 | Download and independently inspect native evidence | PASS: source/tree/version/hash/logs | 02:50:33 | 02:56:52 |
| 4 | Ready-to-merge checks | PASS; exact reviewed head/checks and approved scope | 02:56:52 | 02:57:05 |
| 5 | Merge and publish/install | PASS: main make install exit 0; local known linker diagnostics recorded separately | 02:57:05 | 03:05:24 |
| 5.1 | Preserve ignored prior accepted comparison results before eventual archive | PASS: 154 files, matching checksums | Not recorded | 03:02:46 |
| 6 | Installed default-path acceptance | PASS: real file/steps/global, outside/symlink, tool-free, Ctrl-C | 03:05:30 | 03:05:52 |
| 7 | Documentation closure and worktree cleanup | ONGOING: checkpoint and closure PR | 03:06:04 | — |

Events 3.4–3.10 explicitly record returns to design/plan and unsuccessful fixture attempts;
they were completed before advancing to gate 4. Times are recorded observations, not reconstructed
message timestamps; missing precise boundaries stay unavailable. CI overlaps local review work.

The operator additionally requested a timer subagent to report this table every five minutes,
with an actual table-publication date/time in Dubai (UTC+4) on each user-facing report.
It is a reporting-only exception to the fixed three-role team; product stages remain sequential.

