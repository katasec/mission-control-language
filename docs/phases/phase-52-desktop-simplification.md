# Phase 52 — Desktop simplification

**Goal:** make the Desktop's default path cloud-backed, then collapse its launch machinery into
one MAUI process that performs the same dependency checks. The target is fewer projects, fewer
processes, and no local Kind dependency on the default path.

> **Status: design (2026-09-28).** No spoke is build-ready yet. Each spoke lists its open
> questions; close them before handing off implementation.

## Why

The Desktop launches through a Supervisor, a pipe protocol, a native Host, an Orchestration
library, and an Application Host. On the default path that machinery performs one dependency
check (Conversation Runtime `/health`) plus an Application Host readiness wait. Local Kind startup
adds recurring friction and long agent debugging loops. Local and cloud share one Conversation
contract by design, so an always-up cloud default removes Kind from the normal path and proves
that contract.

## Spokes

Numbers are the execution order. Each spoke starts only after the previous one is complete,
except the 52.4 spike, which runs alongside 52.1.

| # | Spoke | Outcome | State |
|---|---|---|---|
| 1 | [Cloud conversations](phase-52.1-cloud-conversations.md) | Desktop's default Conversation Runtime is the hosted service through ForgeAPI. | Design |
| 2 | [Trim Orchestration](phase-52.2-trim-orchestration.md) | Kind tunnel, Docker launcher, and unused paths deleted. | Design |
| 3 | [Pipeless Boot](phase-52.3-pipeless-boot.md) | One MAUI process checks both runtimes and navigates; Supervisor, Contracts, and Orchestration deleted. | Design |
| 4 | [BlazorWebView](phase-52.4-blazor-webview.md) | UI runs in-process; Application Host and Transport deleted. Gated by an AOT spike. | Design |

## Project count

| After | `forge-desktop/src` projects |
|---|---|
| Today | 10: Application, Application.Host, Application.Transport, ClientRuntime, Desktop, Desktop.Contracts, Desktop.Host, Orchestration, Presentation, Tests |
| 52.3 | 7: Desktop.Host, Application.Host, Application, Application.Transport, ClientRuntime, Presentation, Tests |
| 52.4 | 5: Desktop.Host, Application, ClientRuntime, Presentation, Tests |

## Deferred (backlog)

- `forge dev start` / `stop` — Docker-based local dependencies replacing Kind.
- Split Bob (ClientRuntime) into its own repo/package.

See [backlog.md](../backlog.md).
