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
