# Phase 62 — completed record

Back to the [spoke](phase-62-single-interpreter-replay.md).

## Release

| Repo | Change | Evidence |
|---|---|---|
| forge-mcl | [katasec/forge-mcl#51](https://github.com/katasec/forge-mcl/pull/51), merge `2cd7eb9`, tag `core-v0.1.7` | Core 0.1.7 listed on the katasec feed, private, linked to `katasec/forge-mcl` (the workflow's final check failed as it always does, see [backlog](../backlog.md)) |
| forge-runner | [katasec/forge-runner#24](https://github.com/katasec/forge-runner/pull/24), merge `84215d0`, tag `forge-runner-v0.20.4` | image workflow run 37138802087 `success` |
| forge-infra | [katasec/forge-infra#43](https://github.com/katasec/forge-infra/pull/43), merge `a9a8371` | `make 500-app-what-if`: runner image is the only real change; `make 500-app` Succeeded; `ca-forge-runner-dev--0000041` Running `forge-runner:0.20.4` |

## Tasks

| # | Result |
|---|---|
| 1–5 | `RootScopedExecution`, frame records, the dispatch and the duplicate fingerprint are deleted (grep of `src`: none left). One invoke point, `InvokeStepAsync`; private `RunState` (log, paused key, replay flag) and `PauseScope`. `PipelineRunner.cs` 1137 → 1078 lines. |
| 6 | 7 new tests in `AgentToolPipelineTests.cs` pass; the 16 root-scoped tests pass with one assertion changed (provider error now throws, R8). |
| 7 | See Release. |

Checks: `dotnet build ForgeMission.slnx --no-incremental` 0 warnings; `dotnet test` 739 passed, 0 failed,
10 skipped (supervisor rerun); AOT publish of the CLI 0 IL/AOT warnings; `eng/verify-core-package.sh`
PASS; forge-runner build 0 warnings, 115/115 tests.

## Build decisions

| Question | Decision |
|---|---|
| Version number | Outer `PipelineContinuation.FormatVersion` stays 1 (the envelope the runner knows; it hardcodes 1). The inner checkpoint is format 2; format 1 is rejected as `InvalidContinuation`. No runner code change. |
| `PipelineFailure.ProviderFailed` | Deleted (no references in any repo). Provider errors throw (R8). |
| Root context with root tools | A fresh run seeds only `RootInputs`, as B did, so a fresh run and a resume see the same root context. |
| When the log is kept | Every run (one path). Sequential `parallel` and the pause apply only with root tools. |
| Direct agent inside `parallel` | Now gets root tools and can pause (before, neither interpreter attached them there). |
| Unmatched `when` with no `else` | A's "No when() guard matched" throw (B skipped silently). |
| Trace mission name | A labels a step with the mission running it, so hosted progress for a child step reads `Child:Expert`, not `Root:Expert`. |

## Default-path check (2026-10-03)

Installed `forge` from forge-mcl `main` `2cd7eb9` (`make install`), deployed runner 0.20.4.

- `forge chat --hands`: asked to read `repo-a/README.md` and `repo-b/README.md`. It ran two
  reads (`Read repo-a/README.md → succeeded`, `Read repo-b/README.md → succeeded`), so two pauses and
  two resumes, and quoted both first lines correctly.
- `forge chat`: "reply with exactly the word PONG" → `PONG.`

Gap: the check ran in the project's existing conversation, not a fresh one. `forge chat` always
reopens the latest conversation for the mode and has no option to start a new one.

Not from this phase: line mode prints each reply twice. History shows this from 1:43 PM on the
old runner, before this deploy, for both plain and hands chat.
