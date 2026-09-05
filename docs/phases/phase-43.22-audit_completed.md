# Phase 43.22 — completed design investigation

> Investigation completed 2026-09-05; feature defects are not fixed. This evidence supports the
> [replacement design](phase-43.22-project-mission-reconstruction.md), not release approval.

## Provenance and corrected conclusion

Comparison: 43.20 `50930df92ce3dc5df1be1f82286899aa673e846a` to candidate
`4dbd8be9683fa01571f5402d8f3c2c31c3e60538`. Current main when this plan began:
`7cf8f376de398f2c09ee5edaf39bcfb445508f6c`. The candidate was inspected and tested in a detached
worktree; deletion and locking experiments used disposable copies. They did not alter deployed
code or the source branch. No assumption that those temporary files persist is needed to build
from the new spokes.

The increment is 88 files: 33 application C#/Razor, 5 stylesheets, 6 mission/build assets,
12 tests, 23 SVGs, 9 documents/rules. Application C#/Razor added 2,124 lines; source plus styles
added 2,455. Main-to-candidate additionally includes the earlier Explorer/OCI work and is a
different comparison. File count alone does not establish design quality.

The justified decision is to implement feature coordination afresh and selectively borrow focused
pieces. Evidence for keeping the preexisting engine does not justify keeping the new feature's
coordination structure. The five reconstructed responsibilities are manifest transactions,
submission ownership, thin endpoints, run read state and Home's mission/focus logic. Naive's
policy also needs replacement. Full source disposition: [file matrix](phase-43.22-file-boundary.md).

## Findings and observations

| Finding | Evidence / implication |
|---|---|
| Runs UI exists | Native screenshots and controlled runs showed it. Earlier claim that it was unbuilt was wrong; live existence did not prove the correct build/default provenance or visual quality. |
| Manifest race | Actual candidate ProjectStore overlapping selection/container writers: 60 forced concurrent pairs failed; 22 produced malformed manifests. Shared fixed temp and separate read/modify/write sequences are unsafe. |
| Localized persistence fix feasible | Same candidate setters wrapped in a whole-update stable-file lease: 60 same-process pairs and 60 projects written by two separate .NET processes retained both values, no failures. macOS local filesystem only; no power-loss/cross-platform claim. |
| UI coupling | Home grew 713 to 906 physical lines. RunMissionAsync mixed 83 lines of submission/buffering/navigation concerns; estimated CC 15. Completion scheduled focus while Trace had no composer; controlled browser reported invalid-element focus. |
| Parallel writable route | Old Project Control and new Project Mission both admitted work; removal task remained undone. No second datastore/queue/run engine was found. |
| Retry/error coupling | Selection was reread while command identity lived in the UI; 409 classification parsed English reason text. |
| Lost reopening model | Current-session-only run registration and unused manifest Runs projection disconnected the visible lists from persisted history. The old plan explicitly deferred history; this new scope brings bounded restoration in. |
| Wrong Naive policy | Asset retained goal-refinement/implementation-refusal behaviour. Actual run response reflected that policy; executor reuse itself was sound. |
| Unused/duplicated glue | Unused SelectMission wrapper/default helper; stale two-caller comments; duplicated catalog literals. ToolHandOff still had a real compatibility caller and was not dead code. |

## Verification results retained from audit

| Experiment | Observed result | Limit |
|---|---|---|
| Candidate Host/Worker | 191 Host + 76 Worker tests passed | Host used real Azurite; Worker expert fixtures were fake; not full product acceptance |
| Durable history replay | HTTP 200; 24 strictly ordered events across three earlier completed runs | Read-only replay, no new execution; public events alone omit per-run mission identity |
| Focused UI/runtime baseline | 175 passed | Does not prove browser focus, concurrency or release readiness |
| Disposable legacy-removal builds | Client Runtime, Host, Worker: zero errors/warnings | Mechanical experiment, not a shippable patch or safe deployment migration |
| Post-removal focused suites | 286 passed = 115 UI/runtime + 55 store + 6 compatibility + 49 Host + 59 Worker + 2 HTTP | Obsolete legacy positive cases removed/adapted explicitly; not whole-solution results |
| Actual Host HTTP after removal | Old create POST 405, old submit POST 404; Janus and Naive new starts 202 with real IDs | Fake queue dispatch: acceptance proof, not live provider completion |
| Full audit environment | Rooms 56 passed / 41 failed; Runner 4 passed / 1 failed; ForgeMission.Tests compilation blocked by sibling project paths | Candidate was not release-ready; passing focused suites did not overrule failures |

Original local evidence was written under `/private/tmp/mcl-design-audit/`: `metrics.json`,
`race-results.json`, `lease-results.json`, `process-lock-results.json`, `replay-verification.json`,
`reuse-tests/*.trx`, `boundary-tests/results`, `boundary-after/results`, `after-host-tests/results`,
`after-worker-tests/results`, and isolation build logs. This is historical provenance, not a
required implementation dependency. Implementation must produce fresh committed evidence.

## Complexity/deletion interpretation

Roslyn estimate counted base one plus branches/loops/catches/conditional and boolean operators,
with nested callables separate; it is not control-flow-graph complexity. Existing grain
RecordProgress stayed 16→16; Worker major handlers stayed 11→11. No newly pathological asymptotic
algorithm was demonstrated. The problem was coupled responsibility and competing state ownership,
not every switch with ten cases.

The conservative deletion inventory was 516 physical production lines across 17 files, mostly
older legacy code; only 18 were added/changed in the audited increment. That was inadequate grounds
for promising meaningful reconstruction by deletion percentage. Five affected source files
contained 829 of the increment's 2,124 added C#/Razor lines (~39%); affected area is not net deletion.
The replacement plan requires actual old-owner deletion/replacement and an implementation diff
report, not an arbitrary percentage target.

## Plan-document verification (2026-09-05)

Documentation/reference-only delivery: no application build or test run was needed. Checked
11 new/routing documents, 70 local links, balanced code fences and absence of unresolved markers;
all seven selected/new SVGs parsed as XML. Rendered and inspected the three new history/recovery/trace
reference frames. Reviewed the diff and main hub against the no-subphase-detail rule. This proves
the handoff artifacts are internally navigable; it is not implementation or visual product acceptance.
