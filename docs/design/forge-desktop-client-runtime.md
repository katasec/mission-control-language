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

## Capability boundary — the Client Runtime is the hands (locked 2026-07-30)

The opened folder, local filesystem and local process execution belong **only** to the Client
Runtime. The Mission Runtime is the brain: it owns mission interpretation, experts, provider/model
calls, and deciding whether to return final text or request a declared tool. It never receives a
general mount of the user's opened workspace, regardless of whether it is hosted or a local Docker
container.

The only information crossing from hands to brain is deliberate protocol input:

- conversation messages and `tool_result` values over `/v1/messages`;
- an explicit mission package/source selected for the brain to execute; and
- future explicit artifacts the Client Runtime chooses to upload or relay (for example, an OCR
  file), never an implicit filesystem path with ambient access.

The only instruction crossing from brain to hands is a `/v1` response: final text or a declared
tool request. The Client Runtime executes that request through its existing `IWorkspace` and
`ToolExecutorRegistry`, then sends the result back. Thus Docker and cloud remain target-invariant
for the loop: the URL changes, not the loop component or local-tool authority.

The Task 2b Docker proof intentionally established the wire and lifecycle first. Its repository
bind mount was replaced in [43.2a — Client Runtime capability
boundary](../phases/phase-43.2a-client-runtime-capability-boundary.md): the Client Runtime now
creates the runner stopped, packages the selected mission in memory, uploads it through Docker
Engine into container-owned storage, then starts it with `MissionFile` pointing at that copy. The
runner has no host bind, including for the mission source, and its dynamic `/v1` port publishes only
to `127.0.0.1`. The mission source may be within the opened workspace because only the explicit
mission archive crosses the boundary. OCI mission artifacts and explicit artifact relay remain
future work; see the spoke for verification evidence and review status.

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

## Local Docker provider keys

The Client Runtime, not Electron's parent process, owns provider-key loading for the local Docker
Mission Runtime. It reads a local dotenv file at
`Environment.SpecialFolder.ApplicationData/Forge/provider.env` (on macOS,
`~/Library/Application Support/Forge/provider.env`) and forwards only the established provider and
model allow-list to the runner container. For the Task 2b `missions/vanilla` proof, the minimal
file is:

```dotenv
MCL_API_KEY=<your OpenAI key>
```

The same file may contain `MCL_MODEL`, `MCL_PROVIDER`, `MCL_ENDPOINT`, `OPENAI_API_KEY`,
`CLAUDE_API_KEY`, `XAI_API_KEY`, `GROK_API_KEY`, and `GOOGLE_SEARCH_API_KEY` for other local
missions. The file is user-local, never committed, and must be created with user-only permissions.
An absent file or absent required key is a startup error before Docker is started; the Client Runtime
does not fall back to inherited provider-key environment variables. This is intentional: macOS apps
started by Finder or the Dock do not inherit a terminal's `pwsh` key exports, while the user-local
file is available to either launch path.

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

## Architecture decision — orchestration loop lives in the Client Runtime (2026-07-27)

**Resolved.** The Client Runtime owns the tool-call orchestration loop. It plays exactly the role
the real Claude Code CLI already plays against `forge claude` today: send the accumulated message
history to the Mission Runtime's `/v1/messages`, get back either a final answer or one `tool_use`,
execute it via `IWorkspace`/`ToolExecutorRegistry`, append the result, send again. The Mission
Runtime's `/v1` endpoint never drives more than one turn per HTTP call.

**Correction (same day, before any implementation depended on the imprecise version below): this
is not `AgenticSession` pointed at a network call.**
[43.1](../phases/phase-43.1-tool-execution-engine.md)'s `AgenticSession` calls
`PipelineRunner.RunAsync` directly, in-process, against a locally-held `ast`/`experts`/`IChatClient`
— that shape assumes Client and Mission are the same undifferentiated process, exactly the old
Avalonia assumption this doc's intro already retired. Under the Client Runtime / Mission Runtime
split, the mission's `ast`/`experts`/provider key belong to the Mission Runtime, wherever it runs —
the Client Runtime no longer holds any of them locally. So the Client Runtime needs a **new, small
loop-driving component** (naming left to
[43.2](../phases/phase-43.2-electron-forge-desktop-shell.md) Task 2) that owns the growing
conversation history and talks to `/v1/messages` over HTTP — not a revised `AgenticSession`. What
**is** reused verbatim from 43.1 is its tool-execution machinery
(`ToolExecutorRegistry`/`AgentToolDeclarations`) and [43.7](../phases/phase-43.7-workspace-provider.md)'s
`IWorkspace` — neither cares whether the model turn that produced a `tool_use` came from a local
`PipelineRunner` call or a remote `/v1` response, so both plug into the new component unchanged.
`AgenticSession` itself is untouched by this decision — it remains exactly what 43.1 built, the
in-process loop a **Mission Runtime host** (`forge serve`, the Runner, or a future fully-local
single-process mode) can use; the Client Runtime never calls it.

Reasoning:
- **Reachability.** The Client Runtime always initiates the HTTP call. If the Mission Runtime drove
  the loop, it would need to call back into the Client mid-response to run a tool — infeasible for
  the hosted target (`forge.katasec.com` can't reach into a user's home network) without a new
  persistent push channel.
- **No new invention.** [42.3](../phases/phase-42.3-tool-capable-enriching-responder.md)'s
  mechanism is a synchronous request/response wire contract, reused verbatim by this decision. A
  server-driven loop would require re-architecting it.
- **Identical code path regardless of brain.** A stateless-per-call HTTP contract behaves the same
  whether the Mission Runtime is hosted cloud or local Docker — no reachability asymmetry between
  the two targets.
- **Matches this doc's own framing above** — "it plays exactly the role Claude Code CLI already
  plays today against `forge claude`."

**Mechanical detail — session store vs. conversation history.** The Mission Runtime's session
store ([42.3](../phases/phase-42.3-tool-capable-enriching-responder.md)'s enrich-once gate) stays
what it already is: an internal optimization so the Mission Runtime doesn't re-run full mission
enrichment on every tool-result follow-up call. It is **not** a replacement for the Client Runtime
resending conversation history. The Client Runtime's `AgenticSession` still owns and resends the
growing message list on every call, exactly like the Anthropic wire always has.

## Future consideration — server-owned orchestration loop (deferred, not scheduled)

Flagged during the 2026-07-27 design discussion above, deliberately not pursued now: too much open
scope relative to any concrete near-term value. Captured here so it isn't silently lost, and so it
isn't re-litigated from scratch if it resurfaces.

A server-owned loop would replace `/v1`'s request/response contract with a persistent channel
(client-initiated WebSocket/SignalR — the same primitive
[Rooms](../phases/phase-38.1-room-foundation.md) already uses, so this is not fundamentally blocked
by reachability the way a synchronous mid-response callback would be). The Mission Runtime would
drive the full multi-tool-call arc itself, pushing "execute this" events down the channel and
awaiting results, and the Client Runtime would become a pure reactor with no loop logic of its own.

Who'd actually want it, if ever built:
- **Thin future clients** (a VS Code extension, mobile companion, CI sandbox) that want to execute
  tools without reimplementing `AgenticSession`'s conversation bookkeeping and continuation logic.
- **Centralized limits/policy** — max-tool-calls-per-turn, timeouts, abuse prevention enforced once
  in the Mission Runtime instead of trusted to every client, more relevant once
  [Phase 39](../phases/phase-39-metered-runtime-marketplace.md)'s hosted multi-tenant surface has
  clients Forge doesn't control.
- **Unified trace/metering granularity** — one server-side view of a whole multi-tool-call task
  instead of N stitched-together `/v1` calls, which
  [43.4](../phases/phase-43.4-ide-trace-surface.md)'s planned debugger surface would want eventually.
- **Mission-language reach** — if a mission ever wants to itself bound or react to the tool-call
  loop (e.g. hand off to a different expert after N rounds), that requires the loop to be visible to
  the mission runtime, not opaque client code the mission never sees.

Not mutually exclusive with today's decision — if built, it would most naturally show up as a
*second* Mission Runtime entry point (the WebSocket channel) serving thin clients, coexisting with
today's `/v1` HTTP endpoint, which keeps serving fat clients (real `claude` CLI, this project's own
Client Runtime) exactly as it does today. No trigger condition is set; revisit only if a concrete
thin-client or centralized-policy need actually shows up.
