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

- **A surface-agnostic orchestration layer carries out the Mission Runtime location — it does not
  infer or decide it.** The choice (local Docker, an already-running `forge serve`, a hosted
  `forge.katasec.com` URL) is made by the user, through the presentation surface (GUI, TUI, whatever
  form a given client takes). The orchestrator's job is to take that already-made choice and act on
  it — starting/supervising a process if the choice requires one, then resolving to a URL. This
  logic is shared, not Desktop-specific: `ForgeMission.Desktop`, `forge webui`, and any future
  surface (e.g. a TUI) all call the same layer rather than each re-implementing Docker-start logic —
  but each surface owns collecting the user's actual choice before calling in; none of them, and
  not the orchestrator either, should default or guess it.
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

   **Why building this ahead of a second confirmed consumer doesn't contradict the YAGNI reasoning
   used elsewhere in this spoke (e.g. against a local billing component):** the two aren't the same
   kind of bet. A project/namespace boundary is a Bezos "Type 2," two-way-door decision — cheap to
   draw, cheap to undo if wrong, so it's fine to lean into a plausible-not-yet-certain need (a TUI is
   a real, live possibility, not idle speculation). A local billing component would be a costlier,
   harder-to-cleanly-unwind commitment (real runtime behavior, an interface, tests to maintain) for a
   need that's speculative *and* gated behind several unproven prior milestones (a working desktop
   client → paying customers → a discovered use case among them needing managed/billed client
   instances) — strict YAGNI applies there instead. The door-reversibility test, not a single rule
   applied uniformly, is what decides which way each of these goes.
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
   - **A thin interface goes in at the same time — not a general provider registry.**
     `LocalDockerMissionRuntimeLauncherTests` today only runs against a real Docker daemon (skipped
     otherwise), and without an abstraction, that same "needs Docker installed" requirement would
     leak into every test of the orchestrator's own resolution/injection logic (decision 2's
     fail-fast, the `MissionRuntime__BaseUrl` wiring, `Desktop`'s resolve-then-spawn sequencing) —
     none of which is actually about Docker. That's a present, concrete need, not a speculative one,
     so:
     ```csharp
     internal interface IMissionRuntimeLauncher : IAsyncDisposable
     {
         string BaseUrl { get; }
     }
     ```
     `LocalDockerMissionRuntimeLauncher` implements it (trivial — it already has `BaseUrl` and
     `IAsyncDisposable`); the resolution function returns `IMissionRuntimeLauncher?` for the "local"
     branch, `null` for "cloud" (an already-configured URL has nothing to start or own). **Deliberately
     not more than this** — no swappable-backend provider pattern, no registry, nothing speculative
     for a second local backend (e.g. Podman) that doesn't exist and isn't planned. YAGNI beyond the
     one, real, present testability need.

## Uniform gateway path — no local/cloud fork (2026-08-04)

Locked, refining decision 2 above: the auth, billing, and request-classification layer in front of
the Mission Runtime is **one code path, always present**, for both local and cloud. Local and cloud
differ only in which concrete implementation gets injected — never in whether the layer exists at
all. The taste behind this, stated explicitly: **"Mac philosophy, not Windows — one consistent shape
everyone can predict, not a customized/divergent variant per environment."** Concretely:

- Client Runtime's request code has one unconditional path — it always attaches a credential, always
  goes through the same shaped gateway — never an `if (isLocal)` branch.
- Someone running Forge locally already expects auth/billing to effectively be no-ops for "a
  personally owned local setup" — a uniform path with a no-op local policy matches that expectation,
  rather than surprising them with a structurally different (or missing) path.
- Forward-compatible for free: turning on real local metering later, if ever wanted, becomes
  swapping which ledger implementation is injected — not building a new path.
- Fewer divergent paths is also a reliability argument, not just a readability one: every additional
  structurally-different path is separate surface area to get wrong, separate surface area to test,
  and a separate place a fix can land in one path and get missed in the other. One path with injected
  policy has one thing to get right and one thing to test — that's less bug surface and easier
  long-term maintenance, not just less to read at a glance.

**What's already uniform, no work needed:** the request classifier (`RequestClassifier`,
[42.3](phase-42.3-tool-capable-enriching-responder.md)) lives inside the runner itself, and
[42.4](phase-42.4-container-convergence.md) already put local and cloud on the same runner image —
so the classifier is already present and identical in both targets. Desktop's traffic (tools +
thinking enabled) always classifies as `Mission` under the existing structural rules — it doesn't
need a carve-out door, it naturally never trips the aux path. **This retires the "reuse API B vs. a
narrower cloud door" fork raised earlier in this same design pass — there is no fork.** Desktop rides
the identical `/v1/messages` + classifier path `claude`/`codex` already use; API B is not
`claude`/`codex`-specific, it's just "the runner's real door," and Desktop uses it exactly as-is.

**Resolved 2026-08-04 — auth and billing don't share an answer, and treating them as one "gateway"
question was the mistake.** Split:

- **Auth needs no new component at all.** It reduces entirely to what decision 2 already
  establishes: Client Runtime always sends an `Authorization` header if one is configured, even a
  placeholder locally. The local runner simply never validates it — that's not "a no-op auth check
  runs," it's "no auth check exists," externally identical but zero new code to write.
- **Billing is a genuine fork — and the resolution is to build nothing locally, not to build a
  no-op mirror of `ForgeAPI`.** In the real (cloud) architecture, billing is already exclusively
  server-side: `ForgeAPI` debits based on usage the *runner* reports, never something Client Runtime
  does itself. The runner already emits usage numbers (tokens, compute-seconds) identically in both
  targets, per 42.4's shared image — that uniformity already exists today, for free. What's
  genuinely absent locally is anything that *reads and acts on* those numbers, and the resolution is
  to leave that absent, deliberately, rather than build a ledger component that protects against a
  requirement (non-paying users) that doesn't exist locally. Turning on real local billing later
  means writing a small consumer of data that's already being reported — not retrofitting new
  instrumentation, and not flipping a switch on a component built today for no present reason.

Net: [43.12](phase-43.12-aot-hygiene-backlog.md)'s and task 5b's framing were fine as originally
written — there's no new "local gateway" item to add to either. Local Docker has nothing extra to
build for auth or billing; both were solved by recognizing they're different questions, not by
inventing local infrastructure.

**Refines decision 2 in "Locked decisions — implementation shape" above:** the
`MissionRuntime__BaseUrl` injection should also carry a credential — even locally, a no-op token — so
Client Runtime's HTTP client code attaches an `Authorization` header unconditionally, with no branch
on target.

## Done when

Design is fully closed — every question raised in this spoke, including the auth/billing split
above, is resolved. Not yet build-ready in the sense of a task list with file-by-file steps and a
final "verified" bar; that task breakdown is the next step, not done in this pass. Full spoke is done
when: `ForgeMission.Orchestration` exists and owns Mission Runtime resolution/supervision;
`ForgeMission.ClientRuntime` has no Docker awareness, always sends a credential (even a local
placeholder), and fails fast without `MissionRuntime__BaseUrl`; `ForgeMission.Desktop` resolves via
the new project before spawning Client Runtime; all termination paths (quit/SIGTERM/crash) are
verified clean for whatever the orchestrator now supervises; full test suite passes. **No local
billing component is in scope for this spoke, by design** — see "Uniform gateway path" above.
