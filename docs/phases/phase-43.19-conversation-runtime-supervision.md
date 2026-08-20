# Phase 43.19 — Durable conversation runtime supervision

> **Status: complete and verified (2026-08-20).** Restore the Supervisor's missing durable-conversation bootstrap contract: one resolved endpoint, healthy before Client Runtime starts, and one owner for any local loopback tunnel it creates. Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md).

## Outcome

Launching Forge Desktop starts a usable Janus-capable client or shows a clear boot failure. The Supervisor resolves the durable Conversation Runtime before spawning Client Runtime, defaults to the current local Kind proof endpoint, verifies its health, and injects the verified URL into its child. A Janus send can never reach a relative `/conversations` URI with no base address.

The existing shared activity visual then becomes verifiable in a real Janus turn. A Rooms-style show/hide trace is separate product work: this task supplies no new conversation events, trace transport, or presentation control.

## Read boundary

Read this spoke first. Then read only:

1. `src/ForgeMission.Desktop/DesktopBoot.cs` and `ClientRuntimeProcess.cs` — current Supervisor composition and child-environment boundary.
2. `src/ForgeMission.Orchestration/MissionRuntimeResolver.cs` and `LocalDockerMissionRuntimeLauncher.cs` — the resolution/lifecycle precedent.
3. `src/ForgeMission.ClientRuntime/Program.cs` — the child’s named `conversation-host` client.
4. `src/ForgeMission.ConversationHost/Program.cs` — the existing `GET /health` readiness route.
5. [Forge Architecture](../design/forge-architecture.md), [Security Architecture](../design/security-architecture.md), and [Engineering Philosophy](../design/engineering-philosophy.md).
6. the sibling `../../../forge-infra/dev/350-conversation-data/README.md` only for the named live Kind proof. Its `make 350-conversation-kind-up` remains the owner of Kind provisioning and application deployment.

Do not change ConversationHost/Worker behaviour, Kind manifests, provider keys, storage/Service Bus credentials, Client Runtime conversation protocol, Presentation mission picker, or the shared activity renderer. Do not begin the deferred trace/workbench work in 43.4.

## Finding

The Supervisor refactor correctly moved Mission Runtime resolution to `ForgeMission.Orchestration`, but left the durable Conversation Runtime as an optional pass-through value:

```text
DesktopBoot: configuration["ConversationRuntime:BaseUrl"]
    -> ClientRuntimeProcess only when non-empty
    -> Client Runtime named HttpClient has no BaseAddress otherwise
    -> Janus's first relative "conversations" request fails
```

The current local proof is healthy at `http://127.0.0.1:18080/health`, but that address is neither a Supervisor default nor checked before Desktop declares its Client Runtime ready. Existing lifecycle tests use a completed fake boot; they prove Host/process cleanup but not this resolver → health → child-environment integration.

## Locked design

### One durable-runtime bootstrap path

Add focused durable-conversation counterparts in `ForgeMission.Orchestration`:

| Unit | Sole responsibility |
|---|---|
| `ConversationRuntimeResolver` | Select and validate one base URL from the existing `ConversationRuntime:BaseUrl` configuration value, or the current local default. It owns no HTTP call or process. |
| `ConversationRuntimeReadinessProbe` | Perform the one bounded `GET {baseUrl}health` observation and fail with a contextual `InvalidOperationException` unless it receives success. No retry-policy configuration is introduced. |
| `LocalKindConversationRuntimeTunnel` | Start and stop only a local `kubectl port-forward` process to the named Kind service. It neither creates a cluster nor deploys, scales, rebuilds, or reads secret values. |
| `ConversationRuntimeBootstrap` | Compose the three units: reuse a healthy default endpoint; otherwise start its tunnel, wait for health, and return its endpoint plus the tunnel it owns. An explicit configured endpoint is health-checked but never causes a local tunnel to start. |

These are concrete local-runtime units, not a generic runtime framework. The existing `IAsyncDisposable` lifecycle shape is sufficient for the tunnel lease; no provider registry, settings UI, or parallel configuration model is introduced.

### Default and override contract

`ConversationRuntimeResolver.DefaultLocalBaseUrl` is the sole constant:

```text
http://127.0.0.1:18080/
```

The already-existing environment/configuration value keeps precedence:

```text
ConversationRuntime__BaseUrl -> valid absolute http/https base URL
otherwise                    -> DefaultLocalBaseUrl
```

The resolver normalizes a trailing slash before the probe/client use a relative route. A non-empty but invalid, relative, or non-HTTP(S) override fails during boot with its configuration key named. There is no new mode, port, timeout, or UI setting.

Readiness is one fixed 30-second startup budget, polling every 250 ms, for both the default and an explicit configured endpoint. A configured endpoint never triggers a local tunnel, but it is still required to become healthy before Client Runtime starts; a bounded wait accommodates a service that is concurrently starting without hiding a permanent error.

### Local default behaviour

For the default only, bootstrap first probes `http://127.0.0.1:18080/health`.

- A healthy endpoint is reused; it was not created by this Desktop process and is never stopped by it.
- If it is unavailable, `LocalKindConversationRuntimeTunnel` starts exactly `kubectl port-forward --address 127.0.0.1 --namespace forge-durable service/conversation-host 18080:8080`, waits for the same health endpoint within the fixed Desktop startup budget, then owns that process until Desktop cleanup.
- If the port is held by something unhealthy, `kubectl` is unavailable, the Kind service/pod is absent, or health does not succeed, boot fails visibly and names the local Conversation Runtime prerequisite. It does not kill another process, fall back to an arbitrary port, or start Kind provisioning.

`make -C ../forge-infra 350-conversation-kind-up` remains the explicit action that creates/reuses Kind, obtains development credentials, builds images, and deploys Host/Worker. The Supervisor only supplies the loopback development adapter after that work exists.

### Supervisor composition and cleanup

After it has started the native Host's boot screen, `DesktopBoot` resolves both runtimes before starting Client Runtime. It obtains the Mission Runtime through the existing `MissionRuntimeResolver` and the durable runtime through `ConversationRuntimeBootstrap`. Only once both are ready does it start Client Runtime, passing the resolved durable URL unconditionally as `ConversationRuntime__BaseUrl`.

`DesktopRuntimes.DisposeAsync` owns cleanup in reverse dependency order: stop Client Runtime, dispose a Supervisor-owned Conversation tunnel, then dispose a Mission Runtime launcher. A failed boot follows that same cleanup path. Neither the native Host nor Client Runtime acquires process or cluster ownership.

When a future authenticated Forge edge routes durable conversations, the resolver's default moves to that edge URL and the local-tunnel branch is removed. It must never point a released client at the internal ConversationHost directly. This is a local-proof Type-2 implementation detail; the conversation data boundary and client/runtime contract are unchanged.

## Design gate

| Gate | Answer |
|---|---|
| Bounded context / ownership | ConversationHost remains the sole Conversation context/ordered-event owner. The Supervisor owns only endpoint preparation and the local tunnel process it starts; Client Runtime retains local capability authority. |
| Public entry point / tiers | No public route changes. The default is loopback-only; `kubectl port-forward` exposes the internal Kind service solely on `127.0.0.1`. Future cloud use must target the authenticated edge, never internal Host ingress. |
| Tier-3 and credentials | Desktop/Client Runtime receive only a base URL. They receive no Storage, Blob, Service Bus, Orleans, provider, or cluster secret. The local `kubectl` invocation uses the operator's context only for port-forwarding; it never issues a mutating cluster command. |
| Cross-context access | None. No component gains direct datastore access or a cross-context query. |
| Type / reversal | Type 2 local-proof connection bootstrap. Removing the local default/tunnel when the authenticated cloud edge is selected removes the exception without changing Contracts, Client Runtime transport, or conversation storage ownership. |
| Failure ownership | Resolver owns invalid endpoint configuration; probe owns unavailable endpoint evidence; tunnel owns only its process; `DesktopBoot` owns composition and cleanup; Host displays the boot failure. |
| Engineering-philosophy result | Four concrete, single-purpose units replace an optional value silently drifting across process boundaries. Existing config wins over one constant; no generic manager, speculative switchboard, or duplicate startup path is added. |
| Proof | Resolver/probe/tunnel and child-environment tests, Supervisor failure/cleanup tests, then a packaged Desktop Janus run against the real Kind service. |

## Desktop Design and Implementation Quality Gate

| Required answer | Result |
|---|---|
| What product behaviour is required? | Launching Desktop produces a Janus-capable Client Runtime only after its selected durable Conversation Runtime is reachable; otherwise the native boot screen gives an actionable failure. |
| Who owns it? | The Desktop Supervisor owns startup order, loopback tunnel lifecycle, readiness, and cleanup. The Host owns only Booting/Failed UI; Client Runtime consumes the verified URL. |
| What has been verified about the adapter? | `ConversationHost` exposes `GET /health`; the current Kind `conversation-host` service is ClusterIP on port 8080 and a live localhost `18080` tunnel returned HTTP 200 on 2026-08-18. |
| Why does it preserve the replacement boundary? | No native Host callback, `IDesktopHost` member, Presentation HTTP call, or Client Runtime process management is added. Switching to a cloud edge changes the resolver default only. |
| What proves it? | Tests prove resolver/probe/tunnel and child injection behaviour; lifecycle tests prove cleanup on failure/exit; a published Desktop Janus prompt reaches the group conversation rather than a URI error. |

**PASS.** This extends the Supervisor's existing process/readiness responsibility without moving runtime lifecycle into the native Host or Presentation.

## Dependency-ordered work

### Task 1 — Resolve and prepare the durable Conversation Runtime — done (2026-08-20)

The four Orchestration units exist and their "Done when" is met: 25 focused tests, no Kind/credentials/provider, full suite 774 passed / 0 failed. Detail and verification: [phase-43.19-conversation-runtime-supervision_completed.md](phase-43.19-conversation-runtime-supervision_completed.md#task-1--resolve-and-prepare-the-durable-conversation-runtime--done-2026-08-20).

Task 2 builds against `ConversationRuntimeBootstrap.PrepareAsync(IConfiguration, CancellationToken)` returning `ConversationRuntimeLease(string BaseUrl, IAsyncDisposable? OwnedTunnel)`, whose `DisposeAsync` stops only a tunnel that bootstrap started. Readiness uses one fixed 30s budget polled at 250ms for both the local default and a configured endpoint.

### Task 2 — Compose it into supervised Desktop boot — done (2026-08-20)

`DesktopBoot` prepares both runtimes before the child starts, injects the verified durable URL unconditionally, and cleans up in reverse dependency order. Its "Done when" is met: 9 focused tests, full suite 783 passed / 0 failed, and a packaged macOS Desktop reaching the Janus group conversation over both the reused operator port-forward and a Supervisor-owned tunnel. Detail and evidence: [phase-43.19-conversation-runtime-supervision_completed.md](phase-43.19-conversation-runtime-supervision_completed.md#task-2--compose-it-into-supervised-desktop-boot--done-2026-08-20).

## Completion condition — met (2026-08-20)

Desktop's current local default starts a verified, usable Janus path without a manually exported endpoint. Endpoint selection, health, local port-forward ownership, and cleanup remain in small Orchestration/Supervisor units. The next cloud route replaces only the default resolver branch; no trace or presentation parity work is silently included.
