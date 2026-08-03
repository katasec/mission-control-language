# Forge Architecture — Mission Runtime, Client Runtime, Presentation

**Status: Locked 2026-08-01.** This is the canonical, durable architecture doc for Forge as a
whole, not just Forge Desktop. It supersedes the general-architecture parts of
[forge-desktop-client-runtime.md](forge-desktop-client-runtime.md), which now covers only what's
genuinely desktop-specific (see that doc's updated status line). If this doc and any other doc
disagree on the Mission Runtime / Client Runtime / Presentation split, this doc wins — point
other docs here rather than restating the architecture.

Emerged from the Phase 43 desktop-technology discussion (Tauri vs. Avalonia vs. Electron vs.
native .NET), but the discussion repeatedly exposed questions bigger than "which desktop
framework" — each time, the abstraction moved to the correct layer instead of being bolted onto
whichever framework was under discussion. The result: the desktop is now one of the least
architecturally interesting parts of the system. It's a client, replaceable like any other.

---

## Core philosophy

**Forge is not a desktop application. Forge is a Mission Runtime.** The desktop is one possible
client among several.

The Mission Runtime may execute:

- Forge Cloud (hosted)
- localhost (`forge serve`)
- Docker (local or hosted container)
- Kubernetes
- an enterprise deployment

A client — desktop, CLI, VS Code, Forge Rooms, a mobile app — should not care where the Mission
Runtime lives. The protocol between a client and the Mission Runtime is invariant regardless of
target. This isn't new: [Phase 42](../phases/phase-42-forge-cloud.md) already runs multiple
wire-protocol clients (`forge claude`, `forge codex`, hosted `forge.katasec.com`, the MCP door)
against one stable brain. This doc generalizes that same principle into the standing architecture,
rather than treating it as an implementation detail of the cloud phase.

**Forge stabilizes contracts, not technologies.** Capability contracts, transport contracts, and
provider abstractions are the durable layer. Everything behind them — a WebView vendor, an HTTP
library, a packaging tool — is replaceable infrastructure. When a technology choice changes (as it
already has twice for the desktop shell — Avalonia, then Electron, now Photino), the contracts
above it should not need to change.

---

## The three layers

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation (Face)                                         │
│  windows · tabs · editors · chat · layouts · mission         │
│  progress · notifications · rendering · user interaction     │
│  No reasoning. No filesystem logic. No terminal logic.       │
│  No Git logic. Talks only to the Client Runtime.              │
└───────────────────────────┬────────────────────────────────┘
                             │  Client Runtime API (application contract)
┌───────────────────────────▼────────────────────────────────┐
│  Client Runtime (Hands)                                       │
│  Owns ALL local execution + is the security enforcement       │
│  point. Exposes capability interfaces, not implementations.   │
│  Capability Registry advertises what's available here.        │
└───────────────────────────┬────────────────────────────────┘
                             │  Mission Runtime protocol (/v1, tool_use / tool_result)
┌───────────────────────────▼────────────────────────────────┐
│  Mission Runtime (Brain)                                      │
│  planning · reasoning · expert orchestration · mission         │
│  execution · LLM interaction · deciding which capabilities     │
│  should be invoked. Knows nothing about HOW a capability is    │
│  implemented — only that it exists.                            │
└─────────────────────────────────────────────────────────────┘
```

### Mission Runtime (Brain)

Responsible for planning, reasoning, expert orchestration, mission execution, LLM interaction, and
deciding which capabilities should be invoked. It deliberately knows nothing about how a capability
is implemented — only that the client advertises it. A client treats the Mission Runtime as an
external service; its internal reasoning architecture (`PipelineRunner`, expert resolution,
`AgenticSession` when the Mission Runtime hosts the loop itself) is out of scope for any client.

### Client Runtime (Hands)

Owns all local execution: filesystem, terminal, git, docker, browser automation, clipboard,
notifications, secrets, ssh. It contains no reasoning — it executes requests the Mission Runtime
sends and returns results.

It exposes **capability interfaces, not concrete implementations**:

```csharp
namespace ForgeMission.ClientRuntime.Capabilities;

public interface IFileProvider { /* Read, Edit, Write, Exists — see 43.8 */ }
public interface ITerminalProvider { /* Bash/shell execution — see 43.8 */ }
public interface IGitProvider { /* status, diff, commit, branch — new surface, see 43.8 */ }
public interface IDockerProvider { /* container lifecycle as a requestable capability — new surface */ }
public interface IBrowserProvider { /* automation/screenshot as a capability — new surface */ }
public interface IClipboardProvider { /* new surface */ }
public interface INotificationProvider { /* new surface */ }
public interface ISecretsProvider { /* new surface */ }
public interface ISshProvider { /* new surface */ }
```

A **Capability Registry** sits above these, advertising which capabilities are available on this
particular client instance. The Mission Runtime reasons over capabilities, not implementations —
it asks "can this client edit files," not "does this client have `LocalDiskWorkspace`." This is a
genuine capability upgrade over today's static tool list
([`AgentToolDeclarations`](../../src/ForgeMission.Core/Tools/AgentToolDeclarations.cs) is currently
hand-written and fixed per mission) — see [43.8](../phases/phase-43.8-capability-provider-pattern.md)
for the migration path from today's four tools to this shape.

**`IFileProvider`/`ITerminalProvider` are not new inventions — they generalize what already
exists.** [43.7](../phases/phase-43.7-workspace-provider.md)'s `IWorkspace` already unifies file I/O
and process execution behind one interface (a deliberate choice, backed by prior-art research into
OpenHands/Claude Agent SDK/Codex CLI — see that spoke). The Capability Provider pattern is that same
shape, generalized to more capability kinds and given a registry so the Mission Runtime can discover
what's available rather than assuming a fixed four-tool set.

### Presentation (Face)

Presentation only: windows, tabs, editors, chat, layouts, rendering, mission progress,
notifications, user interaction. No reasoning, no filesystem logic, no terminal logic, no Git
logic. The UI talks only to the Client Runtime — it never invokes a capability provider directly,
and it never talks to the Mission Runtime directly either (see "Communication flow" below).

For Forge Desktop specifically, Presentation is a Blazor WebAssembly UI. WASM code runs sandboxed
in the browser engine by design — it cannot touch the file system or spawn processes. That's not a
limitation to work around; it's *why* the Client Runtime exists as a separate, unsandboxed process.
See [43.11](../phases/phase-43.11-wasm-photino-shell.md) for the desktop-specific hosting decision.

---

## Security responsibility

The Client Runtime is also the desktop's security enforcement point. It:

- validates capability requests,
- enforces local policy,
- validates arguments,
- requests user approval when required,
- dispatches the request to the capability provider,
- audits execution.

**Capability providers do not authorize themselves.** They stay focused on execution; authorization
happens before a provider is ever invoked. This is a single enforcement point instead of trusting N
independently-written providers to each get authorization right — see
[43.9](../phases/phase-43.9-client-runtime-authorization.md).

This mirrors a principle already locked elsewhere in this project:
[39.7](../phases/phase-39.7-exec-secret-isolation.md) flagged that a `kind: exec` step must have no
platform provider key in its environment *by construction*, not by scrubbing. The Client Runtime's
authorization layer is the same idea one level up — enforcement is structural, not something each
capability implementation has to remember to do correctly.

**Three distinct questions, three distinct layers — do not conflate them:**

```
Mission Runtime:    "What should happen next?"        (reasoning / planning)
Client Runtime:      "Is this capability authorized?"   (local policy enforcement)
Operating System:    "Can this operation physically      (OS-level permissions,
                       be performed?"                      file locks, process limits)
```

Depending on local policy, a capability request may be automatically approved, automatically
denied, administrator-controlled, or require explicit user confirmation.

**This is a different mechanism from mission-level human-in-the-loop.**
[43.5](../phases/phase-43.5-human-in-the-loop.md)'s `kind: human` expert/`Suspended` outcome is a
*mission-level* gate — "should this step run at all," decided in MCL, at the language/execution
level. Client Runtime capability authorization is a *capability-level* guard — "this specific
`Bash` call looks risky, confirm before executing," decided locally, independent of what the
mission itself does or doesn't gate. A mission can have no human-in-the-loop steps at all and still
have every one of its tool calls pass through capability authorization; the two are complementary,
not the same mechanism wearing two names.

---

## Communication flow

```
User
  │
  ▼
Blazor UI  ───────────────────────────────────────────┐
  │  (Client Runtime API, e.g. HTTP/fetch)             │
  ▼                                                     │
Client Runtime                                          │
  │  (Mission Runtime protocol, /v1/messages)           │  UI never talks
  ▼                                                     │  directly to a
Mission Runtime  ── reasons, decides ──►  tool_use      │  capability
  │                                          │           │  provider or to
  │  ◄── Client Runtime dispatches ──────────┘           │  the Mission
  ▼                                                     │  Runtime.
Client Runtime                                          │
  │  Authorization ──► Capability Provider ──► Result   │
  ▼                                                     │
Mission Runtime (continues reasoning with the result)   │
  │                                                     │
  ▼                                                     │
UI updates  ◄──────────────────────────────────────────┘
```

Providers never authorize themselves; authorization always occurs before dispatch. The Mission
Runtime never drives more than one turn per protocol call — this is unchanged from the existing
[42.3](../phases/phase-42.3-tool-capable-enriching-responder.md) tool-round-trip mechanism, reused
verbatim, not reinvented for this architecture.

---

## Transport is infrastructure, not architecture

The UI does not depend directly on HTTP. It depends on a transport-independent contract:

```
Blazor UI
   │
   ▼
IClientRuntimeChannel
   │
   ├── HttpClientRuntimeChannel   (initial implementation — HTTP + SSE/WebSockets where needed)
   └── GrpcClientRuntimeChannel   (future implementation, same contract)
   │
   ▼
Client Runtime
```

The first implementation uses HTTP together with server-sent events (and WebSockets where
appropriate for streaming). This follows the same provider pattern used throughout Forge —
capability contract → capability provider, model contract → model provider, storage contract →
storage provider, transport contract → transport provider. Swapping the transport implementation
later (HTTP → gRPC, for example) should never require a UI change, only a new
`IClientRuntimeChannel` implementation. See
[43.10](../phases/phase-43.10-transport-contract.md).

---

## Mission Runtime resolution belongs to an orchestration layer, not the Client Runtime

**Decision (2026-08-04):** Deciding *where* the Mission Runtime lives — a local Docker container, an
already-running `forge serve`, a hosted `forge.katasec.com` URL — and, if that resolution requires
starting something, supervising that process, is the job of a **surface-agnostic orchestration
layer**. It is never the Client Runtime's own decision.

This follows directly from the principle already stated above: *"A client... should not care where
the Mission Runtime lives."* A Client Runtime that defaults to starting Docker itself unless told
otherwise is making exactly the location decision that principle says a client shouldn't make.

- The orchestration layer resolves the Mission Runtime URL **before** the Client Runtime starts, and
  hands it in already-resolved (config/env) — the Client Runtime becomes a pure consumer of a URL,
  with no embedded Docker-mode default and no location logic of its own.
- This orchestration layer is **shared infrastructure**, not specific to any one client — Forge
  Desktop, `forge webui`, and any future surface (a TUI, etc.) call the same layer rather than each
  re-implementing "start Docker locally" independently.
- Supervision of whatever the orchestrator starts must account for **every termination path** —
  normal quit, external `SIGTERM`, and crash — the same bar [43.11](../phases/phase-43.11-wasm-photino-shell.md)
  already established for `Desktop`↔`ClientRuntime` supervision.
- **Transport protocol is a separate, unaffected concern** — this is exactly the "transport is
  infrastructure, not architecture" principle below, applied one layer up. `IClientRuntimeChannel`
  over HTTP/SSE stays as-is regardless of how the Mission Runtime is resolved or reached.

**Prior art:** sanity-checked against GitHub Copilot's own desktop app and public SDK docs — a
comparable shipped product uses the same shape (thin host process supervising separate sidecar
agent-runtime processes over a narrow protocol, with a single enforcement point in front of tool
dispatch). Full research notes, method, and open implementation questions are in
[43.13](../phases/phase-43.13-mission-runtime-orchestration.md), which is where this becomes a
concrete build.

---

## Native host, UI framework, and the verification constraint

**Decision: Blazor WebAssembly for the UI, Photino for native packaging.**

The UI Claude/Codex develop against is a normal browser-hosted Blazor application — localhost,
DevTools, Playwright, screenshots, hot reload, standard browser tooling, the same zero-setup
verification loop [desktop-interaction-principles.md](desktop-interaction-principles.md) already
established as the reason the Avalonia→Electron pivot happened. **Photino is a thin native
packaging layer around that same application, not the UI framework and not where business logic
lives.** Its job is limited to: native window, native WebView, desktop lifecycle, packaging, OS
integration.

This resolves the verification-tooling tension the WASM/native-host choice originally raised:
development and iteration happen against a plain browser tab (CDP-verifiable, exactly as today),
and Photino only wraps the already-verified application for shipping. It does not require making
sandboxed WASM code do real capability execution — that stays the Client Runtime's job, per the
layer split above.

**One residual, lower-severity risk, not a blocker:** Photino's native WebView on macOS is
WKWebView (WebKit), not Chromium — the only native option macOS offers without bundling a separate
browser engine the way Electron does. WebKit and Chromium are not pixel-identical renderers.
Verifying against a standard browser during development is right, but the actual Photino-packaged
build should still get periodic real checks on macOS, not be assumed to match forever just because
the browser-tab version did.

### Why not MAUI, Avalonia, or Tauri

- **Tauri** introduces an unnecessary Rust host into an otherwise entirely `.NET` application — a
  second language/toolchain for a project that has deliberately avoided that elsewhere (see the
  "no npm/Node, right-size deps" bias already established for `ForgeUI`).
- **Avalonia** moved UI development into XAML, which measurably reduced AI-assisted development
  productivity and duplicated UI technology with the rest of the project (Blazor/`forge.css`
  already exists and works). This isn't a hypothetical concern — it's drawn directly from this
  project's own Avalonia attempt: paid DevTools tier, per-machine license setup, a multi-day
  environment saga, and ultimately an abandoned visual-identity task. See
  [phase-43.2-avalonia-vanilla-shell_completed.md](../phases/phase-43.2-avalonia-vanilla-shell_completed.md).
- **MAUI** is a significantly broader application framework than Forge needs and introduces
  concerns outside Forge's scope (native control renderers per platform, a much larger surface
  area than "host a WebView and package an app").
- **Photino** stays intentionally minimal: it hosts the WebView and packages the app. Everything
  else belongs to Blazor or the Client Runtime. The desktop shell should remain almost invisible.

**Due diligence done, not just planned** — see the finding recorded in
[phase-43.11's locked decisions](../phases/phase-43.11-wasm-photino-shell.md#locked-decisions)
(2026-08-01): genuine yellow flag (both `photino.NET`/`Photino.Native` and especially
`Photino.Blazor` are stale, several unmerged community PRs, one unanswered Linux segfault
report), accepted not deferred, because the mitigation is structural — see below.

### Why Photino, specifically: the shell is intentionally disposable

We didn't choose Photino because it's the best long-term desktop framework. We chose it because,
after the layer split above, the desktop shell became intentionally insignificant — and that
insignificance is the actual risk mitigation for picking a dependency with known maintenance
weaknesses.

All meaningful Forge behavior lives outside the shell: the Blazor WASM UI, the Client Runtime, the
Mission Runtime, the provider model, transport contracts, capability contracts. The desktop shell
is deliberately reduced to: native window, native WebView, application lifecycle, packaging, native
OS integration. Nothing more.

Because of that separation, the shell is disposable. **If Photino disappeared tomorrow, the
expected outcome is "replace the shell," not "rewrite Forge."** That's an architectural success
criterion, not an aspiration: if replacing the desktop shell ever requires changes outside the
Desktop project, that's a violation of one of Forge's core architectural boundaries, not an
acceptable cost of the choice.

In other words, Photino's maintenance risk was de-risked by construction, not by picking a
"safer" framework — it owns almost no business logic, so its health doesn't gate Forge's health.
Photino is simply today's implementation of the Desktop Shell contract; if a better-maintained or
more capable native host emerges later, it should be replaceable with minimal impact to the rest
of the platform. This was one of the architectural objectives converged on during the desktop
design discussions, not an accident of how the code happened to end up.

### Naming the desktop shell project

**Decision (2026-08-01, locked): the project is `ForgeMission.Desktop`, not
`ForgeMission.ClientRuntime.Photino`.** Every other satellite in the `ClientRuntime.*` family is
named for its role (`.Transport`, `.Presentation`, `.TransportProbe`) — never for the library
implementing it; there is no `ForgeMission.ClientRuntime.Kestrel`. `.Photino` was the one exception,
and it directly contradicted the disposability argument two paragraphs up: naming the artifact after
today's implementation library is exactly what makes a future replacement look like a rewrite
instead of a swap. Two changes, not one:

- **Suffix:** `.Photino` → `.Desktop` — names the role (Desktop Shell contract), not the library.
- **Prefix/nesting:** moved out from under `ClientRuntime.` entirely, to a top-level
  `ForgeMission.Desktop`, sibling to `ForgeMission.Cli`/`ForgeMission.ClientRuntime`. The
  `ClientRuntime.*` prefix correctly marks things that are *part of* the Client Runtime
  (`Transport`, `Presentation`); this project isn't one of those — per the layer split above, it
  spawns and wraps the Client Runtime as a subprocess (#1 spawning #2), it doesn't live inside it.
  Nesting it under `ClientRuntime.` mischaracterized that relationship.

This is a pure rename — no behavior change, and it does **not** touch actual `Photino.NET`/
`Photino.Native` usage (the shell is still built on Photino under the hood; only the project that
*wraps* that library is no longer named after it). Renamed 2026-08-01: `Program.cs`, `.csproj`,
`ForgeMission.slnx`, `Makefile`, and `PhotinoShellBoundaryTests` → `DesktopShellBoundaryTests` (see
[43.11](../phases/phase-43.11-wasm-photino-shell.md)). Note this reuses a name previously held by
the now-deleted Avalonia-era shell project — intentional, not a collision: `ForgeMission.Desktop`
names *whichever implementation currently satisfies the Desktop Shell contract*, per the
disposability argument above, and that project is what git history is for.

### Desktop Host abstraction (`IDesktopHost`)

**Decision (2026-08-01, locked): the Desktop Shell contract is a real interface,
`IDesktopHost`, not just prose + a project-reference test.** Same pattern this project already
uses for Model/Storage/Transport/Capability Providers — a stable seam something programs against,
with a swappable implementation behind it. The seam wasn't obvious at first because a native
desktop shell has no external caller (nothing outside it invokes "the shell," it invokes
everything else) — but there *is* an internal caller: `ForgeMission.Desktop/Program.cs`'s
Client-Runtime orchestration (spawn the child process, wait for its ready URL, register
SIGTERM/SIGINT teardown) is itself host-agnostic and was already proven independently correct
through three separate bug fixes (top-level-`await`-on-threadpool, `ProcessExit` not firing on
external `kill`, window-close bypassing the normal return path — see
[43.11](../phases/phase-43.11-wasm-photino-shell.md)). Before this decision, that proven
orchestration code called `PhotinoWindow` directly, so replacing Photino meant rewriting
`Program.cs` wholesale — including re-deriving those three bugs from scratch. `IDesktopHost` moves
the seam to where it belongs: the orchestration now depends only on `IDesktopHost`
(`Load(url)` / `RegisterClosingHandler(Func<bool>)` / `Run()`), and the one line that constructs a
concrete host (`new PhotinoDesktopHost()`) is the entire footprint a replacement host would touch —
mirrors `ProviderClientBuilder`'s switch-case being the one place `IChatClient`'s concrete provider
types are named. (Where the implementation actually lives is covered below — this paragraph is
about the seam, not the project layout.)

**Revised same day: split into three projects, not kept inside `ForgeMission.Desktop`.** The first
cut kept `IDesktopHost`/`PhotinoDesktopHost` inside `ForgeMission.Desktop` itself (both `internal`)
on the grounds that a second project for one implementation would be speculative. That reasoning
was sound for *runtime* swappability (there's still only one host), but missed a *legibility* cost:
with everything in one project, `Program.cs` still had to `using` a namespace that could be read as
naming Photino just to reach the interface, and nothing structurally signaled "this file is the
implementation, that file is host-agnostic" beyond an `internal` modifier — easy for a future
reader (human or agent) to miss and reintroduce coupling. Moved to three projects instead:

- `ForgeMission.Desktop.Contracts` — `IDesktopHost` only, `public`, zero dependencies (not even on
  `ForgeMission.Desktop`).
- `ForgeMission.Desktop.Photino` — `PhotinoDesktopHost`, `public sealed`, the only project allowed
  to depend on the `Photino.NET` package; references `ForgeMission.Desktop.Contracts`.
- `ForgeMission.Desktop` — the exe/composition root. References both of the above; `Program.cs`'s
  orchestration logic (spawn/wait/signal-teardown) reads only `IDesktopHost`, and
  `new PhotinoDesktopHost()` is the only place a *concrete host is constructed* — the project name
  `ForgeMission.Desktop.Photino` itself still appears in that file's `using` directive and a couple
  of explanatory comments, which is expected and fine; the boundary that matters is "no `Photino.NET`
  type reference outside the composition line," not "the string Photino appears once."

No `LinkerArg`/`PublishAot` on the two new projects — those stay exe-only, matching how
`ForgeMission.Core` (a library linked into other AOT exes) is set up; both get `IsAotCompatible`
only, same as `ForgeMission.ClientRuntime.Transport`. `DesktopShellBoundaryTests` was extended
(`[Theory]`/`[InlineData]`) to check both `ForgeMission.Desktop.csproj` *and*
`ForgeMission.Desktop.Photino.csproj` for forbidden references to
`Core`/`ClientRuntime`/`ClientRuntime.Transport` — now precisely testing "the Desktop Host
implementation project," not just the exe that happens to contain it.

**Open, separate from the above — not yet decided:** whether the desktop shell should embed and
self-serve the WASM UI itself, instead of loading a URL served by the Client Runtime's Kestrel host.
Closing *this* question is not implied by closing "is Photino the shell" above, nor by the naming
decision just above it — it's a distinct, independently-revisitable question. Treat the WHY and the
HOW as two separate steps, in this order, and don't let them blur together (a prior discussion
looped by answering "how" — a specific API/mechanism — while the actual question on the table was
still "why"):

- **WHY (motivation) — settled:** packaging simplicity. HashiCorp embeds Vault/Consul's UI into
  their server binaries via `go:embed` for exactly this reason — "single binary deployment, no
  runtime dependencies, no external files to manage" (their own stated rationale, see
  [Vault UI README](https://github.com/hashicorp/vault/blob/main/ui/README.md)). That reasoning
  plausibly applies *more* to a desktop app than to their server case: an ops team deploying Vault
  has tooling/discipline to keep a binary and its asset folder together; an end user who downloaded
  Forge does not — a separated `wwwroot/` folder is a more likely, not less likely, failure mode
  in an unmanaged desktop environment than in a managed fleet deployment. Note the precedent's
  scope, though: HashiCorp embeds into *the server binary itself* (no separate disposable-shell
  layer exists in their architecture) — that maps to what `ForgeMission.ClientRuntime` (#2)
  already does today (Kestrel serves `wwwroot` from the same publish artifact), not automatically
  to whether #1 (`ForgeMission.Desktop`) should *also* carry a copy. Don't re-derive this "why" from
  scratch — reference it.
- **HOW (solutioning) — not yet done, deliberately deferred:** if/how the desktop shell specifically
  would embed and serve the assets (e.g. `Photino.NET`'s `RegisterCustomSchemeHandler` is one
  candidate mechanism, confirmed present in the package version in use), and what it costs — what a
  future shell replacement would additionally need to reimplement (resource embedding, scheme-handler
  wiring, cross-origin handling against the Client Runtime's transport API) versus the
  disposability goal above. This is intentionally not solved here yet — a future session should
  pick up solutioning from the WHY above, not restart the why/how debate.

---

## Capability evolution

Initially, every capability executes inside one Client Runtime process:

```
Client Runtime
├── Files
├── Git
├── Docker
├── Browser
└── Terminal
```

Later, without changing anything above it, any capability implementation may move transparently
into an isolated helper process:

```
Desktop
├── filesystem-host
├── git-host
├── docker-host
├── browser-host
└── terminal-host
```

Neither the Mission Runtime nor the UI should notice this change — only capability implementations
move, never the contracts. This is deliberately deferred, not built now: an abstraction earns its
place when it has a real caller, and today one in-process Client Runtime is sufficient. Split a
capability into its own process only when a concrete need (isolation, a differently-privileged
capability, a capability that needs its own lifecycle) actually shows up.

---

## Architectural goals

Optimize for: simplicity, one primary language (`.NET`/C#) across the stack, clean separation of
concerns, cloud/local parity, implementation-agnostic capabilities, one reusable Client Runtime,
and interchangeable clients.

Future clients that should be able to speak the same Mission Runtime protocol and reuse the same
Client Runtime contracts without forking either:

- Forge Desktop
- Forge Rooms
- CLI
- VS Code
- Open WebUI
- mobile applications

The Mission Runtime remains the stable center of gravity. The Client Runtime remains the stable
execution engine. The desktop — or any other presentation layer — remains replaceable
infrastructure around both.
