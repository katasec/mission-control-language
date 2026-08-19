# Phase 43.19 — Durable conversation runtime supervision — completed work archive

Verification detail and build narrative for finished tasks in
[phase-43.19-conversation-runtime-supervision.md](phase-43.19-conversation-runtime-supervision.md),
moved out per this repo's hub/spoke rule. The active spoke keeps the locked design (Task 2 still
builds against it) and a one-line status per finished task.

---

## Task 1 — Resolve and prepare the durable Conversation Runtime — done 2026-08-20

Four focused units in `ForgeMission.Orchestration`, nothing else in the repo touched:

| File | What it owns |
|---|---|
| `src/ForgeMission.Orchestration/ConversationRuntimeResolver.cs` | `DefaultLocalBaseUrl = "http://127.0.0.1:18080/"` as the sole constant, plus `ConversationRuntimeEndpoint(BaseUrl, IsLocalDefault)`. Existing `ConversationRuntime:BaseUrl` wins when it parses as an absolute http/https URL; trailing slash normalized; anything else throws naming the key. No HTTP, no process. |
| `src/ForgeMission.Orchestration/ConversationRuntimeReadinessProbe.cs` | `GET {baseUrl}health`. `IsHealthyAsync` treats unreachable as "not healthy" rather than an error; `EnsureHealthyAsync` polls to a caller-supplied deadline and throws naming the endpoint and the `350-conversation-kind-up` prerequisite. `StartupBudget` is a fixed 30s constant, polled at 250ms — not configuration. |
| `src/ForgeMission.Orchestration/LocalKindConversationRuntimeTunnel.cs` | Exactly `kubectl port-forward --address 127.0.0.1 --namespace forge-durable service/conversation-host 18080:8080`, built in `PortForwardStartInfo()`. Missing kubectl (`Win32Exception`) or no started process becomes a named prerequisite failure. `DisposeAsync` stops only its own handle and is idempotent. |
| `src/ForgeMission.Orchestration/ConversationRuntimeBootstrap.cs` | `PrepareAsync` → `ConversationRuntimeLease(BaseUrl, OwnedTunnel)`, whose disposal stops only a tunnel this bootstrap started. Configured endpoint: health-checked, never tunnelled. Local default: reused when healthy, otherwise tunnel → health-wait → lease, disposing the tunnel if health never arrives. |

### Decisions locked during implementation

- **One 30s/250ms startup budget for both branches** (Codex, plan approval): a configured endpoint
  is waited on with the same budget, not a single shot, because Client Runtime must not start
  before it is healthy either way.
- **Injection is parameter-scoped, never static** (Codex, plan approval): the probe takes an
  `HttpClient`, the tunnel takes a `Func<ProcessStartInfo, Process?>` spawn argument, and the
  bootstrap's internal overload takes the probe, a tunnel factory and the budget. No mutable
  global test seam exists to reset or leak between tests.
- **Tunnel factory is `Func<IAsyncDisposable>`, not `Func<CancellationToken, Task<IAsyncDisposable>>`**
  (deviation from the approved plan): `Start` is synchronous, so the async signature would have
  been decoration. Cancellation still bounds the health wait that follows.
- **`kubectl` output stays inherited, not redirected**: nothing in this unit drains a pipe, and an
  undrained one would eventually block a long-lived port-forward.

### Verification (2026-08-20)

- `dotnet build src/ForgeMission.slnx` — Build succeeded, 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.slnx` — 773 passed, 0 failed, 11 skipped (pre-existing
  operator-gated integration skips): ConversationHost 139, ConversationWorker 42, ForgeMission
  490, Runner 5, Rooms 97.
- 24 of those are the new focused tests in `src/ForgeMission.Tests/Orchestration/`, run in 273ms
  with no Kind, cloud credential, provider or real network:

| Test file | Proves |
|---|---|
| `ConversationRuntimeResolverTests.cs` | default on unset/whitespace; override wins and is flagged not-default; trailing slash normalized; relative, non-HTTP(S) and malformed overrides each throw naming `ConversationRuntime:BaseUrl`. |
| `ConversationRuntimeReadinessProbeTests.cs` | the request is exactly `GET http://127.0.0.1:18080/health`; 200 healthy, 503 not; unreachable returns false rather than throwing; polling returns as soon as health arrives; never-healthy throws naming endpoint and prerequisite; the budget is 30s. |
| `LocalKindConversationRuntimeTunnelTests.cs` | the argv is exactly the loopback port-forward with no mutating verb and no shell execute; missing kubectl and no-process-started both surface the named prerequisite; disposing a handle that is not running is a safe, idempotent no-op. |
| `ConversationRuntimeBootstrapTests.cs` | healthy default reused with no tunnel started and nothing stopped on disposal; unavailable default starts exactly one tunnel, leases it, and disposes it exactly once; configured endpoint is probed at its own `/health` and never tunnelled, healthy or not; a tunnel that never becomes healthy fails *after* being disposed. |

Test doubles live in `src/ForgeMission.Tests/Orchestration/ConversationRuntimeTestDoubles.cs`
(scripted `/health` handler + fake tunnel).

### Not done here, by scope

`DesktopBoot` still passes `configuration["ConversationRuntime:BaseUrl"]` straight through and
nothing calls `ConversationRuntimeBootstrap` yet — that composition, the unconditional child
environment variable, and the cleanup ordering are Task 2. The packaged macOS Desktop Janus run
against the real Kind service is Task 2's proof, not Task 1's.
