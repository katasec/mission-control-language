# Phase 49.4c — Runner and Orchestration CLI dependency cut: evidence

> **Status:** Final review evidence, 2026-09-22. Raw AOT/MAUI output remains outside Git at
> `/private/tmp/phase49-4c-20260922`.

## Outcome

`ForgeMission.MissionRegistry` is an AOT-safe, explicitly non-packable internal owner for the
existing OCI cache/pull and pinned built-in catalogue behavior. Runner now references Registry and
Scout, not CLI. Orchestration owns a source-local copy of its Docker default resource, avoiding an
OCI transport dependency in Desktop; a test proves it matches Registry's canonical pinned value.

## Acceptance observations

| Check | Observation |
|---|---|
| Scope/graph | Runner has no CLI project/source edge; Orchestration has no linked CLI resource; Desktop has no Registry edge. Registry is `IsPackable=false`. No Docker, workflow, IaC, deploy, package, identity, or store change. |
| Behavior | Tests prove XAI key wins GROK, absent key returns null, canonical resource is digest-pinned, and unpinned references reject. Existing OCI fallback and MissionRef/MissionFile behavior remains covered. |
| Focused tests | Runner tests passed 9/9. Registry/Orchestration tests passed 5/5 with one intentional Docker integration skip. |
| Full build/test | `dotnet build src/ForgeMission.slnx --no-restore` passed 0 warnings/errors; full suite passed 735, skipped 4, failed 0. |
| AOT/MAUI | CLI, Application Host, Desktop Supervisor, and MAUI publishes passed. Only known macOS linker compatibility warnings appeared; no new warning class. |
| Review | Independent review PASS after structural packaging, documentation, and deterministic failure/default corrections. |

## Rollback

Revert the accepted merge commit to restore the Runner → CLI edge, CLI-local registry files, and
the linked Orchestration resource. No package, image, deployment, datastore, identity, or cache
mutation needs rollback. The bounded duplicate resource must re-home through a versioned contract
before Desktop/MCL extraction.
