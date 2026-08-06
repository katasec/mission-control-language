# Brainstorm: Generative UI (CopilotKit / AG-UI) for Forge Desktop

**Status: conceptual only — not decided, not scoped as a phase.** Captured from a design
conversation on 2026-08-06. If this gets picked up, split it into a proper phase hub/spoke per
`AGENTS.md`; don't build directly from this doc.

## The idea

CopilotKit popularized "generative UI" for AI chat surfaces: instead of an agent replying with
text only, a tool call can render a real, interactive UI component inline — a rich card, a diff
viewer, an approve/deny widget, a form — chosen dynamically by the agent rather than hardcoded
per chat bubble. It also ships shared state (bidirectional sync between agent and UI) and
human-in-the-loop approval components.

This maps unusually well onto ground Forge Desktop is already covering:

| CopilotKit concept | Forge Desktop equivalent |
|---|---|
| Tool call → generative UI render | `AgenticSession`/`FunctionCallContent` ([43.1](../phases/phase-43.1-tool-execution-engine.md), done) |
| Frontend actions / shared state | Capability Provider pattern ([43.8](../phases/phase-43.8-capability-provider-pattern.md)) |
| Human-in-the-loop approve/deny UI | [43.5 — Human-in-the-loop](../phases/phase-43.5-human-in-the-loop.md) (suspend/resume, not yet built) |
| Rich inline widgets beyond chat bubbles | [43.4 — IDE trace surface](../phases/phase-43.4-ide-trace-surface.md) (outline/thread/gate/code-pane) |

## Note for future agents — two false blockers raised in this conversation, both busted

An earlier pass through this brainstorm (same session, 2026-08-06) raised two "this doesn't work"
claims. Both were wrong. Don't re-raise them without reading the evidence below first.

### False blocker 1 — "CopilotKit has no .NET integration"

**Wrong.** There is official, first-party support via the **AG-UI protocol**:

- `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` is a real NuGet package. Confirmed directly from
  Microsoft's own .NET 10 announcement (devblogs.microsoft.com/dotnet/announcing-dotnet-10):
  *"Use the new `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` package to easily map AG-UI
  endpoints for your agents."*
- Microsoft explicitly names **CopilotKit** in that same announcement as a recommended
  AG-UI-compatible frontend client.
- AG-UI itself is a real, open, event-based protocol (streaming, tool calls, shared state, UI
  sync) — not a proprietary CopilotKit-only thing.

Nuance that's still true and matters: the .NET package is a **backend hosting layer**, aimed at
agents built with **Microsoft Agent Framework** (a different orchestration framework from
`ForgeMission.Core`). AG-UI is just an event schema though, so Forge could in principle expose its
own AG-UI endpoint over the Mission Runtime without adopting Microsoft Agent Framework — untested,
but not blocked by anything found so far.

Sources: [CopilotKit MS Agent Framework showcase](https://showcase.copilotkit.ai/integrations/ms-agent-dotnet),
[quickstart](https://docs.copilotkit.ai/ms-agent-dotnet/quickstart),
[generative UI docs](https://docs.copilotkit.ai/microsoft-agent-framework/generative-ui),
[AG-UI compatibility announcement](https://webflow.copilotkit.ai/blog/microsoft-agent-framework-is-now-ag-ui-compatible),
[.NET 10 announcement](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/) (all fetched
and confirmed directly, not taken on faith).

### False blocker 2 — "There's no practical way to embed React inside Blazor WebAssembly (or vice versa)"

**Wrong.** Confirmed directly (fetched, not assumed):

- **JS interop (`Microsoft.JSInterop`, `[JSImport]`/`[JSExport]`) is official, first-party, and
  mature** — bidirectional .NET ↔ JS calls have existed since early Blazor and are still core to
  the .NET 10 docs. This is the real, production-grade primitive underneath every option below.
- **`ReactBlazorAdapter`** (React components rendered inside Blazor WASM) — real and functional.
  Checked the repo directly: 10 stars, solo-maintainer, .NET 8 + React 18 only, WASM/MAUI
  supported (Blazor Server explicitly not). Working, but a small community project — treat as a
  spike candidate, not established infrastructure.
- **`maraf/blazor-wasm-react`** (Blazor WASM embedded inside a React app, the inverse direction) —
  also real. Checked directly: 2 stars, 2 commits, requires .NET 10 preview. Genuine
  proof-of-concept, not battle-tested.
- Microsoft also ships an official **JS Component Generation sample**
  (`aka.ms/blazor-js-components`, `aspnet/samples`) that auto-generates JS/React wrapper code for
  Blazor components — an officially-sanctioned pattern for this exact interop direction.

Net: the underlying mechanism is solid and official; the specific "mount CopilotKit inside our
Blazor shell" adapter libraries that exist today are real but small and unproven at scale. That's
a maturity/risk judgment to make deliberately, not a hard technical wall.

## What this actually changes for Forge Desktop

Before this evidence, the architecture looked like a hard fork: either rewrite the Forge Desktop
frontend in React/Next.js to get CopilotKit's generative-UI components, or hand-build an
AG-UI-speaking client natively in Blazor from scratch. There is a genuine third option:

**Embed CopilotKit's React generative-UI components inside the existing Blazor WASM shell via JS
interop**, with the .NET backend exposing AG-UI-shaped endpoints (either by adopting
`Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` directly, or hand-rolling an AG-UI endpoint over
`ForgeMission.Core` if pulling in Microsoft Agent Framework isn't wanted).

Real cost of that option: two rendering runtimes shipping together (.NET WASM + a React runtime),
DOM-ownership boundaries to manage carefully (Blazor's JS interop docs are explicit that Blazor
must not have its DOM mutated externally where it owns rendering), and reliance on small/solo-
maintained adapter libraries rather than turnkey Microsoft tooling.

## Where this could land, if pursued

- **[43.5 — Human-in-the-loop](../phases/phase-43.5-human-in-the-loop.md)** is the first concrete
  use case — approve/deny UI is exactly what CopilotKit's HITL components already do.
  `kind: human` + `Suspended` `StepEnvelope` gives the backend event to hang a rendered component
  off of.
- **[43.4 — IDE trace surface](../phases/phase-43.4-ide-trace-surface.md)** is the natural home for
  a general tool-call → component registry, since it's already iterating toward a richer-than-chat
  surface (outline/thread/gate/code-pane).

## Open questions — not resolved, don't build from this doc

- Does Forge want an AG-UI endpoint on `ForgeMission.Core` directly, or is adopting Microsoft
  Agent Framework as (part of) the Mission Runtime itself now back on the table? Different
  question from generative UI, but this evidence reopens it.
- Is a React-runtime-inside-WASM-shell payload acceptable for a Photino-packaged desktop app
  (less webpage-weight-sensitive than a public site, but not free)?
- Has anyone actually spiked `ReactBlazorAdapter` (or the inverse) against the current Forge
  Desktop shell before this gets designed into a phase? Recommended next step, not yet done.
