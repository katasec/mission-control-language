# Phase 43.17 — Responsive Desktop lifecycle and UI

> **Status: design spike active (2026-08-16).** The current Desktop can block the macOS native
> event path during startup and shutdown, and it renders every streamed event immediately. First
> resolve the narrow Photino main-thread lifecycle mechanism; only then implement the named
> lifecycle and event-flow changes below. Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md).

## Read boundary

Read this spoke first. Then read only:

1. [Forge Architecture — Mission Runtime resolution](../design/forge-architecture.md#mission-runtime-resolution-belongs-to-an-orchestration-layer-not-the-client-runtime),
   [native host/UI](../design/forge-architecture.md#native-host-ui-framework-and-the-verification-constraint),
   and [Desktop Host abstraction](../design/forge-architecture.md#desktop-host-abstraction-idesktophost).
2. The source file named by the task being executed.
3. [Security Architecture](../design/security-architecture.md) and
   [Engineering Philosophy](../design/engineering-philosophy.md) only for the design-gate review
   below.

Do **not** reload the Phase 43 parent’s historical framework pivots, Janus implementation detail,
or unrelated task spokes. They are not dependencies of this work.

## Outcome

The shell reacts immediately. Native callbacks only make small state transitions; process, Docker,
network, and stream work is outside the rendering path. The user can see boot/closing state while
the work continues, and a stream cannot turn into unbounded memory or one render per token.

## Current evidence

| Location | Current behavior | Consequence |
|---|---|---|
| `src/ForgeMission.Desktop/Program.cs` | `ResolveAsync(...).Wait()` resolves/starts the Mission Runtime before the Photino host exists; `WaitForReadyUrl` blocks for up to 20 seconds. | A slow Docker pull or child start leaves no useful native UI to render. |
| `src/ForgeMission.Desktop/Program.cs` | The synchronous Photino close callback calls `KillIfRunning`, which waits up to 10 seconds for Client Runtime, then synchronously disposes the Mission Runtime launcher. | **P0:** AppKit’s close/UI path can beachball while process/Docker cleanup runs. |
| `src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor` | `AddFolderAsync` starts a new event loop without stopping the existing default-session loop. | Duplicate SSE subscribers can process later events twice. |
| `src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor` | Each SSE event mutates state and invokes `StateHasChanged`; text appends allocate a new string for every delta. | Render pressure and growing per-turn string-copy cost during long streams. |
| `src/ForgeMission.ClientRuntime/Transport/ClientRuntimeEventHub.cs` | Each subscriber has an unbounded channel and every event is forwarded/flushed individually. | A slow WebView can accumulate unbounded queued data. |

The durable Conversation Runtime already has valuable precedents: it owns one tail, cancels it when
the session is replaced, deduplicates by event ID, and reconnects with a cursor. Reuse those
principles; do not introduce a second generic reactive framework.

## Comparable-product research — GitHub Copilot app (2026-08-16)

**Method:** compliance-bounded black-box observation of the installed GitHub Copilot app 1.1.2
(`ps`, `lsof`, launch, and visible UI only), plus GitHub's public documentation. No binary/resource
inspection. This is product inspiration, not an API or implementation dependency.

**Observed:** within three seconds of launch, the native `github` host had started four direct
`copilot --server --stdio --no-auto-update` child processes and was listening only on two loopback
ports. Yet the visible app already presented a complete, focused shell: persistent navigation,
session rail, prompt box, mode/model/effort controls, and immediately useful empty-state content.
The host did not make its first interactive surface wait for all agent workers to become useful.

GitHub's public docs describe the same product shape: independent sessions appear in a persistent
sidebar; an active session owns its workspace and can be switched without turning the entire app
into one global busy operation; and the stream distinguishes ephemeral deltas from persisted
messages/tool results. Sub-agent events carry identity so the main transcript can render only the
parent response while routing other activity to progress/trace UI. See [Copilot app sessions](https://docs.github.com/en/copilot/how-tos/github-copilot-app/agent-sessions) and
[Copilot streaming events](https://docs.github.com/en/copilot/how-tos/copilot-sdk/features/streaming-events).

### Adopt the principle, not the product shape

| Copilot observation | Forge response |
|---|---|
| Shell appears while several workers start. | Task 2 must create/render the native boot shell before Mission Runtime and Client Runtime readiness. A boot state is a product state, not a blocked precondition. |
| One typed stream has transient deltas and replayable completed facts. | Task 4 must classify/buffer text deltas separately from ordered tool/error/durable conversation state. Deltas may coalesce; durable facts cannot be silently dropped. |
| UI has a durable session rail and an active-session focus. | Task 3's single current view/session operation is the v1 prerequisite. Only its events may update the active transcript; session replacement cancels the old subscription before it creates the new one. |
| Permission/interception is outside the agent's core turn. | Keep Forge's existing Client Runtime `ICapabilityDispatcher` boundary; responsiveness work must not let UI rendering or Mission Runtime calls bypass it. |

**Not adopted:** Copilot's stdio transport, four-sidecar startup count, worktree/cloud-sandbox
product model, and specific visual design. Forge retains its existing Client Runtime HTTP/SSE
contract and one Client Runtime process. Parallel/multi-workspace sessions become a separate product
decision only when the current single-active-session lifecycle is proven responsive.

## Locked boundaries

- **Presentation stays presentation-only.** Blazor/WASM may render lifecycle and stream state, but
  it never starts Docker, manages child processes, or dispatches capabilities.
- **Client Runtime remains the security and capability boundary.** This work does not alter
  `ICapabilityDispatcher`, authorization, or the Mission Runtime protocol.
- **Mission Runtime resolution remains in `ForgeMission.Orchestration`.** Responsiveness changes
  when Desktop begins/observes that work, not who chooses the runtime location or owns Docker.
- **`IDesktopHost` remains the native-host seam.** A lifecycle capability belongs there only if the
  Photino spike proves it is necessary to get back onto the native main thread. Do not add a global
  `UiScheduler` or a general reactive framework.
- **Stream policy is fixed, not configurable.** Preserve ordered state/tool/error events; coalesce
  only adjacent text deltas for the same session. A slow subscriber is bounded rather than allowed
  unlimited memory.

## Design gate

| Gate | Answer |
|---|---|
| Bounded context / data ownership | No new context or datastore. Desktop, Client Runtime, and Mission Runtime ownership stays unchanged. |
| Public entry point / tier change | Not applicable: this is loopback Desktop lifecycle and local UI scheduling; no hosted ingress or cross-context datastore path changes. |
| Credentials | No new credential or environment forwarding. The existing Client Runtime-only credential boundary remains intact. |
| Type | Type 2: lifecycle scheduling and per-client event delivery behind existing Desktop/transport contracts. Reversal is confined to the Desktop host and Presentation/transport implementations. |
| Failure ownership | `DesktopLifecycle` owns boot/close state and process cleanup; Client Runtime owns event fan-out; Presentation owns a view operation’s cancellation and rendering. |
| Proof | Fake-host/lifecycle tests, event batching tests, published AOT desktop observation, and an explicit no-orphan check. |

## Dependency-ordered work

### Task 1 — Photino lifecycle spike (design gate; do first)

**Goal:** Establish the current `Photino.NET` package’s supported way to create the window on the
macOS main thread, show a lightweight local boot/closing view, and later request navigation/close
from background completion without violating AppKit affinity.

**Inspect only:** `IDesktopHost.cs`, `PhotinoDesktopHost.cs`, `ForgeMission.Desktop/Program.cs`,
the installed package API/source, and the native-host architecture sections named above.

**Decide and record in this spoke before any implementation handoff:**

1. the smallest `IDesktopHost` lifecycle additions, if any, for boot rendering, main-thread
   navigation, close veto, and final close;
2. the exact owner/thread of each operation; and
3. how a close during boot cancels boot work and still guarantees supervised child/container cleanup.

Do not assume `Task.Run`, an arbitrary `await`, or an undocumented Photino callback is a safe
main-thread marshal. The existing top-level `await` failure is evidence that this must be proven.

**Done when:** a tiny published-AOT macOS probe demonstrates window creation, a visible boot state,
background completion reaching the native host safely, and a vetoed close followed by a programmatic
close — with the API/version and observation recorded here.

### Task 2 — Non-blocking Desktop lifecycle

**Depends on:** Task 1’s recorded mechanism.

Create one focused `DesktopLifecycle` owner in the Desktop composition layer. It exposes a small
state model (`Booting`, `Ready`, `Closing`, `Failed`, `Closed`) and owns exactly-once startup and
shutdown. It calls the existing orchestration resolver and starts Client Runtime outside the native
event path; `IDesktopHost` receives only minimal state/navigation/close actions on its proven
main-thread mechanism.

The normal close callback must veto immediately, display `Closing`, initiate one background
shutdown, and permit final native close only after graceful-first child termination and launcher
disposal complete. SIGTERM/SIGINT remain shutdown paths, but may bypass UI presentation because the
process is exiting. Startup failure becomes a visible retryable/error state, never a silent hang.

**Done when:** fake-host tests prove no native callback waits for lifecycle completion; a deliberately
delayed runtime shows boot state before readiness; and a real published macOS close leaves neither a
Client Runtime child nor Docker Mission Runtime container.

### Task 3 — Session operation ownership and stale-result suppression

**Depends on:** Task 2 only for shared lifecycle terminology; otherwise independent.

Make `Home.razor` own one cancelable session/view operation at a time. Replacing a workspace or
mission cancels and awaits the old event subscription before creating the replacement. Prompt and
setup requests receive a view/session cancellation token plus a generation identity; a late result
must not mutate the newly-selected session. Replace the fire-and-forget event loop with an observed
task whose expected cancellation is silent and whose unexpected failure becomes `Disconnected`.

Do not invent a reusable `LatestOperationWins` framework: this page has the real caller and should
own its short, explicit operation lifetime.

**Done when:** tests or a transport probe show exactly one active subscription after repeated
folder/mission replacement, cancellation of an old prompt cannot update the current session, and a
broken event stream becomes visible/retryable state rather than an unobserved task failure.

### Task 4 — Bounded, frame-friendly event delivery

**Depends on:** Task 3.

Give Client Runtime event fan-out and the Presentation consumer complementary fixed policies:

- a subscriber queue is bounded; it cannot grow indefinitely;
- adjacent `MissionTextDelta` events for one session are coalesced before UI delivery;
- tool, error, session, and durable conversation events retain their order and are never silently
  discarded; and
- Presentation batches queued changes into one render at most once per short frame interval
  (target: 30 Hz), while terminal/error state is delivered with the next scheduled render.

Keep the existing `IClientRuntimeChannel` HTTP/SSE contract. This is an implementation change below
that contract, not a transport rewrite. If a bounded slow consumer must be disconnected, record the
reconnect/recovery behavior explicitly; durable conversation replay remains the authoritative path
for durable events.

**Done when:** a deterministic 1,000-delta test preserves final text, produces bounded render
notifications rather than 1,000 renders, preserves ordered tool/error events, and proves a stalled
subscriber cannot make `ClientRuntimeEventHub` retain unbounded messages.

### Task 5 — Progressive rendering baseline

**Depends on:** Tasks 2–4.

Render the useful shell immediately: boot/closing/error state first, then the active workspace,
session, and transcript as they become available. Keep only the active conversation’s visible tail
in the normal render tree when transcript volume makes that necessary; add history virtualization or
an explicit older-history loader only when measurement demonstrates it. Do not pre-emptively add a
generic cache, background service, or markdown/highlighting pipeline before a real panel uses one.

**Done when:** a normal browser and packaged Photino run show usable shell state before delayed
secondary work completes; the active transcript remains input-responsive under the 1,000-delta
probe; and the UI is checked in both Chromium and the packaged macOS WKWebView.

## Responsive Desktop rules

1. A native/UI callback changes state and returns; it never waits for child, Docker, network, or
   filesystem work.
2. Start the native event loop before potentially slow boot work; render boot progress immediately.
3. Use genuine asynchronous I/O; use a worker only for CPU-bound work or an API with no async form.
4. Keep AppKit/WebView-affine work on the host’s proven main-thread path.
5. Every view-owned long operation has cancellation and a stale-result identity.
6. A replacement awaits cancellation of its prior subscription before it subscribes again.
7. State drives rendering; business/lifecycle operations do not choreograph individual controls.
8. Treat text deltas as buffered data, not one-render instructions.
9. Bound queues and state the recovery behavior for a slow or disconnected consumer.
10. Verify both perceived behavior (visible boot/closing state, responsive input) and lifecycle
    correctness (no orphaned process/container) in the published native app.

## Done when

All five tasks are implemented and verified: the published macOS Desktop displays promptly during a
deliberately delayed boot, never beachballs while close cleanup runs, leaves no child/container
orphan on normal close or SIGTERM, has one cancellable current session subscription, and processes a
long stream with bounded queue/render behavior. `dotnet build src/ForgeMission.slnx` and
`dotnet test src/ForgeMission.slnx` pass with zero failures; the native verification names the exact
observations rather than inferring success from code review.
