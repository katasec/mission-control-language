# Phase 46.2 Task A — Core generic root-scoped continuation

> **Status:** implementation accepted 2026-09-07. The later integrated normal-Desktop
> acceptance remains owned by the Phase 45/46 generic hands path.
> Finding: F46.1-01. Parent: [Phase 46.2](phase-46.2-codex-supervised-remediation.md).

Core now provides the generic, root-scoped nested-tool pause/resume seam. It is declaration-based,
serializable, restart-safe, and has no capability authority. Janus/Naive Worker specialization is
unchanged here and remains the dependent F46.1-02/03 migration.

| Evidence | Observation |
|---|---|
| Independent review | Accepted after two adversarial passes; no Worker, Host, Bob, profile, UI, or legacy-route scope drift. |
| Focused tests | `AgentToolPipelineTests`: 18 passed. |
| Build | `dotnet build src/ForgeMission.slnx`: 0 warnings, 0 errors. |
| Full tests | Exit 0: ForgeMission.Tests 617 passed / 7 skipped; Conversation Host 153; Worker 60; Runner 5; Rooms 97. |
| Native AOT | `make install` exit 0; published `forge` to `~/.local/bin`. macOS linker emitted existing platform-library compatibility warnings; no managed build warnings. |
| Default path | Not independently closable at this Core-only boundary: no published Desktop journey yet invokes the generic durable hands seam. The required zero-argument Desktop acceptance stays explicit in the dependent Phase 45/46 integration work. |

Full approved scope, failure contract, and implementation evidence: [completed record](phase-46.2-task-a-core-continuation_completed.md).
