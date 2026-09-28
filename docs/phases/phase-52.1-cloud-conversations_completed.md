# Phase 52.1 — Cloud conversations: completed tasks

Active spoke: [phase-52.1-cloud-conversations.md](phase-52.1-cloud-conversations.md).

## Task 1 — forge-runner absorbs the Worker

**Done 2026-09-29.** Supervised under the [supervisor workflow](../design/supervisor-workflow.md);
merged by the supervisor after independent verification.

| Repo | PR | Change |
|---|---|---|
| forge-runner | [katasec/forge-runner#6](https://github.com/katasec/forge-runner/pull/6) | Worker's queue consumer, processor, and executor moved in (namespace-only edits except the consumer's per-message usage factory); `RunnerExpertRunnerFactory` shared by `/run` and the queue entry point; queue entry point registered only when Service Bus settings are present; runtime consumer faults cannot stop the web host. |
| forge-infra | [katasec/forge-infra#16](https://github.com/katasec/forge-infra/pull/16) | `kind-up.sh` builds the `mission-worker` role from a clean forge-runner `main`; stops building the Worker image. |
| forge-conversations | [katasec/forge-conversations#3](https://github.com/katasec/forge-conversations/pull/3) | Worker project, tests, and Dockerfile deleted; Host test edits only; Host production code unchanged. |

**Evidence**

- forge-runner: 37/37 tests pass (13 existing, 18 moved, 6 new), re-run by the supervisor. The
  fault-isolation test fails with the containment removed and passes with it restored. The `/run`
  refactor moves the old `BuildRunner` body verbatim into the factory.
- forge-runner CI initially failed with 403 on `Katasec.Forge.Conversations.Contracts`; fixed by
  granting forge-runner read access under the package's "Manage Actions access". CI then passed.
- forge-conversations: 174/174 tests pass, re-run by the supervisor; `git ls-files | grep -i worker`
  is empty; `git diff origin/main -- src/ForgeMission.ConversationHost ':!*.md'` is empty.
- Kind, rebuilt with `make 350-conversation-kind-up` from clean `main` of every repo: Host at
  forge-conversations `64d1814`, `mission-worker` at forge-runner `170709d`. Runner log: queue entry
  point enabled, session processor started ~11 s after pod creation.
- One durable turn through Host → `mission-command` → forge-runner → `conversation-progress`: run
  events `userMessage → queued → participantStarted → participantMessage 'hello from forge-runner'
  → completed`; runner log `Processed command … 92+17 tok`. Driven through the Host's HTTP API
  with a fresh Project container (no existing Project touched).
- The published Desktop bundle (`make build-desktop`, zero arguments) booted against Kind and its
  Conversation Runtime health check passed. No UI turn: the Desktop UI is a mock-up (see the
  spoke's default-path row).

**Found in passing:** stopping the Desktop Supervisor with SIGTERM left its native Host and
Application Host children running; recorded in the [backlog](../backlog.md).

## Task 2 — stateless one-shot, durable-only tool loops

**Done 2026-09-29.** Rescoped mid-task after investigation showed client-held continuations are
unsafe (tampering could skip verifiers or alter `kind:exec` inputs) and that the durable path was
already leaking the continuation to clients. Merged by the supervisor after independent verification;
deployed with operator approval by a deployment subagent.

| Repo | PR | Change |
|---|---|---|
| forge-runner | [#7](https://github.com/katasec/forge-runner/pull/7) | `/run` stateless one-shot; `History`/`Tools`/`ToolUse` and the enrichment cache removed; continuation records engine version (build SHA) and package hash; every failed resume ends explicitly. Runner packages 0.2.0 published. |
| forge-infra | [#17](https://github.com/katasec/forge-infra/pull/17) | Kind builds pass `SOURCE_REVISION`. |
| forge-conversations | [#4](https://github.com/katasec/forge-conversations/pull/4) | Continuation removed from every client-facing event/work item; carried on internal progress; Host stores and resumes from its own copy. Contracts + Presentation 0.2.0 published. |
| forge-runner | [#8](https://github.com/katasec/forge-runner/pull/8) | Runner sends the continuation on internal progress only. |
| forge-platform | [#9](https://github.com/katasec/forge-platform/pull/9) | `ExecuteMission` stateless one-shot. |
| forge-desktop | [#5](https://github.com/katasec/forge-desktop/pull/5) | Legacy cloud mission-chat client deleted; sessions default to durable. |
| forge-infra | [#18](https://github.com/katasec/forge-infra/pull/18), [#19](https://github.com/katasec/forge-infra/pull/19) | Enrichment cache removed from Bicep (live DB and Key Vault secret untouched); runner `0.11.6`, ForgeAPI `0.3.3`. |

**Evidence**

- Tests (re-run by the supervisor): forge-runner 48/48; forge-conversations 175/175, including a
  negative test that fails when the continuation leaks into client JSON; forge-desktop
  `ProjectTransportContractTests` 25/25 at the merged commit (full suite 351 passed, 1 skipped, per
  implementer).
- Kind: a plain durable turn and a paused-then-resumed tool turn (client driven through
  `/mission-hands/*`) completed; no continuation in any client-facing JSON.
- Cloud (live): runner revision `ca-forge-runner-dev--0000028` on `forge-runner:0.11.6` (built from
  `8cc43aa`, dll version `0.1.0+8cc43aa…`), ForgeAPI `ca-forge-api-dev--0000008` on `forge-api:0.3.3`,
  both Healthy with 100% traffic (supervisor `az` check). Runner env has no enrichment-cache setting.
- `forge exec websearch "what shipped in the Claude API this week?"` completed `✓ verified` through
  ForgeAPI → runner `/run/stream` (4 steps, 3326+425 tok) and was debited.

**Open from this task:** Rooms one-shot against the new runner not yet exercised (needs the operator
in the browser). Billing logged that `grok-4.5` has no pricing rate and used the fallback. ForgeAPI logs
`Cannot load library libgssapi_krb5.so.2` at startup but serves normally.

## Tasks 3 and 4 — Billing service and metering

**Done 2026-09-29, delivered together.** ForgeAPI debited `authbilling_db` inline on every run, so it
could not drop its database credentials (Task 3) until settlement moved (Task 4); doing them as one
sequence avoided a temporary direct "settle" call that would break the command-bus rule.

| Repo | PR / artifact | Change |
|---|---|---|
| forge-runner | [#9](https://github.com/katasec/forge-runner/pull/9); packages 0.3.0; image `forge-runner:0.12.0` (built from `1930f5d`, pushed from a workstation) | `RunRequest` carries `MemberId`/`RunId`; the runner publishes `{MemberId, RunId, Segment, MissionRef, Usage}` to `private-run-settlement` after every `/run`, `/run/stream`, and `/v1` door run; no `MemberId` → logged and unsettled. |
| forge-platform | [#10](https://github.com/katasec/forge-platform/pull/10) → `forge-api:0.3.4`; [#11](https://github.com/katasec/forge-platform/pull/11) → `forge-api:0.4.0`, `forge-billing:0.1.0`, `Katasec.Forge.Billing.Contracts` 0.1.0 | Billing service: key resolution and balance queries, settlement consumer calling `SettleRunAsync` unchanged (idempotent by `RunId:Segment`). ForgeAPI: auth through Billing, no inline settlement, no billing DB or Npgsql; `/v1` strips client `Authorization`/`X-Forge-*` and sets `X-Forge-Member-Id`; cost/balance removed from `ExecuteMissionResponse`. |
| forge-rooms | [#3](https://github.com/katasec/forge-rooms/pull/3) → `forge-ui:0.7.0` | ForgeUI reads via Billing; no inline settlement; only the two sign-in writes remain (transitional exception). |
| forge-infra | [#20](https://github.com/katasec/forge-infra/pull/20), [#21](https://github.com/katasec/forge-infra/pull/21) | `370-billing-data` (financial namespace, queue, 3 identities, queue/secret-scoped roles), `540-billing`, runner and ForgeAPI on their own identities. |

**Evidence**

- Tests (supervisor re-run): forge-runner 59/59; forge-platform Api 35, Billing.Service 21 (incl.
  redelivery settles once; two segments debit twice), Billing 28; forge-rooms 36 plus a grep showing
  only `PlatformKeyEndpoints.cs:102` and `MemberProvisioningService.cs:35` write billing.
- Deploy (operator-approved; what-if before every apply; cutover order 550-api 0.3.4 → 370 → 540 →
  500-app → 550-api 0.4.0): `370-billing-data` 15 creates, 0 modify, 0 delete; namespace
  `disableLocalAuth=true`, no namespace-scope role assignments.
- Live (supervisor `az` check): runner `0.12.0`, ForgeAPI `0.4.0`, ForgeUI `0.7.0`, Billing `0.1.0`,
  all Healthy at 100% traffic. ForgeAPI env is only `ASPNETCORE_ENVIRONMENT`, `RunnerBaseUrl`,
  `BillingBaseUrl`; its identity has AcrPull only.
- One debit: balance 4,826,807 → 4,822,223 µ$ across one `forge exec websearch` run; Billing logged a
  single `Debited 4584µ$ … WebSearch 1312+101 tok`; queue active 0, dead-letter 0. A bad key returns 401.

- Rooms (operator, browser, 2026-09-28 20:41 UTC): `Hi @grok` answered; runner logged `Ran 'Grok' [trusted] — verified=True steps=3 in 1733+50 tok`; balance 4,822,223 → 4,817,009 µ$ (one debit of 5,214 µ$); settlement queue active 0, dead-letter 0. The Rooms card showed "Not verified" although the runner reported `verified=True` — display mismatch, recorded in the backlog.

**Open from this task:** The
settlement path logs no run id on success, so a debit can't be traced to its run from logs — added to
Task 6's alerting work.

## Tasks 5 to 8 — cloud conversations live

**Done 2026-09-29.** Supervised; each PR merged by the supervisor after re-running its tests; cloud
changes deployed with operator approval through forge-infra `make` targets, what-if before every apply.

| Task | PRs | Outcome |
|---|---|---|
| 5 Host commands over the bus; owner link | [forge-conversations#5](https://github.com/katasec/forge-conversations/pull/5), [forge-runner#10](https://github.com/katasec/forge-runner/pull/10) | 17 commands via `conversation-ingress` with request/reply on session-enabled `conversation-reply`; 8 queries + event stream direct with `X-Forge-Member-Id`; owner link is the storage key itself (tenant = `MemberId`), so a non-owner gets not found; `MemberId` rides `mission-command`, so durable segments settle. Contracts 0.3.0. |
| 7 ForgeAPI messages | [forge-conversations#6](https://github.com/katasec/forge-conversations/pull/6), [#7](https://github.com/katasec/forge-conversations/pull/7), [forge-platform#12](https://github.com/katasec/forge-platform/pull/12) | 25 messages + stream: commands awaited over the bus (30 s, then 503), queries/stream proxied with the member header set by ForgeAPI; credit gate on segment-starting messages; conversation errors use `ConversationApiError` (M4 exception); existing DTOs reused without `Version` (M3 exception). Host HTTP command routes deleted; ingress namespace required. Contracts 0.4.0. |
| 6 Cloud infrastructure | [forge-conversations#8](https://github.com/katasec/forge-conversations/pull/8), [forge-runner#11](https://github.com/katasec/forge-runner/pull/11), [forge-platform#13](https://github.com/katasec/forge-platform/pull/13), [forge-infra#22](https://github.com/katasec/forge-infra/pull/22) | `private-mission-command`/`private-conversation-progress`; new `380-conversation-edge` (local auth off); 525 Host-only (`forge-conversation-host:0.1.0`); runner `0.13.0` (`Runner:Default*`, `minReplicas` 1, durable entry point on managed identity); Billing `0.1.1`; ForgeAPI `0.5.0`; two log alert rules (no action group yet); settlement logs carry `RunId:Segment`. |
| 8 Desktop | [forge-desktop#6](https://github.com/katasec/forge-desktop/pull/6) | Default Conversation Runtime = ForgeAPI; `ConversationHostClient` speaks ForgeAPI messages with `Bearer`; Project runs carry the published version's package; Janus prompt path and Kind tunnel deleted; ForgeAPI auth errors decoded. |

**Evidence**

- Tests (supervisor re-runs): forge-conversations 185 → 190 → 186 → 191 across the PRs; forge-runner
  61/61; forge-platform Api 87, Billing.Service 21, Billing 28; forge-desktop 347 passed, 1 skipped.
- Cloud security checks: edge and financial namespaces `disableLocalAuth=true`; all 14 Service Bus role
  assignments queue-scoped; ForgeAPI holds only Send on ingress and Receive on reply; no edge identity
  on any `private-` queue.
- Task 7 live (ForgeAPI, platform key): `CreateProjectMissionContainer` 201 → `StartProjectMissionRun`
  202 → events `… participantMessage "hello from the cloud" … completed`; runner `Settlement sent for
  bbbf60a5…:04278286…` and Billing `Settled bbbf60a5…:04278286… 436µ$` equal to the balance drop; bad
  key → 401.
- **Task 8 default path** (published bundle from forge-desktop `8e1c15c`, zero arguments, no
  `MissionRuntime*`/`ConversationRuntime*`/`FORGE_*`, after `forge login`, driven through the
  Application Host `/transport/*` because the UI is a mock-up): project/create → draft → promote →
  case → evaluate (Passed) → publish → `project/mission/run` accepted → events `participantMessage
  "hello from the desktop" … completed` → `project/runs` lists the run Completed. No `kubectl` ran.
  Balance 4,816,573 → 4,810,113 µ$ = evaluation `Settled f83513e2…:014508c2… 5519µ$` + Project run
  `Settled ca7101b9…:887e8c99… 941µ$` (supervisor Log Analytics query). Stopping the Desktop left no
  orphaned child processes.

**Found in passing (backlog):** the starter mission's Reviewer ignored the requested reply and
repeated its answer; the Desktop keeps polling after a run completes (~1 platform-key resolve per
second); run history shows `Durable` rather than the mission name; `/transport` rejects string enums;
a local Kind Host joins the cloud Orleans cluster (Kind scaled to 0 during deploy); alert rules have
no recipient; old queues and the Worker identity await retirement.
