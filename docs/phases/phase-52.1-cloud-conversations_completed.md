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
