# Phase 43 — Forge Desktop

> **Status: Project Workbench MVP in progress (2026-08-30).** The shared Rooms/Desktop activity
> surface and durable Conversation Runtime supervision are verified. The selected follow-up replaces
> the proof-era anonymous workspace with a project-first workbench.

## Purpose

Forge Desktop is the coding-agent client where users attach a **mission**, rather than a model. It
is a replaceable presentation client over the Mission Runtime (reasoning) and Client Runtime
(local capability execution and authorization). The canonical system architecture is
[Forge Architecture](../design/forge-architecture.md); this hub does not restate it.

## Read only what the task needs

| If working on… | Read… |
|---|---|
| Current Project/Conversation/Run workbench MVP | [43.20 — Project Workbench MVP](phase-43.20-project-workbench-mvp.md). |
| Shared in-chat activity visual (complete; read only if changing it) | [43.18 — Shared conversation activity surface](phase-43.18-shared-conversation-activity.md), then only its named design sections and source files. |
| Deferred rich trace/workbench UX | [43.4 — IDE trace surface](phase-43.4-ide-trace-surface.md). |
| Deferred responsiveness/lifecycle work | [43.17 — Responsive Desktop lifecycle and UI](phase-43.17-responsive-desktop.md). |
| Human approval/suspend/resume | [43.5 — Human-in-the-loop](phase-43.5-human-in-the-loop.md). |
| Mission picker/catalog follow-up | [43.3 — Mission-as-attach-point](phase-43.3-mission-attach-point.md). |
| AOT follow-up | [43.12 — AOT hygiene backlog](phase-43.12-aot-hygiene-backlog.md). |

Do not read the prior Desktop framework pivots or completed task summaries to work on one of these
items. Their history is either in the individual spoke or the
[Phase 43 completed record](phase-43-forge-desktop_completed.md).

## Current routing

| Work | Status |
|---|---|
| [43.20 — Project Workbench MVP](phase-43.20-project-workbench-mvp.md) | In progress. The local Project record replaced the anonymous workspace 2026-08-30; Mission Control, named runs, exact Trace, Stop, guidance, and the outcome loop remain. |
| [43.19 — Durable conversation runtime supervision](phase-43.19-conversation-runtime-supervision.md) | Verified complete 2026-08-20. Supervisor resolves, health-checks, and injects the current local Conversation Runtime before Client Runtime starts. |
| [43.18 — Shared conversation activity surface](phase-43.18-shared-conversation-activity.md) | Verified complete 2026-08-17. One renderer live in Rooms and the packaged Desktop; no new event or trace transport. |
| [43.17 — Responsive Desktop lifecycle and UI](phase-43.17-responsive-desktop.md) | Lifecycle and session ownership are done. Bounded delivery and progressive rendering remain deferred. |
| [43.16 — Durable Janus conversation proof](phase-43.16-janus-desktop-local-poc.md) | Core product proof done and verified 2026-08-16. |
| 43.3 mission catalog/OCI follow-up; 43.4 rich workbench; 43.5 human gates | Deferred follow-up work; each owns its own design and readiness. |

## Durable decisions

- **Missions are the attach point, not models.**
- **Forge owns local tool execution.** The Client Runtime, never the Presentation layer, owns
  capability authorization and filesystem/terminal work.
- **Desktop is supervised separately from its native host.** `ForgeMission.Desktop` owns runtime
  lifetime; the Host is a disposable child process (Photino today), not the UI framework or a place
  for business logic, credentials, or cleanup.
- **The Mission Runtime is external to the Client Runtime.** The shared orchestration layer resolves
  and supervises it before injecting its URL into Client Runtime.

The exact contracts and rationale live in [Forge Architecture](../design/forge-architecture.md).

## Completion record

The prior Desktop proof, framework pivots, completed prerequisite chain, and historical routing
table have moved to [Phase 43 completed record](phase-43-forge-desktop_completed.md). This hub
only routes active work.
