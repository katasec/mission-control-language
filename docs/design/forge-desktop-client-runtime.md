# Forge Desktop — Client Runtime / Mission Runtime split

> **Why this doc exists.** [Phase 43.2](../phases/phase-43.2-avalonia-vanilla-shell_completed.md)
> (the Avalonia vanilla shell) proved the tool-execution loop and the agentic streaming UX work —
> Tasks 1–3 were genuinely done and independently verified. What it also proved is that verifying a
> *visual* change in Avalonia needs a paid DevTools tier, per-machine license setup, and hit a
> multi-day environment saga (broken system `PATH`, per-client license/env registration gaps,
> stale-process-needs-restart) before it worked at all — see
> [desktop-interaction-principles.md](desktop-interaction-principles.md) for what that tooling
> looked like and why it was dropped. Meanwhile porting `forge.css`'s design tokens into Avalonia's
> XAML `DynamicResource`/`ThemeDictionaries` is real, recurring translation-tax work every time the
> design changes — and the existing `ForgeUI` Blazor app has been fast and painless to iterate on,
> with zero-setup browser-based visual verification (Chrome DevTools Protocol) already available.
> **Decision (2026-07-27): pivot Forge Desktop's client to a web-rendered UI**, presented either as
> an Electron shell or, for `forge webui`, a plain browser tab. This is not a framework swap alone —
> it changes the shape of the system into two components, described below.

Cross-references: [Phase 43 hub](../phases/phase-43-forge-desktop.md) (the phase this doc backs),
[Phase 43.2 — Electron shell](../phases/phase-43.2-electron-forge-desktop-shell.md) (the active
spoke building against this design), [Phase 42](../phases/phase-42-forge-cloud.md) (the `/v1` wire
protocol this reuses), [42.3](../phases/phase-42.3-tool-capable-enriching-responder.md) (the tool
round-trip mechanism reused verbatim), [42.4](../phases/phase-42.4-container-convergence.md) (the
one-`/v1`-image convergence Docker parity depends on), [Phase 39](../phases/phase-39-metered-runtime-marketplace.md)
(metering — orthogonal to this split, see below).

## The two components

**Client Runtime** — the Electron shell (or, for `forge webui`, just a browser tab pointed at the
same local server). Owns UI rendering **and** local tool execution: it holds `IWorkspace`
([43.7](../phases/phase-43.7-workspace-provider.md)) and `ToolExecutorRegistry`
([43.1](../phases/phase-43.1-tool-execution-engine.md)), with real filesystem/process access on the
user's own machine. This is the "hands."

- Built with **Blazor Server**, not a new JS/TS frontend. Reasons:
  - Shares C# types directly with `ForgeMission.Core` — no serialization boundary. This is the same
    reasoning [Phase 35](../phases/phase-35-forge-ui-blazor.md)'s design doc already used for
    `ForgeUI`.
  - Introduces no new language/toolchain into an otherwise pure .NET repo — this project has an
    explicit "no npm/Node, right-size deps" bias (see
    [Phase 40.1's design-system doc](../phases/phase-40.1-design-system-foundation.md)).
  - The SignalR-per-session overhead that would matter at multi-tenant cloud scale
    ([Phase 39](../phases/phase-39-metered-runtime-marketplace.md)'s domain) is a non-issue for one
    local desktop session — there's exactly one circuit, on the user's own machine.
- UI components for this IDE-shaped surface are **freshly authored** — explicitly **not** reusing
  `ForgeUI`/Rooms' existing Razor components, which are shaped for async multi-user chat (room
  membership, `@`-mentions, trust seals) and don't fit a single-user coding-agent surface.
- **Do** reuse `forge.css`'s design tokens directly — it's already CSS, zero porting cost, unlike
  the Avalonia XAML translation tax that was part of why this pivoted. See
  [ui-design-system.md](ui-design-system.md) for the token catalogue itself (not duplicated here).

**Mission Runtime ("brain")** — a separate, swappable component reached over
[Phase 42](../phases/phase-42-forge-cloud.md)'s existing `/v1` wire protocol (the Anthropic/
OpenAI-compatible chat wire with `tool_use`/`tool_result` round trips already built in
[42.3](../phases/phase-42.3-tool-capable-enriching-responder.md)'s "tool-capable enriching
responder" — `AnthropicServer` accepting tools, emitting `tool_use`, resuming on `tool_result`, the
enrich-once/re-entrancy gate). It is **either**:

- the hosted Forge Cloud endpoint (`forge.katasec.com`, per the existing `forge claude` model,
  [Phase 42.6](../phases/phase-42.6-hosted-endpoint-ttfa.md)), **or**
- a **local Docker container running the exact same `/v1` image**
  [Phase 42.4](../phases/phase-42.4-container-convergence.md) already built (the "one `/v1` image:
  Docker ≡ ACA" convergence).

The Client Runtime's code path is identical regardless of which brain backs it — it plays exactly
the role Claude Code CLI already plays today against `forge claude`: execute what the server asks
locally, POST results back. **This reuses 42.3's already-shipped mechanism; it is not a new
invention.**

## Why Docker is retained (parity, not legacy)

Docker's presence in this design is **not** a holdover from `forge webui`'s old Open WebUI
dependency (see redefinition below) — it's retained because it's the mechanism that gets:

1. **Local dev/test parity with the cloud contract** — the same `/v1` image
   [42.4](../phases/phase-42.4-container-convergence.md) built for Azure Container Apps runs
   unmodified on a developer's own machine. One image, two schedulers, one contract to maintain.
2. **A genuine fully-local/private/no-account deployment mode** — a user who never wants to talk to
   `forge.katasec.com` or send code to a hosted provider gets a real, complete offline path: local
   Docker Mission Runtime + local Client Runtime, no network egress required beyond whatever
   provider key the local container is configured with.

## Metering (orthogonal to this split)

[Phase 39](../phases/phase-39-metered-runtime-marketplace.md)'s metered ledger wraps the **hosted**
Mission Runtime only — the same "metering wrapped in cloud only" invariant
[42.4](../phases/phase-42.4-container-convergence.md) already locked. A local Docker Mission Runtime
runs unmetered against the developer's own provider keys, exactly like `forge serve` does today.
This split doesn't change Phase 39's design; it just gives the Mission Runtime two hosting
targets, only one of which is billed.

## `forge webui` redefinition

Today, `forge webui` launches [Open WebUI](https://github.com/open-webui/open-webui) in a Docker
container — a generic third-party chat UI ([Phase 23](../phases/phase-23-container-commands.md)).
That dependency is dropped. **Going forward**, `forge webui` starts the local Docker Mission
Runtime (if not pointed at a hosted/cloud target) and opens the Client Runtime UI in a browser tab
— same backend as the Electron app, no Electron wrapper, browser instead of a native shell.

Electron and `forge webui` become **two presentation shells over the identical local Client
Runtime** — same Blazor Server host, same `IWorkspace`/`ToolExecutorRegistry`, same wire protocol
to whichever Mission Runtime is configured. The only difference is the chrome around it (a native
window vs. a browser tab).

## Open architecture question — NOT resolved here

**Where does the tool-call orchestration loop (deciding what to call next) live?**

The original Avalonia build had the Client hold `AgenticSession` in-process — no `forge serve` hop
was a locked [43.2](../phases/phase-43.2-avalonia-vanilla-shell_completed.md) decision at the time,
made when Client and Mission were one undifferentiated process.

Under the Client Runtime / Mission Runtime split above, that assumption no longer obviously holds:
the orchestration loop *could* stay client-side (the Client Runtime keeps `AgenticSession`, and the
Mission Runtime is just an `IChatClient` swapped in over the wire), or it could move server-side
(the Mission Runtime owns `AgenticSession`, and the Client Runtime becomes a thinner tool-executing
peer that only responds to `tool_use` requests over the wire — much closer to how Claude Code CLI
itself relates to `forge claude` today).

**This is stated as an open design question, not a decided fact.** It is
[43.2](../phases/phase-43.2-electron-forge-desktop-shell.md) Task 2's job to resolve it as part of
wiring the Mission Runtime connection — whoever picks up that task must decide and record the
answer there, not silently default to one shape or the other.
