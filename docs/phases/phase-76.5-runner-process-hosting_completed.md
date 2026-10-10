# Phase 76.5 — Hosting evidence

The task is open; this file holds resolved review detail, not a completion claim.
[Active design](phase-76.5-runner-process-hosting.md).

## Design reviews

Initial r8 reviews found one shared defect: hosting closure depended on the subsequent Core
correction. Supervisor separated the downstream Core gate; the complete revised design received
fresh full r9 reviews below. Actual image/deployment/default evidence remains required.

| Simplicity check | Current r9 verdict |
|---|---|
| New apps/libraries | PASS: distribution tini, no new application/product package |
| Reuse | PASS: runtime apt stage, existing workflow and infra Make |
| Multiple paths | PASS: one fixed init entrypoint, subsequent Core rejects bare PID1 |
| Legacy paths | PASS: no fallback; superseded scan removed in separate Core plan |
| Knobs | PASS: no mode, privilege or group-forwarding option |
| Speculative abstractions | PASS: existing distinct container/direct-root owners |
| Library choice | PASS: standard init; actual image probe still required |
| Copy-paste | PASS: existing build/publication route |
| Redundant definitions | PASS: no public/credential/durable-owner change |
| Size versus requirement | PASS: independently closing hosting increment |
| Test volume | PASS: topology, exit, signals, orphans and hosted Chat; Core separate |

| Ownership behavior | Derived/proposed owner | Current r9 verdict |
|---|---|---|
| Install init | Runner image | PASS |
| Init PID1/dotnet direct child | Runner image | PASS |
| Direct-child signal forwarding | Container init | PASS |
| Child exit propagation | Container init | PASS |
| Orphan reap | Container init | PASS |
| Visible startup failure/no fallback | Runner image/startup | PASS |
| Atomic direct-root/group lifetime and I/O | Core | PASS |
| Bare Linux PID1 prelaunch refusal | Subsequent Core correction | PASS |
| Superseded adopted-scan removal | Subsequent Core correction | PASS |
| Unrelated managed child ownership | Core/runtime | PASS |
| Both architectures/build/publish | Existing Runner image workflow | PASS |
| Image selection/deployment | forge-infra | PASS |
| Independent hosting acceptance | Supervisor/product default | PASS |
| Published image downstream Core proof | Separate Core verification | PASS |
| Durable content/identity/public contracts | Existing Host/platform, unchanged | PASS |

Both reviewers: dependency/security/engineering/default design PASS, UI N/A. Ownership derived
from atlas and component/repository READMEs; fresh named-Forge-repository search found no competing
init/subreaper. No second job or new component. Nothing to move/remove. Neither review claims
actual image or default-path success or approves implementation.

| Stage | Assignment start UTC | Observed end UTC | Observation |
|---|---|---|---|
| Supervisor design | 2026-10-09 23:52:24 | 2026-10-09 23:53:00 | Initial scoped proposal |
| Simplicity design r8 | 2026-10-09 23:53:00 | 2026-10-09 23:55:00 | Closure dependency REVISE |
| Ownership design r8 | 2026-10-09 23:55:17 | 2026-10-09 23:56:21 | Same finding; observed start23:55:37 |
| Supervisor revision | 2026-10-09 23:56:58 | 2026-10-09 23:56:58 | Separated downstream gate; validation PASS |
| Simplicity design r9 | 2026-10-09 23:58:55 | 2026-10-09 23:59:18 | Full11 PASS; observed start23:59:13 |
| Ownership design r9 | 2026-10-09 23:59:29 | 2026-10-10 00:00:00 | Full15/gates PASS; observed start23:59:45 |
| Supervisor design lock | 2026-10-10 00:00:19 | 2026-10-10 00:00:19 | Independent full artifact check; DESIGN LOCKED |

Product plan, approval, implementation, reviews, merge, publication, deployment and acceptance
are future boundaries. Tokens unavailable; no measured token claim.
