# Phase 76.5 — Hosting evidence

Verified 2026-10-10 00:37:11 UTC; exact default-path observations below close this hosting task.
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

At design lock, product implementation/reviews/merge/publication/deployment/acceptance were
future boundaries; their later observations follow below. Tokens unavailable.

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
[Publication run38008306610](https://github.com/katasec/forge-runner/actions/runs/38008306610)
head17080b73a84ad0b0e42a891024090e8f997194ed; both native merged-source verify legs SUCCESS,
build-push job114082747564 SUCCESS00:28:00 UTC. Normal merged-main image0.20.6 published.
Root downloaded named runner-image-publication artifact into handback/publication; source.txt
matches exact merged main. (Downloading every artifact encountered a non-ZIP Docker build record;
named evidence download succeeded, without changing/repeating publication.) Independent GHCR
manifestGET200 and ACR repository-show returned the same index digest00:28:50 UTC.

| Published fact | Both ACR and public GHCR |
|---|---|
| Version | forge-runner0.20.6 |
| Source | 17080b73a84ad0b0e42a891024090e8f997194ed |
| OCI index | sha256:c0031d451d046d4f28a75ca7f7c7f26169b26e92555de4b601128a005670331c |
| linux/amd64 | sha256:cf2a7e14f639519af223dd1efa0a2adf4d40cf1cac5f1b7d2017c5860ca1078b |
| linux/arm64 | sha256:ff3eab0400405722fc0ff4059624bd7617aa64600f4eaa3e5473c16cf43087c9 |

Native merged-source verify jobs114082150766/114082151008 SUCCESS00:18:43/00:18:20 UTC,
respectively. No NuGet tag/release. Approved sixth-file infra implementation assigned00:28:50;
deployment/default acceptance remain open.

| Stage | Assignment start UTC | Observed end UTC | Observation |
|---|---|---|---|
| Implement / same implementer r5 | 2026-10-10 00:06:49 | 2026-10-10 00:12:23 | Five-file frozen c6d9411, local gatesPASS |
| Code review / simplicity+style r3 | 2026-10-10 00:12:51 | 2026-10-10 00:14:09 | Full11/9PASS; observed start00:13:15 |
| Code review / ownership r3 | 2026-10-10 00:14:29 | 2026-10-10 00:15:22 | Full16/gatesPASS; observed start00:14:48 |
| Readiness / supervisor | 2026-10-10 00:15:46 | 2026-10-10 00:15:46 | Independent full gate check |
| Runner product merge | 2026-10-10 00:15:50 | 2026-10-10 00:15:50 | PR26; infra product merge remains future |

## Infrastructure image pin

Same implementer r6 assigned00:28:50, ended00:29:54 UTC. Infra
`codex/phase-76-runner-init` HEAD`0420a5281977e653e1cc5f2455b1fe3b65187be2`
against main`7d849c92b035866061418daf779a2be8fde29e96`; exactly the approved sixth path,
`dev/500-app/main.bicepparam:14`, Runner0.20.5→0.20.6. Full diff SHA256
`B57E1C7C9B0ACD7A876E960261D9B05312DAF1445AB58BF70E60C66CF3AEE104`.
[PR45](https://github.com/katasec/forge-infra/pull/45) attached. Local template/parameter
compilation and diff check PASS; existing Bicep upgrade notices retained. Normal current-source
[infra CI38009292447](https://github.com/katasec/forge-infra/actions/runs/38009292447)
SUCCESS00:31:00 UTC, all existing layers compiled. Raw local/CI evidence:
`/private/tmp/phase76-runner-hosting-20261010T000649Z/infra/`.

Full current simplicity/style r4 assigned00:30:14, observed00:30:39–00:30:57 UTC; no findings.

| Simplicity check | Current r4 verdict |
|---|---|
| New apps/libraries | PASS: none |
| Reuse | PASS: existing image parameter/deploy route |
| Multiple paths | PASS: none added |
| Legacy | PASS: pin replaced, no fallback |
| Knobs | PASS: no new setting |
| Speculative abstraction | PASS: none |
| Library choice | N/A: published image selection |
| Copy-paste | PASS: none |
| Redundant definitions | PASS: one existing parameter |
| Size | PASS: one insertion/deletion, approved sixth path |
| Test volume | PASS: existing Bicep/CI sufficient |

| Code-style check | Current r4 verdict |
|---|---|
| Progressive disclosure | PASS: named runnerImage declaration |
| Small functions | N/A: no function |
| Top-down | PASS: existing order |
| Explicit errors | N/A: no error behavior changed |
| Shallow nesting | N/A: literal |
| Side effects | PASS: existing Make boundary |
| Zero warnings | PASS: no template diagnostics; existing tool upgrade notices disclosed |
| Extraction | PASS: none |
| Complexity | N/A: no control flow |

Full current ownership r4 assigned00:32:48, observed00:33:12–00:33:47 UTC. Technical and
placement PASS; no blocker/move. Full current verdict:

| Behavior / gate | Derived and actual owner / observation | Verdict |
|---|---|---|
| Deployed image selection | Infra500-app parameter | PASS |
| Registry/container resolution | Existing unchanged Bicep | PASS |
| Init/signals/orphans | Published Runner image; no infra wrapper | PASS |
| Input validation | Existing infra CI | PASS |
| Preview/apply | Existing Make; supervisor | Correct owner; pending |
| Live topology/default Chat | Supervisor through existing product | Correct owner; pending |
| Scope/compatibility | One approved path/line | PASS |
| Dependency/provenance | Merged source/normal publication/both native legs | PASS |
| Published identity | Both registries/index/platform manifests agree | PASS |
| Compilation | Local/current CI13layers | PASS |
| Security | Existing ingress/identity/secrets/data boundaries | PASS |
| Engineering/failure | One existing deployment path, no fallback | PASS |
| Deployment/default | Actual what-if/deploy/Chat still required | Open |
| AOT/UI | Parameter-only | N/A |

Root readiness00:34:04 UTC: exact clean HEAD/diff/checks independently checked; all findings
clear, approved scope/ownership/contracts/failure/security retained; AOT/UI N/A. PR45 merged
00:34:10 UTC to`fa49f916e85eb637f735e2b9e61e57ea26b3de8d`; clean/current infra main.
Mandatory Make what-if started after merge; inspection/deploy/default acceptance remain open.

Supervisor `make 500-app-what-if` exited0; full raw`infra/what-if.log`. Two resources predicted
modified, two unchanged,23ignored: intended Runner image0.20.5→0.20.6; other differences are
unresolved Bicep reference expressions and API-owned `runningStatus`/`exposedPort`. Before
deploy, independent restricted Azure queries confirmed exact referenced Host/Runner/Billing
FQDNs and Runner identity clientId match their current values. No datastore/identity/secret/role
change is predicted; alert rules unchanged. Root approved this observed preview and started
only `make 500-app` after those checks; raw`infra/deploy.log`, completion pending. Exact
approval/start clock boundary unavailable; no timestamp inferred from the earlier query clock.

## Default-path acceptance

Supervisor observed Make apply exit0, then restricted deployment query `main` Succeeded,
timestamp2026-10-10T00:35:44.364926+00:00, correlation0a8a524c-96cc-410b-aae6-f9fe56d1c206.
Ready/latest revision both`ca-forge-runner-dev--0000043`, provisioningSucceeded, image
`crforgeroomsdev.azurecr.io/forge-runner:0.20.6`. Observation stage began00:36:14 UTC.
Read-only `az containerapp exec` on replica`ca-forge-runner-dev--0000043-7f969f6db-4q56l`
returned exit0 and actual `/proc` facts: init`tini`, PID1; direct child`dotnet`, PID7,
command`dotnet ForgeMission.Runner.dll`. Assembly bytes contain exact merged source
`17080b73a84ad0b0e42a891024090e8f997194ed`. Raw observations under handback`infra/`:
`deployed-image.json`, `deployment-observation.json`, `process-observation.txt` (tool-output
transcription); full normal Make output`deploy.log`. No secret/environment values inspected.

The prior live image0.20.5/revision42 was independently observed with `/proc/1/comm=dotnet`
at2026-10-09 23:54:16 UTC. Its actual .NET10 bare-PID1 reaper conflict is recorded in the
[Core failure evidence](phase-76.4-core-cloud-primitives_completed.md#verification-correction-r4-and-linux-container-failure).
The new topology is an actual observation, not inferred from the Dockerfile.

| Required default fact | Supervisor observation |
|---|---|
| Artifact | Installed `/Users/ameerdeen/.local/bin/forge`, version0.10.1-dev.0+8d28dc1ff8f127facfd708b1c89369179e98cf18; SHA25618a18adde627dd49ff37aae6d6a7d1a9910b82c0a90e635eb933e47a15a24340 |
| Defaults | FORGE_API_ENDPOINT, FORGE_PLATFORM_ENDPOINT, RID, RELEASE_TAG, CLI_OUTPUT absent; normal saved login/ForgeAPI |
| Dependency | Normal ACR image0.20.6, immutable index/platform/source above; merged infrafa49f916, Make deployment/revision43/process facts above |
| Starting state | Fresh disposable `/private/tmp/phase76-hosting-default-20261010T003630Z`; Projectad4abac3-4a08-42bf-af3c-9f6acdf1f556; Chat@1, emptyfolders |
| Action | Installed `forge project create`, then plain piped `forge chat` with unique exact-response prompt |
| Result | Both exit0; actual `[Chat:Answerer]` body exactly PHASE76_HOSTING_OK_20261010T003630Z; no stderr; projection identifies conversationed7c1acb-747e-5c55-8f06-4aea67c97a84 |
| Raw proof | Disposable directory observation.json/version/create/chat stdout/stderr; supervisor inspected actual answer separately from request echo |
| Controlled evidence | Native image probe cases above are supporting component proof; no default override/stub/entrypoint replacement used for installed Chat |

All hosting Done-when conditions now PASS. Core code/package/native/publication/default gates
remain separate and open; hosting acceptance does not claim unified execution is delivered.
Runner and infra both clean/current main, no task worktree. MCL closure PR records this evidence.

## Final stage boundaries

Earlier full design/plan/Runner implementation/review tables remain above. Product PR timestamps
come from `gh pr view`, not agent lifetimes. Tokens unavailable.

| Stage | Start UTC | End UTC | Wall | Evidence |
|---|---|---|---|---|
| Scope→last product merge | 2026-10-09 23:52:24 | 2026-10-10 00:34:10 | 41m46s | Runner26 + infra45 |
| Runner product PR | 2026-10-10 00:10:29 | 2026-10-10 00:15:50 | 5m21s | source c6d9411→main17080b73 |
| Infra implement r6 | 2026-10-10 00:28:50 | 2026-10-10 00:29:54 | 1m4s | approved one-line pin |
| Infra simplicity/style r4 | 2026-10-10 00:30:14 | 2026-10-10 00:30:57 | 43s | observed start00:30:39, full11/9PASS |
| Infra ownership r4 | 2026-10-10 00:32:48 | 2026-10-10 00:33:47 | 59s | observed start00:33:12, full gatesPASS |
| Infra readiness | 2026-10-10 00:34:04 | 2026-10-10 00:34:04 | Boundary | independent diff/checks/security/default review |
| Infra product PR | 2026-10-10 00:29:47 | 2026-10-10 00:34:10 | 4m23s |0420a528→mainfa49f916 |
| Make preview/apply | Exact start unavailable | 2026-10-10 00:36:14 | Unavailable | reviewed preview, apply exit0 available before observation start |
| Postmerge supervisor acceptance | 2026-10-10 00:36:14 | 2026-10-10 00:37:11 | 57s | actual image/process/source + installed Project/ChatPASS |

Closure documentation validation/PR is outside the product timing span. Next: full revised Core
implementer plan against the locked init split and exact published image; no mechanism edit
until full sequential plan reviews and supervisor approval.
