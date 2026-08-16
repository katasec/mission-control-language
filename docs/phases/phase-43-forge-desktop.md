# Phase 43 — Forge Desktop

> **Status: UI responsiveness design active (2026-08-16).** The Janus Desktop product proof is
> complete; the immediate work is making the shipped Photino + Blazor WASM desktop feel responsive
> through startup, shutdown, streaming, and session changes.

## Purpose

Forge Desktop is the coding-agent client where users attach a **mission**, rather than a model. It
is a replaceable presentation client over the Mission Runtime (reasoning) and Client Runtime
(local capability execution and authorization). The canonical system architecture is
[Forge Architecture](../design/forge-architecture.md); this hub does not restate it.

## Read only what the task needs

| If working on… | Read… |
|---|---|
| Current Desktop responsiveness work | [43.17 — Responsive Desktop lifecycle and UI](phase-43.17-responsive-desktop.md), then only its named design sections and source files. |
| Trace/workbench UX | [43.4 — IDE trace surface](phase-43.4-ide-trace-surface.md). |
| Human approval/suspend/resume | [43.5 — Human-in-the-loop](phase-43.5-human-in-the-loop.md). |
| Mission picker/catalog follow-up | [43.3 — Mission-as-attach-point](phase-43.3-mission-attach-point.md). |
| AOT follow-up | [43.12 — AOT hygiene backlog](phase-43.12-aot-hygiene-backlog.md). |

Do not read the prior Desktop framework pivots or completed task summaries to work on one of these
items. Their history is either in the individual spoke or the
[Phase 43 completed record](phase-43-forge-desktop_completed.md).

## Current routing

| Work | Status |
|---|---|
| [43.17 — Responsive Desktop lifecycle and UI](phase-43.17-responsive-desktop.md) | **Current design spike.** Establish a non-blocking native lifecycle and bounded reactive UI updates before further UI iteration. |
| [43.16 — Durable Janus conversation proof](phase-43.16-janus-desktop-local-poc.md) | Core product proof done and verified 2026-08-16. |
| 43.3 mission catalog/OCI follow-up; 43.4 workbench; 43.5 human controls | Deferred follow-up work; each owns its own design and readiness. |

## Durable decisions

- **Missions are the attach point, not models.**
- **Forge owns local tool execution.** The Client Runtime, never the Presentation layer, owns
  capability authorization and filesystem/terminal work.
- **Desktop is Blazor WebAssembly packaged by Photino.** Photino is a disposable native host, not
  the UI framework or a place for business logic.
- **The Mission Runtime is external to the Client Runtime.** The shared orchestration layer resolves
  and supervises it before injecting its URL into Client Runtime.

The exact contracts and rationale live in [Forge Architecture](../design/forge-architecture.md).

## Completion record

The prior Desktop proof, framework pivots, completed prerequisite chain, and historical routing
table have moved to [Phase 43 completed record](phase-43-forge-desktop_completed.md). This hub
only routes active work.
