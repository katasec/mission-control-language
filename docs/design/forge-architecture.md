# Forge Architecture — Mission Runtime, Client Runtime, Presentation

> **Ownership amendment, 2026-09-06:** the finalized [domain ownership end state](../retrospectives/phase-43-domain-ownership/end-state.md) governs the planned extraction in [43.23](../phases/phase-43.23-domain-ownership.md). It separates Application services/API hosting from Client Runtime's local capability execution. In particular, “all local execution” below means agent capability execution in the target; Project persistence/content access has its own Application owner. The three-layer/process/naming descriptions below record the pre-extraction implementation and remain current deployment guidance until migrated. The linked amendment takes precedence for new ownership decisions; it does not claim the new projects already exist.

**Status: Locked 2026-08-01; durable-conversation extension locked 2026-08-12;
Desktop Supervisor/Host boundary locked 2026-08-17; durable local-runtime bootstrap locked
2026-08-18.** This is the canonical, durable architecture doc for Forge as a
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
                             │  Mission protocol (/v1 compatibility or Forge conversation API)
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

### Durable Conversation Runtime

Some missions are a durable multi-party conversation rather than one stateless model turn. For
those, the Mission Runtime is fronted by the Forge Conversation API: the conversation owns ordered
events and run state, and exposes replayable progress to Desktop and Rooms. It is additive to the
existing '/v1' compatibility doors, not a replacement for them. The Client Runtime still owns local
capability authorization and execution; it receives a tool request from the conversation, executes
it locally, and returns a result to the durable run. The complete storage, Orleans, Table, Blob,
Service Bus, recovery, and reconnect decision is
[durable-conversations.md](durable-conversations.md).

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

### Presentation-surface parity — non-negotiable

Desktop and TUI are interchangeable Presentation surfaces over the same Client Runtime capabilities.
Every supported product action—creating/opening a Project, selecting a mission, submitting an instruction,
launching a run, and reading its trace—must be expressible through a named shared Application
contract (currently packaged as ClientRuntime.Transport) and have no Desktop/Blazor/native-Host-only business path. Future stop/guidance actions must satisfy the same rule when designed. A surface may choose its own
layout, input, focus, and accessibility behaviour; those presentation details are not a separate
product capability. A new Desktop action is not ready until a TUI could invoke its underlying
contract with the same authorization, outcome, and failure semantics.

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

**Decision (2026-08-04):** *Where* the Mission Runtime lives — a local Docker container, an
already-running `forge serve`, a hosted `forge.katasec.com` URL — is a choice the **user makes
through the presentation surface** (GUI, TUI, whatever form a given client takes). Neither the
Client Runtime nor the orchestration layer infers or defaults it. A **surface-agnostic orchestration
layer** takes that already-made choice and carries it out: starting/supervising a process if the
choice requires one, then resolving to a URL. It is never the Client Runtime's own decision, and it
is not the orchestrator's own decision either — only the user's, relayed through the surface.

This follows directly from the principle already stated above: *"A client... should not care where
the Mission Runtime lives."* A Client Runtime that defaults to starting Docker itself unless told
otherwise is making exactly the location decision that principle says a client shouldn't make.

- The orchestration layer resolves the Mission Runtime URL **before** the Client Runtime starts, and
  hands it in already-resolved (config/env) — the Client Runtime becomes a pure consumer of a URL,
  with no embedded Docker-mode default and no location logic of its own. It fails fast at startup if
  that URL is missing, rather than silently defaulting.
- This orchestration layer is **shared infrastructure**, not specific to any one client — Forge
  Desktop, `forge webui`, and any future surface (a TUI, etc.) call the same layer rather than each
  re-implementing "start Docker locally" independently.
- Supervision of whatever the orchestrator starts must account for **every termination path** —
  normal quit, external `SIGTERM`, and crash — the same bar [43.11](../phases/phase-43.11-wasm-photino-shell.md)
  already established for `Desktop`↔`ClientRuntime` supervision.
- **Transport protocol is a separate, unaffected concern** — this is exactly the "transport is
  infrastructure, not architecture" principle below, applied one layer up. `IClientRuntimeChannel`
  over HTTP/SSE stays as-is regardless of how the Mission Runtime is resolved or reached.
- **Auth and billing don't share an answer, and treating them as one "gateway" concern was itself a
  mistake worth correcting here.** Request classification is already uniform for free — it lives
  inside the runner, and the runner image is already identical local/cloud (see below) — no new work.
  Auth reduces to Client Runtime always sending a credential (even a local placeholder) with the
  local runtime simply never validating it — not a no-op check, no check at all, zero new code.
  Billing is deliberately **not** mirrored locally with a no-op ledger: billing is already
  exclusively server-side in the real architecture (`ForgeAPI` debits off usage the runner reports,
  never something the client does), the runner already reports that usage identically in both
  targets, and building a local no-op ledger component would protect against a requirement — paying
  users — that doesn't exist locally. Turning on real local billing later is writing a small consumer
  of data already being reported, not flipping a switch on infrastructure built today for no present
  reason. This still mirrors [43.9](../phases/phase-43.9-client-runtime-authorization.md)'s Client
  Runtime authorization pattern — one enforcement point, policy varies — for the piece that
  genuinely is one enforcement point (auth); it does not mean inventing a matching enforcement point
  where none is needed (local billing). Full reasoning is in
  [43.13](../phases/phase-43.13-mission-runtime-orchestration.md).

**Prior art:** sanity-checked against GitHub Copilot's own desktop app and public SDK docs — a
comparable shipped product uses the same shape (thin host process supervising separate sidecar
agent-runtime processes over a narrow protocol, with a single enforcement point in front of tool
dispatch). Full research notes, method, and the concrete implementation shape (project boundaries,
env-var injection, a thin `IMissionRuntimeLauncher` for testability) are in
[43.13](../phases/phase-43.13-mission-runtime-orchestration.md), which is where this becomes a
concrete build.

---

### Durable Conversation Runtime local-proof bootstrap

The durable Conversation Runtime is an independent boot dependency of a Janus-capable Desktop, not an optional value that may drift into Client Runtime configuration. During the current Kind proof, the Desktop Supervisor composes one `ConversationRuntimeResolver` result before starting Client Runtime:

1. use the existing `ConversationRuntime:BaseUrl` override when present and valid; otherwise use the sole current local default, `http://127.0.0.1:18080/`;
2. verify `GET /health` succeeds before Client Runtime starts;
3. for the default only, start and own a localhost-only `kubectl port-forward` to Kind's `conversation-host` service if no healthy endpoint already exists; and
4. pass only the verified URL to Client Runtime, then dispose only a tunnel the Supervisor itself started.

The Supervisor does not provision Kind, deploy Host/Worker images, read development secret values, or grant credentials; Forge Infra's explicit Kind workflow owns those operations. Client Runtime and Presentation receive no data-plane or cluster credential. This is a Type-2 local-proof adapter: when the authenticated cloud edge exists, replace this resolver's default with the edge URL and remove the local tunnel branch. Do not point a released client at the internal ConversationHost.

## Desktop Supervisor and native host are separate processes

**Decision (2026-08-17, locked): the user-launched `ForgeMission.Desktop` process is the
Desktop Supervisor. It is never the native host.** It owns Mission Runtime resolution, the Client
Runtime child, the launcher, startup cancellation, and every cleanup path. It starts the native
host as a separate child process and remains alive after that child exits. A concrete host — Photino
today, another host tomorrow — owns only a native window and its WebView.

```
ForgeMission.Desktop (Desktop Supervisor)
  ├─ ForgeMission.Desktop.Host (disposable native host) ── native window + WebView
  ├─ ForgeMission.ClientRuntime                              local capability boundary
  └─ Mission Runtime launcher/container                      reasoning runtime
```

This is a process boundary, not a convention. The Supervisor has no `Photino.NET` reference and
never constructs `IDesktopHost`; `ForgeMission.Desktop.Host` is the only composition root that does.
`ForgeMission.Desktop.Photino` remains only one implementation behind that host contract. Replacing
the host therefore cannot move runtime ownership, cleanup, credentials, or capability dispatch into
the replacement.

### Lifecycle contract

- **Boot:** the Supervisor starts the Host first. The Host displays local, static `Booting` content
  without requiring a Client Runtime URL. The Supervisor resolves and starts runtimes in the
  background, then tells the Host to navigate to the ready Client Runtime URL.
- **Window close or Host crash:** the Host may exit immediately. The Supervisor observes its child
  process exit; it does not rely on a host callback being delivered. It cancels startup, gracefully
  stops Client Runtime, disposes the Mission Runtime launcher, and then exits. There is no
  in-window `Closing` state after the user has closed the only window.
- **Supervisor shutdown:** SIGTERM/SIGINT and normal supervisor shutdown run the same exactly-once
  cleanup owner. The Supervisor terminates the Host as part of its own shutdown; host exit alone
  never owns runtime cleanup.
- **Supervisor abnormal termination:** this process model does not claim cleanup after an uncatchable
  Supervisor crash or force-kill. That requires a separately designed parent-death containment
  mechanism and is not silently delegated to the Host. Until such a mechanism is designed and
  verified, the supported cleanup observations are normal Host exit/crash and Supervisor
  SIGTERM/SIGINT only.
- **Failure/retry:** the Host can render a locally supplied failure state and send a retry request;
  only the Supervisor may retry resolution or start children. A Host restart is never a second
  runtime startup.

The control contract is two inherited, local anonymous pipes. The complete public contract in
`ForgeMission.Desktop.Contracts` is deliberately this small:

```csharp
public enum DesktopHostCommandKind : byte { Navigate = 1, ShowFailure = 2 }
public readonly record struct DesktopHostCommand(DesktopHostCommandKind Kind, string Payload);
public enum DesktopHostEventKind : byte { RetryRequested = 1 }
public readonly record struct DesktopHostEvent(DesktopHostEventKind Kind);
```

Supervisor → Host sends only `Navigate(url)` and `ShowFailure(message)`; Host → Supervisor sends
only `RetryRequested`. Each pipe frame is `[kind: byte][UTF-8 payload byte count: Int32 little-endian]
[payload]`; `RetryRequested` has an empty payload. `Navigate` accepts an absolute loopback URL and
`ShowFailure` accepts display text. No other command, options record, acknowledgement, listener,
HTTP endpoint, or generic event bus is permitted without a fresh architecture decision. The
Supervisor also waits on the Host process, which is the authoritative host-exit signal if a pipe
message is lost. The Host receives no platform key, Mission Runtime credential, or capability
implementation; the URL is the only runtime value it needs.

### Consequences and enforcement

- A host's close callback must return immediately if it has one, but no host needs a close veto for
  correct runtime cleanup. `IDesktopHost` must not grow a generic scheduler, process API, or
  “keep the app alive” capability.
- `DesktopLifecycle` belongs to the Supervisor and is the sole owner of runtime state
  (`Booting`, `Ready`, `Failed`, `Stopping`, `Stopped`) and exactly-once cleanup. The Host owns only
  its visible local state.
- The process boundary is Type 2: the control transport and concrete host can change, but the
  Supervisor-only ownership and no-secret Host boundary are fixed. There is no public ingress,
  datastore, or new credential. Inherited pipe handles are the sole local control path.
- A boundary test must prove the Supervisor has no project/package reference to a concrete host and
  the Host has no reference to `Core`, `Orchestration`, `ClientRuntime`, or capability providers.
  Published-AOT verification must prove: visible boot before delayed runtime readiness; a normal
  window close leaves no Client Runtime or container; an unexpected Host kill does the same; and a
  supervisor SIGTERM terminates all children.

This corrects the former same-process composition, where the Supervisor accidentally constructed the
native host and a native close callback became responsible for process cleanup. That implementation
shape is not an alternative architecture and must not be reintroduced.

---

## Native host, UI framework, and the verification constraint

**Decision: Blazor WebAssembly for the UI, Photino for native packaging.**

The UI Claude/Codex develop against is a normal browser-hosted Blazor application — localhost,
DevTools, Playwright, screenshots, hot reload, standard browser tooling, the same zero-setup
verification loop [desktop-interaction-principles.md](desktop-interaction-principles.md) already
established as the reason the Avalonia→Electron pivot happened. **Photino is a thin native
packaging layer around that same application, not the UI framework and not where business logic
lives.** Its job is limited to: native window, native WebView, local Host rendering, packaging, and
OS integration. Runtime lifecycle belongs to the Desktop Supervisor above.

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
is deliberately reduced to: native window, native WebView, local window lifecycle, packaging, native
OS integration. Nothing more.

Because of that separation, the shell is disposable. **If Photino disappeared tomorrow, the
expected outcome is "replace the Host adapter," not "rewrite Forge."** That's an architectural
success criterion, not an aspiration: if replacing the native host requires changes outside
`ForgeMission.Desktop.Host` and its selected adapter, that's a violation of one of Forge's core
architectural boundaries, not an acceptable cost of the choice.

In other words, Photino's maintenance risk was de-risked by construction, not by picking a
"safer" framework — it owns almost no business logic, so its health doesn't gate Forge's health.
Photino is simply today's implementation of the native Host contract; if a better-maintained or
more capable native host emerges later, it should be replaceable with minimal impact to the rest
of the platform. This was one of the architectural objectives converged on during the desktop
design discussions, not an accident of how the code happened to end up.

### Naming the Desktop processes

**Decision (2026-08-17, locked): `ForgeMission.Desktop` names the user-launched Supervisor, and
`ForgeMission.Desktop.Host` names the disposable native-host executable.** Neither name contains a
concrete framework. `ForgeMission.Desktop.Photino` names only today's adapter library; it is never
the user entry point and may be replaced without renaming or moving the Supervisor.

This corrects the old single-process naming, where `ForgeMission.Desktop` ambiguously meant both
the user-facing app and the native shell. The public `ForgeMission.Desktop` entry point stays stable;
the process it starts is an implementation detail behind the fixed Supervisor↔Host contract.

### Desktop Host abstraction (`IDesktopHost`)

**Decision (2026-08-01, locked): the Desktop Shell contract is a real interface,
`IDesktopHost`, not just prose + a project-reference test.** Same pattern this project already
uses for Model/Storage/Transport/Capability Providers — a stable seam something programs against,
with a swappable implementation behind it. Its only caller is the **Host process**, not the Desktop
Supervisor. `IDesktopHost` covers local window/WebView operations: showing Host-owned markup, showing
a ready URL, hearing the one local Retry click so the Host can translate it into the locked
`RetryRequested` event, and running the native loop. It does not own processes, credentials, cleanup,
or a generic scheduler. In particular, a host close veto is not part of the Supervisor lifecycle
design.

The former same-process composition made `ForgeMission.Desktop/Program.cs` both runtime supervisor
and `IDesktopHost` caller. That was an implementation error: it coupled host exit to runtime cleanup
and made a host-specific close callback look architecturally important. The split in
`Desktop Supervisor and native host are separate processes` above is the current rule; do not use
the old composition as precedent.

**Revised same day: split into three projects, not kept inside `ForgeMission.Desktop`.** The first
cut kept `IDesktopHost`/`PhotinoDesktopHost` inside `ForgeMission.Desktop` itself (both `internal`)
on the grounds that a second project for one implementation would be speculative. That reasoning
was sound for *runtime* swappability (there's still only one host), but missed a *legibility* cost:
with everything in one project, `Program.cs` still had to `using` a namespace that could be read as
naming Photino just to reach the interface, and nothing structurally signaled "this file is the
implementation, that file is host-agnostic" beyond an `internal` modifier — easy for a future
reader (human or agent) to miss and reintroduce coupling. Moved to three projects instead:

- `ForgeMission.Desktop.Contracts` — `IDesktopHost` and the fixed Supervisor↔Host pipe records,
  `public`, zero dependencies (not even on `ForgeMission.Desktop`).
- `ForgeMission.Desktop.Photino` — `PhotinoDesktopHost`, `public sealed`, the only project allowed
  to depend on the `Photino.NET` package; references `ForgeMission.Desktop.Contracts`.
- `ForgeMission.Desktop.Host` — the Host executable/composition root. References Contracts and the
  selected concrete adapter; this is the only process that constructs `IDesktopHost`.
- `ForgeMission.Desktop` — the Supervisor executable. References Contracts and Orchestration; it
  starts the Host executable and runtime children, but has no host package and never names or
  constructs `IDesktopHost`.

No `LinkerArg`/`PublishAot` on Contracts or adapter libraries — those stay exe-only, matching how
`ForgeMission.Core` (a library linked into other AOT exes) is set up; they get `IsAotCompatible`
only. The Supervisor and Host executables each publish Native AOT. This split is enforced by
`DesktopSupervisorHostBoundaryTests` (added by Task 2): the Supervisor cannot reference a concrete-host package and
its source cannot name `IDesktopHost`; the Host cannot reference `Core`, `Orchestration`,
`ClientRuntime`, or capability providers; and the Photino adapter cannot reference those runtime
projects either.

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
  to whether #1 (`ForgeMission.Desktop.Host`) should *also* carry a copy. Don't re-derive this "why" from
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
