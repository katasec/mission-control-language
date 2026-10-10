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

Product implementation, code reviews, merge, publication, deployment and acceptance are future
boundaries. Tokens unavailable; no measured token claim.

## Plan reviews

Read-only implementer plan00:00:19–00:03:08 UTC; [complete artifact](phase-76.5-runner-process-hosting-plan.md).
Root transcription/document validation PASS14docs/88links/2JSON/fences/global hub and diff check.

| Simplicity check | Current plan r6 verdict |
|---|---|
| New apps/libraries | PASS: apt tini, no application/project dependency |
| Reuse | PASS: existing Dockerfile/workflow/tests/secret/Make |
| Multiple paths | PASS: fixed production entrypoint/shared probe; controlled cases labelled |
| Legacy paths | PASS: no fallback/handler replacement/Core edit |
| Knobs | PASS: no product setting/authority; version recheck |
| Speculative abstractions | PASS: bounded stdlib script outside payload |
| Library choice | PASS: standard init; native image observations mandatory |
| Copy-paste | PASS: existing workflow; no second publisher/test project |
| Redundant definitions | PASS: no DTO/provider/identity/durable-owner duplication |
| Size versus requirement | PASS: six paths; independent hosting increment |
| Test volume | PASS: two native images, topology/health/orphan/shutdown/exit37/missing init/default Chat |

Simplicity full review assigned00:04:46, observed00:05:02–00:05:17 UTC. Security/engineering/default
consistent; JIT-image AOT and UI N/A. No removal/merge finding. Ownership review assigned00:05:31
UTC, observed00:05:49–00:06:14 UTC; full18 behaviors/gates PASS below.

| Ownership behavior | Derived/proposed owner | Current plan r6 verdict |
|---|---|---|
| Install/fixed init→dotnet | Runner image | PASS |
| Direct signals/exit propagation | Init composed by image | PASS |
| Image/PID1/child/health proof | Runner image probe | PASS |
| Orphan adoption/kill/reap/sentinel | Runner image probe | PASS |
| Normal graceful shutdown | Runner image probe | PASS |
| Nonzero37 propagation | Runner image probe, controlled | PASS |
| Missing init/no fallback | Runner image probe, controlled | PASS |
| Deadlines/owned-container cleanup | Runner image probe | PASS |
| Managed/both-native-architecture checks | Existing image workflow | PASS |
| PR no Azure/OIDC/push authority | Existing image workflow | PASS |
| Publication verification/main gate | Existing image workflow | PASS |
| Image-only publication | Existing image workflow | PASS |
| Version/revision/registry digests | Publication owner/supervisor | PASS |
| Published version pin | forge-infra | PASS |
| Validate/merge/Make/what-if | forge-infra/supervisor | PASS |
| Live topology/default Chat | Supervisor/product owners | PASS |
| Core independent correction gates | Core, held | PASS |
| Ownership documentation | Existing Runner READMEs | PASS |

Scope/dependency/security/engineering/default plan PASS; AOT/UI N/A. Fresh duplicate search,
no existing probe/competing init, no second job. Existing image/NuGet tag triggers confirm
image-only main dispatch avoids unrelated package publication. Infra nearest AGENTS confirms
codex/ branch. No placement change. Actual image/default evidence remains pending.

Supervisor independently checked full plan, actual workflow/Dockerfile/runtime startup, infra
AGENTS/README/layer, unchanged dependencies/authority, failure/negative/default evidence and
six-path scope. **PLAN APPROVED00:06:49 UTC**; same implementer receives bounded handoff.

| Stage | Assignment start UTC | Observed end UTC | Observation |
|---|---|---|---|
| Plan / same implementer r8 | 2026-10-10 00:00:19 | 2026-10-10 00:03:08 | Complete six-file read-only plan |
| Plan review / simplicity r6 | 2026-10-10 00:04:46 | 2026-10-10 00:05:17 | Full11 PASS; observed start00:05:02 |
| Plan review / ownership r6 | 2026-10-10 00:05:31 | 2026-10-10 00:06:14 | Full18/gates PASS; observed start00:05:49 |
| Plan approval / supervisor | 2026-10-10 00:06:49 | 2026-10-10 00:06:49 | Explicit bounded approval; same implementer r5 handoff |

Design/plan record merged in [documentation PR378](https://github.com/katasec/mission-control-language/pull/378),
main`b78a3ce200929030e60143795eb76036255cd046`. This is not runtime completion.
