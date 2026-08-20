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
| `src/ForgeMission.Orchestration/LocalKindConversationRuntimeTunnel.cs` | Exactly `kubectl port-forward --address 127.0.0.1 --namespace forge-durable service/conversation-host 18080:8080`, built in `PortForwardStartInfo()` — the loopback address and local port are read from `ConversationRuntimeResolver.DefaultLocalBaseUrl`, so the default endpoint has one source of truth; only the Kind half (kubectl, namespace, service, remote port 8080) is the tunnel's own. Missing kubectl (`Win32Exception`) or no started process becomes a named prerequisite failure. `DisposeAsync` stops only its own handle and is idempotent. |
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
- **The tunnel derives its loopback address and local port from the resolver constant** (Codex,
  Task 1 review): the endpoint default has exactly one source of truth, and the command's Kind-side
  facts stay with the tunnel. The emitted command is byte-for-byte unchanged.
- **`kubectl` output stays inherited, not redirected**: nothing in this unit drains a pipe, and an
  undrained one would eventually block a long-lived port-forward.

### Verification (2026-08-20)

- `dotnet build src/ForgeMission.slnx` — Build succeeded, 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.slnx` — 774 passed, 0 failed, 11 skipped (pre-existing
  operator-gated integration skips): ConversationHost 139, ConversationWorker 42, ForgeMission
  491, Runner 5, Rooms 97.
- 25 of those are the new focused tests in `src/ForgeMission.Tests/Orchestration/`, run in ~265ms
  with no Kind, cloud credential, provider or real network:

| Test file | Proves |
|---|---|
| `ConversationRuntimeResolverTests.cs` | default on unset/whitespace; override wins and is flagged not-default; trailing slash normalized; relative, non-HTTP(S) and malformed overrides each throw naming `ConversationRuntime:BaseUrl`. |
| `ConversationRuntimeReadinessProbeTests.cs` | the request is exactly `GET http://127.0.0.1:18080/health`; 200 healthy, 503 not; unreachable returns false rather than throwing; polling returns as soon as health arrives; never-healthy throws naming endpoint and prerequisite; the budget is 30s. |
| `LocalKindConversationRuntimeTunnelTests.cs` | the argv is exactly the loopback port-forward with no mutating verb and no shell execute; its address and local port track `DefaultLocalBaseUrl` rather than repeating it; missing kubectl and no-process-started both surface the named prerequisite; disposing a handle that is not running is a safe, idempotent no-op. |
| `ConversationRuntimeBootstrapTests.cs` | healthy default reused with no tunnel started and nothing stopped on disposal; unavailable default starts exactly one tunnel, leases it, and disposes it exactly once; configured endpoint is probed at its own `/health` and never tunnelled, healthy or not; a tunnel that never becomes healthy fails *after* being disposed. |

Test doubles live in `src/ForgeMission.Tests/Orchestration/ConversationRuntimeTestDoubles.cs`
(scripted `/health` handler + fake tunnel).

### Not done here, by scope

`DesktopBoot` still passes `configuration["ConversationRuntime:BaseUrl"]` straight through and
nothing calls `ConversationRuntimeBootstrap` yet — that composition, the unconditional child
environment variable, and the cleanup ordering are Task 2. The packaged macOS Desktop Janus run
against the real Kind service is Task 2's proof, not Task 1's.

---

## Task 2 — Compose it into supervised Desktop boot — done 2026-08-20

Two production files changed; no other component touched.

| File | What changed |
|---|---|
| `src/ForgeMission.Desktop/DesktopBoot.cs` | Startup order moved into `internal static ComposeAsync(prepareMissionRuntime, prepareConversationRuntime, startClientRuntime, ct)`. It prepares Mission Runtime, then `ConversationRuntimeBootstrap.PrepareAsync`, then starts Client Runtime with `lease.BaseUrl`, then awaits readiness. `StartAsync` keeps the credential check first and wires the three production delegates. Cleanup is one private `StopAsync` in reverse dependency order — Client Runtime, Conversation lease, Mission launcher — shared by the returned `DesktopRuntimes` and the catch path. |
| `src/ForgeMission.Desktop/ClientRuntimeProcess.cs` | `conversationRuntimeBaseUrl` is non-nullable and always set; the "only forwarded when configured" branch is gone. `BuildStartInfo(...)` is extracted so the exact child environment is assertable without spawning a process. |

### The ownership fix caught in review

The first plan had the child-start seam return `Task<(Url, Stop)>`. A readiness
failure would then have thrown before the caller ever held the stop closure,
orphaning a Client Runtime that had already started. The seam is therefore
synchronous and returns `ClientRuntimeStart(Task<string> ReadyUrl, Func<ValueTask> StopAsync)`:
ownership transfers the moment the process exists, and `ComposeAsync` awaits
`ReadyUrl` afterwards. Test 4 below is the regression guard for exactly that.

### Verification (2026-08-20)

- `dotnet build src/ForgeMission.slnx` — Build succeeded, 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.slnx` — 783 passed, 0 failed, 11 skipped:
  ConversationHost 139, ConversationWorker 42, ForgeMission 500, Runner 5, Rooms 97.
- Nine new tests, no Docker/Kind/credential/process involved:

| Test | Proves |
|---|---|
| `DesktopBootTests.StartsClientRuntimeWithTheResolvedDurableUrl` | the child is handed the lease's `BaseUrl` plus the resolver's mission URL/mode. |
| `…NormalBoot_DisposesEachOwnedRuntimeExactlyOnce_InReverseOrder` | disposal order is client → conversation → mission, one each. |
| `…DurableReadinessFailure_StartsNoClientRuntime_AndDisposesTheMissionLauncher` | a durable failure starts no child and disposes the already-started launcher once. |
| `…ClientRuntimeStartedThenReadinessFails_StopsTheStartedClientRuntimeThenLeaseThenLauncher_ExactlyOnce` | the started-but-never-ready child is stopped, then lease, then launcher. |
| `…ChildSpawnFailure_DisposesLeaseAndLauncherExactlyOnce_AndStopsNoClientRuntime` | no ownership returned → conversation → mission only. |
| `…MissionRuntimeFailure_DisposesNothing_AndNeverPreparesTheDurableRuntime` | the durable runtime is never prepared when mission resolution fails. |
| `…CancellationBeforeClientStart_StartsNoClientRuntime_AndDisposesWhatWasPrepared` | a window closed mid-boot leaves nothing running. |
| `ClientRuntimeProcessTests.BuildStartInfo_CarriesBothRuntimesIntoTheChildEnvironment` | `ConversationRuntime__BaseUrl` plus the `MissionRuntime__*` trio, redirected streams, no shell execute. |
| `…BuildStartInfo_SetsTheDurableUrlUnconditionally` | a default-derived URL reaches the child exactly as a configured one does. |

`DesktopLifecycleTests` and `DesktopSupervisorHostBoundaryTests` were not modified and stayed green.

### Packaged macOS Janus proof (2026-08-20)

Environment: Kind cluster `forge-durable` up, `conversation-host` ClusterIP:8080 (age 5d22h),
`make desktop-publish` → `dist/forge-desktop/ForgeMission.Desktop`.

**Run 1 — healthy-default reuse.** An operator `kubectl port-forward` (PID 80970) was already
serving `127.0.0.1:18080`. Desktop booted, and `ps eww` on the packaged child showed
`ConversationRuntime__BaseUrl=http://127.0.0.1:18080/` alongside
`MissionRuntime__BaseUrl=https://api.forge.katasec.com` and `MissionRuntime__Mode=cloud`. No second
port-forward appeared. A Janus prompt through the packaged client reached the durable group
conversation — "Status: Queued", then a Proposer turn, then "Approver is thinking…" — with no
invalid-URI error. On exit, all packaged processes were gone and PID 80970 was **still running**:
Desktop did not stop what it did not start.

**Run 2 — Supervisor-owned tunnel.** With 80970 stopped, `127.0.0.1:18080` refused connections.
Desktop booted and started its own tunnel: PID 20284, parent PID 20276 (the Supervisor), argv
exactly `kubectl port-forward --address 127.0.0.1 --namespace forge-durable service/conversation-host 18080:8080`.
Health went green, the child again received `ConversationRuntime__BaseUrl=http://127.0.0.1:18080/`,
and a Janus prompt again reached the group conversation with a Proposer turn. On exit no
port-forward process remained and `127.0.0.1:18080` refused connections again — the Supervisor
stopped the tunnel it owned.

An HTTP 409 "Conversation already has an active run" appeared in run 2 because the operator clicked
Send twice; that is a real ConversationHost application response over a working base address, not a
transport or URI failure. The dev machine was restored afterwards with an equivalent port-forward.
