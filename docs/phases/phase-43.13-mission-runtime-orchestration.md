# Phase 43.13 — Mission Runtime resolution & the orchestration layer

**Status: Design — decisions locked 2026-08-04, implementation not started.** Part of
[Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Raised 2026-08-04
during architecture review of the [`DockerMissionRuntime` → `LocalDockerMissionRuntimeLauncher`
rename](phase-43.2a-client-runtime-capability-boundary.md) — the rename fixed a naming collision
with the unrelated `IDockerProvider` capability, but surfaced a real design drift underneath it that
the rename alone doesn't fix. Locked-decision summary lives in
[forge-architecture.md](../design/forge-architecture.md#mission-runtime-resolution-belongs-to-an-orchestration-layer-not-the-client-runtime);
this spoke is where the actual build gets task-broken-down once the design below is final.

## The drift this corrects

[`ForgeMission.ClientRuntime/Program.cs`](../../src/ForgeMission.ClientRuntime/Program.cs) defaults
`MissionRuntime:Mode` to `"docker"` and, unless overridden, starts a local Docker container itself
via `LocalDockerMissionRuntimeLauncher`, falling back to a configured `MissionRuntime:BaseUrl` only
if Docker mode is explicitly turned off. This contradicts the architecture's own stated principle
([forge-architecture.md:31](../design/forge-architecture.md)): *"A client... should not care where
the Mission Runtime lives."* Deciding where the brain lives — and starting it, if that requires
starting something — is not a Client Runtime concern; it leaked in because Client Runtime was the
first (and so far only) process that needed to make the decision.

## Locked decisions (2026-08-04)

- **A surface-agnostic orchestration layer resolves the Mission Runtime location, not the Client
  Runtime.** It decides — local Docker container, an already-running `forge serve`, a hosted
  `forge.katasec.com` URL — and, if starting something is required, supervises that process. This
  logic is shared, not Desktop-specific: `ForgeMission.Desktop`, `forge webui`, and any future
  surface (e.g. a TUI) all call the same layer rather than each re-implementing Docker-start logic.
- **The Client Runtime becomes a pure consumer of an already-resolved Mission Runtime URL.** No
  embedded default, no `MissionRuntime:Mode` branch, no `LocalDockerMissionRuntimeLauncher`
  dependency inside `ForgeMission.ClientRuntime` at all — the orchestrator resolves the URL *before*
  starting Client Runtime and injects it in (config/env, consistent with how `Desktop` already passes
  `ClientRuntime` its startup parameters).
- **Supervision must account for every termination path** — normal quit, external `SIGTERM`, and
  process crash — not just the happy path. [43.11](phase-43.11-wasm-photino-shell.md) already found
  and fixed three distinct ways `Desktop`↔`ClientRuntime` supervision could leak a process; this
  orchestration layer inherits that same bar for whatever it now also supervises (e.g. a local Docker
  container).
- **Transport protocol is unaffected — explicitly not changing.** [43.10](phase-43.10-transport-contract.md)'s
  `IClientRuntimeChannel`/HTTP+SSE stays as-is. This is the same "transport is infrastructure, not
  architecture" principle already locked in
  [forge-architecture.md](../design/forge-architecture.md#transport-is-infrastructure-not-architecture) —
  swappable later if ever needed, not a decision to revisit now.

## Prior art / social proof

Researched the locally installed GitHub Copilot desktop app (`/Applications/GitHub Copilot.app`) and
GitHub's public Copilot SDK documentation to sanity-check this direction against a comparable shipped
product, before committing engineering time to it.

**Method (compliance note, so this isn't re-derived less carefully later):** black-box only — `ps`,
`lsof`, launching and quitting the app, and public web search/fetch of GitHub's own SDK
documentation. No binary disassembly, no `strings` extraction, no unpacking of app resources — an
earlier pass by a different agent had used `strings` on the compiled binary, which was judged to be
over the line (EULAs generally treat that as reverse engineering even though it isn't decompilation);
its findings were discarded and are not reflected here.

**What was found:**

- GitHub Copilot's desktop app (`github` process) spawns three sidecar CLI processes
  (`copilot --server --stdio --no-auto-update`) as direct children, communicating over stdio, not a
  local network port — confirming a supervisor/sidecar shape, not one monolithic process.
- **On quit, one sidecar was left orphaned** (reparented to `launchd` instead of torn down). Directly
  validates that process-supervision-on-shutdown is a real, easy-to-get-wrong problem worth the
  explicit handling 43.11 already built — not this project over-engineering a non-issue.
- GitHub's public Copilot SDK docs (GA June 2026) describe the same shape at the protocol level:
  `Application → SDK Client → JSON-RPC → Copilot CLI (server mode)` — a thin host process talking to
  a separate agent-runtime process. Communication is JSON-RPC over stdio (their transport choice, not
  adopted here — see the locked decision above).
- Their **hook system** (pre/post tool-use, session-start, permission-request interception) is
  functionally the same shape as our own [43.9](phase-43.9-client-runtime-authorization.md)
  `CapabilityDispatcher` — independent validation that "one enforcement point in front of every
  capability dispatch" is the right shape, not something invented in isolation.

## Cross-reference

[43.12](phase-43.12-aot-hygiene-backlog.md) item 2 ("run the AOT-published `ClientRuntime` binary
under its real default Docker-starting startup path") is written against the *current* drifted
behavior. Once this spoke's design lands and Docker-starting logic moves out of `ClientRuntime`
entirely, that item's shape changes — the equivalent AOT-published smoke test should move to
whatever project ends up owning orchestration. Not fixed yet; flagged here so it isn't chased stale.

## Locked decisions — implementation shape (2026-08-04)

The three open questions above are now resolved:

1. **The orchestration layer is its own project, `ForgeMission.Orchestration`** — not folded into
   `ForgeMission.Desktop`. It may be consumed by `ForgeMission.Desktop`, a future `forge webui`
   launcher, a future TUI, or any other surface, so it must not be owned by any one of them.
   `ForgeMission.Orchestration` depends only on `ForgeMission.Docker` (plus, if ever needed, a small
   shared "resolved endpoint" type) — never on `ForgeMission.Core` or `ForgeMission.ClientRuntime` —
   so it can't quietly become Desktop-shaped again.
2. **The resolved Mission Runtime URL is injected via environment variable** —
   `MissionRuntime__BaseUrl` (ASP.NET Core's `__` → `:` section-separator convention, already the
   config key `Program.cs` reads today). The orchestrator resolves the URL first, then sets that env
   var when it spawns `ClientRuntime`. **Client Runtime fails fast at startup if the URL is missing**
   — no silent default, no implicit Docker fallback. Chosen over a config file (extra I/O for no
   benefit) or a CLI arg (env var is the more idiomatic ASP.NET Core convention here, and matches the
   general [12-Factor App](https://12factor.net/config) practice of env vars for settings that vary
   by deploy target).
3. **`LocalDockerMissionRuntimeLauncher` moves wholesale, not a reimplementation** — it's already
   correct and tested. Exact migration:
   - **From:** `src/ForgeMission.ClientRuntime/Services/LocalDockerMissionRuntimeLauncher.cs` +
     `src/ForgeMission.Tests/ClientRuntime/LocalDockerMissionRuntimeLauncherTests.cs`, called from
     `ForgeMission.ClientRuntime/Program.cs`'s `StartLocalDockerMissionRuntimeAsync`/
     `MissionRuntime:Mode` branch (deleted per decision 2 above); `ForgeMission.ClientRuntime.csproj`
     drops its `ForgeMission.Docker` reference (it existed only for this launcher).
   - **To:** `src/ForgeMission.Orchestration/LocalDockerMissionRuntimeLauncher.cs` (keeps its
     `ForgeMission.Docker` reference), tests to
     `src/ForgeMission.Tests/Orchestration/LocalDockerMissionRuntimeLauncherTests.cs` (keeping the
     one shared `ForgeMission.Tests` project, matching existing precedent, rather than a new test
     project). `ForgeMission.Desktop` gains a new `ProjectReference` to `ForgeMission.Orchestration`
     — the new call site that resolves+starts the Mission Runtime, then spawns `ClientRuntime` with
     the resolved URL per decision 2. This doesn't violate `DesktopShellBoundaryTests` — that test
     forbids `Core`/`ClientRuntime`/`ClientRuntime.Transport` specifically, and
     `ForgeMission.Orchestration` is none of those.
   - One adaptation during the move, not a rewrite: the health-wait step should reuse the same
     HTTP-poll-a-readiness-endpoint idiom Client Runtime's own `/ready` endpoint and `Desktop`'s own
     startup handshake already use, rather than a second, different health-check mechanism — matches
     both this codebase's existing precedent and general practice (Kubernetes readiness probes,
     Docker Compose `condition: service_healthy`).

## Done when

Design is closed — all three implementation-shape questions above are resolved. Not yet build-ready
in the sense of a task list with file-by-file steps and a final "verified" bar; that task breakdown
is the next step, not done in this pass. Full spoke is done when: `ForgeMission.Orchestration` exists
and owns Mission Runtime resolution/supervision; `ForgeMission.ClientRuntime` has no Docker awareness
and fails fast without `MissionRuntime__BaseUrl`; `ForgeMission.Desktop` resolves via the new project
before spawning Client Runtime; all termination paths (quit/SIGTERM/crash) are verified clean for
whatever the orchestrator now supervises; full test suite passes.
