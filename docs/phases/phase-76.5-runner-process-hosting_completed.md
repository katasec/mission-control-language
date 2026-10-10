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

## Runner implementation handback

Same implementer r5 boundary00:06:49–00:12:23 UTC. Five approved Runner paths, 301insertions/
4deletions, frozen clean/pushed`c6d9411c471a8d105389eea6da7f244f1e484b9a`; attached draft
[PR26](https://github.com/katasec/forge-runner/pull/26). Infra and four-file Core HOLD unchanged.
Actual Core pin0.1.7, no application/package/wire/identity/permission change. Full raw commands,
logs and complete diff: `/private/tmp/phase76-runner-hosting-20261010T000649Z/EVIDENCE.md`.
Complete.diffSHA256`8084071D4691049E560892005978F2F1D29F567AE601BC79832EA21FA4D4E1C7`.

| Observation | Result / raw file |
|---|---|
| Normal restore | PASS restore.log |
| Normal Release solution -warnaserror build | 0warnings/errors build.log |
| Full normal Runner tests, MCL_API_KEY absent | 115PASS/0fail/0skip tests.log |
| Python/main +5embedded scripts | PASS python-syntax-final.log / embedded-syntax.log |
| Workflow YAML syntax | PASS yaml-syntax.log; auxiliary Ruby hostPATH-mode warning disclosed, no suppression or permission edit |
| Local Docker | Absent daemon docker-local.log; no lifecycle PASS inferred |
| Final source native image CI | run38007944173 in progress, both native hosts at actual hosting probe |
| Existing NuGet PR verify | run38007944090 in progress; no package publication requested |

Supervisor independently read full five-file frozen diff, matched inventory/hash and raw build/
test results; no additional concrete blocker established. Sequential full code review begins
00:12:51 UTC. Earlier31505a5 CI is not final-source evidence. Product merge, publication,
infra pin/deployment, actual image proofs and default Chat remain pending.

## Native image verification

Current-source run38007944173 completed SUCCESS; native amd64 job114080991180 and arm64
job114080991122 each built the actual Dockerfile, passed115tests/0skips/0warnings and observed
all hosting cases PASS00:13:18 UTC. Existing NuGet verify38007944090 SUCCESS. Both publication
jobs were correctly SKIPPED for the PR. Root downloaded raw native-amd64.log/native-arm64.log
and native-artifacts into the handback directory and inspected actual identities/commands.

| Fact | amd64 / arm64 observation |
|---|---|
| PR source | c6d9411; GitHub test-merge revision labela61b2e8876eb6c9baedc50cc9370b6bc1e960ce3 |
| Local image ID amd64 | sha256:f3864259f7ea82ec5bf549ad11fffcb7863dff4a930f962bfb9c7af7a533479e |
| Local image ID arm64 | sha256:cae5273866e2ecbef0247d7adb3a923f3c416a16d6e09d19004e0d355f914196 |
| Entrypoint | Exact locked tini--dotnet Runner DLL, both |
| Topology | tiniPID1, dotnet direct childPID7, both |
| Init package | tini0.19.0, apt0.19.0-1, both |
| Actual lifecycle | Health, orphan adoption→termination→process-entry reap/no sentinel, SIGTERM/graceful0, controlled37, missing-init/no fallback PASS, both |

These are controlled premerge actual-image observations, not publication/deployment/default
acceptance. Full current code reviews and the remaining gates still apply.

## Runner code reviews

Full current c6d9411 simplicity/style review assigned00:12:51, observed00:13:15–00:14:09 UTC.
No blocking finding; no removal/change required. Full diff/raw logs and reuse searches checked.

| Simplicity check | Current r3 verdict |
|---|---|
| New apps/libraries | PASS: distribution init/existing apt |
| Reuse | PASS: existing image workflow/tests/publication |
| Multiple paths | PASS: fixed production entrypoint, controlled cases separate |
| Legacy | PASS: no bare-dotnet fallback/duplicate reaper |
| Knobs | PASS: no product configuration |
| Speculative abstraction | PASS: bounded probe outside payload |
| Library choice | PASS: both actual native images prove tini behavior |
| Copy-paste | PASS: shared checked Docker operation/cohesive scenarios |
| Redundant definitions | PASS: no public contract/runtime owner duplication |
| Size | PASS: five approved Runner paths301/4; infra deferred |
| Test volume | PASS: required topology/shutdown/exit/orphan/missing-init facts |

| Code-style check | Current r3 verdict |
|---|---|
| Outline first | PASS: intent/scenario flow first |
| Small functions | PASS:5–20lines |
| Top-down | PASS: main/scenarios/helpers |
| Explicit errors | PASS: checked timeouts/aggregated owned cleanup |
| Shallow nesting | PASS:≤2levels |
| Side effects | PASS: named Docker/evidence boundary |
| Zero warnings | PASS: local/both native managed0warnings/errors; AOT N/A |
| Extraction | PASS: actual scenario/observation/cleanup seams |
| Complexity | PASS: classicMcCabe PythonAST max4(remove_containers), others1–3; assertions/Boolean operators excluded |

Actual PR merge-test revision parents95d677c+c6d9411 verified. Native lifecycle/full115tests
PASS on both hosts, existing NuGet verifyPASS, publication skipped. Actions tooling deprecation
messages are disclosed separately from compiler warnings. Publication/deployment/default remain
open. Full ownership review assigned00:14:29, observed00:14:48–00:15:22 UTC; complete current
technical/placement PASS below, no concrete blocker or move required.

| Ownership behavior | Derived/actual owner | Current r3 verdict |
|---|---|---|
| Distribution init install | Existing Runner apt stage | PASS |
| Fixed init→dotnet | Runner Dockerfile entrypoint | PASS |
| Image/platform/source/entrypoint facts | Runner probe | PASS |
| Normal readiness | Runner probe | PASS |
| Actual PID1/direct child | Runner probe | PASS |
| Orphan adoption/termination/reap/sentinel | Runner probe | PASS |
| Normal SIGTERM/graceful exit | Runner probe | PASS |
| Nonzero child propagation | Runner probe, controlled | PASS |
| Missing-init refusal/no fallback | Runner probe, controlled | PASS |
| Bounded operations/all-owned cleanup | Runner probe | PASS |
| Managed/both native images | Existing image workflow | PASS |
| PR authority restrictions | Existing image workflow | PASS |
| Verify/main-before-publication gate | Existing image workflow | PASS |
| Multiarch publication/digest record | Existing image workflow | PASS; execution pending |
| Ownership documentation | Existing Runner READMEs | PASS |
| Published pin/deploy/default Chat | Infra/supervisor, deferred sixth path | Correct owner; pending |

Full scope/security/failure/engineering/native gates PASS; AOT/UI N/A. Actual five-file diff,
hash/clean tree and PR merge-test parents independently confirmed. Fresh named-repository search:
no duplicate init/probe, no owner second job. No public/data/identity expansion; existing secret
seam preserved. This verdict does not close publication/deployment/default acceptance.

## Runner merge and publication

Supervisor readiness00:15:46 UTC: all code-review findings clear, exact approved five-file
source c6d9411 clean/pushed, current checks green, full diff/raw observations checked, unchanged
contracts/authority, JIT-image AOT/UI N/A. [PR26](https://github.com/katasec/forge-runner/pull/26)
merged00:15:50 UTC to`17080b73a84ad0b0e42a891024090e8f997194ed`; Runner clean/current main.

Exact prepublication checks00:16:03 UTC: GHCR manifest0.20.6 HTTP404, ACR explicit tag-does-not-exist,
Git tag forge-runner-v0.20.6 HTTP404. No authorization error inferred as absence. Supervisor
dispatched only forge-runner-image.yml from merged main with version0.20.6; no tag/NuGet release.
Publication result/digests remain pending.

| Stage | Assignment start UTC | Observed end UTC | Observation |
|---|---|---|---|
| Implement / same implementer r5 | 2026-10-10 00:06:49 | 2026-10-10 00:12:23 | Five-file frozen c6d9411, local gatesPASS |
| Code review / simplicity+style r3 | 2026-10-10 00:12:51 | 2026-10-10 00:14:09 | Full11/9PASS; observed start00:13:15 |
| Code review / ownership r3 | 2026-10-10 00:14:29 | 2026-10-10 00:15:22 | Full16/gatesPASS; observed start00:14:48 |
| Readiness / supervisor | 2026-10-10 00:15:46 | 2026-10-10 00:15:46 | Independent full gate check |
| Runner product merge | 2026-10-10 00:15:50 | 2026-10-10 00:15:50 | PR26; infra product merge remains future |
