# Phase 43.17 — Responsive Desktop lifecycle and UI

> **Status: Supervisor/Host split live (2026-08-17); Task 3 active.** Tasks 1–2 are done — the
> Desktop Supervisor and its disposable native host are separate processes, and the Supervisor owns
> a non-blocking lifecycle with exactly-once cleanup. Task 3 (session ownership) is next; Tasks 4–5
> (bounded event delivery and progressive rendering) are deferred after Task 3 by operator direction
> on 2026-08-17. Part of
> [Phase 43 — Forge Desktop](phase-43-forge-desktop.md).

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

The Supervisor reacts correctly even when a native host exits or crashes. The Host renders a useful
boot state immediately; runtime resolution, process, Docker, and network work live in the
Supervisor, outside native callbacks and rendering. Closing the window closes the disposable Host;
the Supervisor then cleans up every runtime before it exits. A stream cannot turn into unbounded
memory or one render per token.

## Current evidence

| Location | Current behavior | Consequence |
|---|---|---|
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
| Shell appears while several workers start. | Task 2 must start the Host and render its native boot shell before Mission Runtime and Client Runtime readiness. A boot state is a product state, not a blocked precondition. |
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
- **Desktop Supervisor and Host are separate processes.** The Supervisor exclusively owns runtime
  resolution, children, cancellation, and cleanup. The Host is disposable; its exit is an observed
  child-process event, never the cleanup owner. See [Forge Architecture — Desktop Supervisor and
  native host](../design/forge-architecture.md#desktop-supervisor-and-native-host-are-separate-processes).
- **`IDesktopHost` remains a Host-private seam.** The Supervisor does not construct it. Do not add a
  global `UiScheduler`, a process API, or a close-veto requirement to the interface.
- **Stream policy is fixed, not configurable.** Preserve ordered state/tool/error events; coalesce
  only adjacent text deltas for the same session. A slow subscriber is bounded rather than allowed
  unlimited memory.

## Design gate

| Gate | Answer |
|---|---|
| Bounded context / data ownership | No new context or datastore. Desktop, Client Runtime, and Mission Runtime ownership stays unchanged. |
| Public entry point / tier change | Not applicable: this is loopback Desktop lifecycle and local UI scheduling; no hosted ingress or cross-context datastore path changes. |
| Credentials | No new credential or environment forwarding. The existing Client Runtime-only credential boundary remains intact. |
| Type | Type 2: local Supervisor/Host control transport and host implementation. Reversal swaps the Host or pipe transport; it never changes Supervisor ownership of runtime cleanup. |
| Failure ownership | `DesktopLifecycle` in the Supervisor owns boot/stop state and process cleanup; the Host owns only its window/local state; Client Runtime owns event fan-out; Presentation owns a view operation’s cancellation and rendering. |
| Proof | Supervisor/Host boundary tests, event batching tests, published AOT boot observation, and no-orphan checks after host close, host crash, and supervisor signal. |

## Desktop Design and Implementation Quality Gate

Apply the mandatory [Engineering Philosophy gate](../design/engineering-philosophy.md#desktop-design-and-implementation-quality-gate)
before approving a Task 2 plan or its implementation. The design result is below; the implementation
result — with the measured adapter observations and the published-app evidence — is in the
[completed record](phase-43.17-responsive-desktop_completed.md#desktop-quality-gate-result).

| Required answer | Result |
|---|---|
| What product behaviour is required? | Closing the native window ends only the disposable Host; the Supervisor remains long enough to cancel and clean up its runtimes. |
| Who owns it? | The Desktop Supervisor owns runtime/process cleanup. The Host owns only window/WebView state. |
| What has been verified about the adapter? | Photino's macOS close-veto callback is not the lifecycle mechanism: its managed contract promises cancellation, while the current native adapter invokes it post-decision. That is an adapter defect/limitation, not a product-architecture requirement. |
| Why does the proposal preserve the replacement boundary? | No Host callback, `IDesktopHost` member, package patch, or fork participates in runtime cleanup; the Host can be swapped without changing the Supervisor. |
| What proves it? | Boundary tests plus published-AOT observations: Host window close and Host kill leave no Client Runtime child or Mission Runtime container; Supervisor SIGTERM does the same. |

**PASS:** the Supervisor/Host split passes. **FAIL:** the discarded close-veto/Photino-workaround
direction failed because it conflated closing a window with terminating the application and tried to
make an adapter callback own Supervisor cleanup. Any future task that repeats that direction fails
this gate before implementation.

## Dependency-ordered work

### Task 1 — Supervisor / Host boundary (design gate; locked)

The design is locked in [Forge Architecture — Desktop Supervisor and native host are separate
processes](../design/forge-architecture.md#desktop-supervisor-and-native-host-are-separate-processes).
It replaces the former requirement that a concrete Host veto a window close.

**Project and contract shape:**

- `ForgeMission.Desktop` remains the user-launched **Desktop Supervisor**. It references
  `Orchestration` and starts/supervises Client Runtime, Mission Runtime, and the Host child; it has
  no `Photino.NET` dependency and never names or constructs `IDesktopHost`.
- `ForgeMission.Desktop.Host` is a new native-host executable. It owns local Booting/Failed content,
  constructs `IDesktopHost`, and receives no credentials or capability providers.
- `ForgeMission.Desktop.Contracts` owns the inherited-pipe protocol:
  `DesktopHostCommand(DesktopHostCommandKind Kind, string Payload)`, where `Kind` is exactly
  `Navigate` or `ShowFailure`, plus `DesktopHostEvent(DesktopHostEventKind Kind)`, where `Kind` is
  exactly `RetryRequested`. Frames are `[kind: byte][UTF-8 payload byte count: Int32 little-endian]
  [payload]`; `RetryRequested` has an empty payload. No listener, HTTP endpoint, acknowledgement,
  options record, or generic event framework is introduced.
- `ForgeMission.Desktop.Photino` remains the only project that references `Photino.NET`; it is
  consumed only by `ForgeMission.Desktop.Host`.

**Lifecycle:** the Supervisor starts Host first; Host renders `Booting`; Supervisor resolves and
starts runtimes; then it sends `Navigate`. A normal Host window close or an unexpected Host crash is
detected by the Supervisor's child-process wait and triggers one background cleanup. The Supervisor
does not need a Host close callback, and Host exit cannot leave a Client Runtime child or Mission
Runtime container running. A failed boot is shown by `ShowFailure`; only a `RetryRequested` event
allows the Supervisor to retry.

**Done when:** this locked design is reflected in the canonical architecture and this spoke, with
named ownership, control messages, credential boundary, failure paths, and verification observations.

### Task 2 — Non-blocking supervised lifecycle

**Done 2026-08-17** — Supervisor/Host split implemented and verified; see
[phase-43.17-responsive-desktop_completed.md](phase-43.17-responsive-desktop_completed.md#task-2--non-blocking-supervised-lifecycle-done-2026-08-17)
for the build narrative, boundary/lifecycle test evidence, and the six published-app observations.

### Task 3 — Session operation ownership and stale-result suppression

**Depends on:** Task 2 only for shared lifecycle terminology; otherwise independent.

Make `Home.razor` own one cancelable session/view operation at a time. Replacing a workspace or
mission cancels and awaits the old event subscription before creating the replacement. Prompt and
setup requests receive a view/session cancellation token plus a generation identity; a late result
must not mutate the newly-selected session. Replace the fire-and-forget event loop with an observed
task whose expected cancellation is silent and whose unexpected failure becomes `Disconnected`.

Do not invent a reusable `LatestOperationWins` framework: this page has the real caller and should
own its short, explicit operation lifetime.

#### Locked Task 3 recovery decisions (2026-08-17)

`IClientRuntimeChannel.Subscribe` has no cursor or replay route. A retry therefore opens a fresh
subscription for the same session, generation, and cancellation token; it does not recreate the
session, resend a prompt, or claim to restore events missed while disconnected. Expected
cancellation (replacement or disposal) is silent. An unexpected subscription failure shows its
error and a Retry action.

After a successful retry, Presentation keeps a persistent transcript-area notice: **“Reconnected.
Updates that arrived while disconnected are not shown.”** It clears only when a new view begins.
This is deliberately honest about the permanent gap, especially for a durable conversation whose
transcript is event-derived. No UI-only durable-event type or transcript-model entry is added.
Remove the notice only when a replay/cursor path exists below `IClientRuntimeChannel`; the deferred
Task 4 owns that work.

While disconnected, Mission-kind prompts remain enabled because their final `PromptResponse` is
rendered independently of the event stream. DurableConversation prompts are disabled until Retry
succeeds because their visible transcript is event-derived and would otherwise acquire another
unrecoverable gap. Replacing a workspace or mission cancels an in-flight prompt and silently
discards its late result; the user-selected replacement must not wait for it.

**Done when:** tests or a transport probe show exactly one active subscription after repeated
folder/mission replacement, cancellation of an old prompt cannot update the current session, and a
broken event stream becomes visible/retryable state rather than an unobserved task failure. A retry
is explicitly proven to display the persistent gap notice, which clears only on session replacement.

### Task 4 — Bounded, frame-friendly event delivery

**Depends on:** Task 3.

> **Deferred after Task 3 (operator direction, 2026-08-17).** Do not begin this task unless it is
> deliberately reselected; see [Backlog](../backlog.md#deferred-responsive-desktop-follow-up).

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

> **Deferred after Task 3 (operator direction, 2026-08-17).** Do not begin this task unless it is
> deliberately reselected; see [Backlog](../backlog.md#deferred-responsive-desktop-follow-up).

Render the useful shell immediately: boot/error state first, then the active workspace,
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
10. Verify both perceived behavior (visible boot/error state, responsive input) and lifecycle
    correctness (no orphaned process/container after Host close/crash or Supervisor signal) in the
    published native app.

## Done when

All five tasks are implemented and verified: the published macOS Desktop displays promptly during a
deliberately delayed boot, a Host close or crash never leaves a child/container orphan, Supervisor
SIGTERM cleans up every child, the UI has one cancellable current session subscription, and a long
stream has bounded queue/render behavior. `dotnet build src/ForgeMission.slnx` and
`dotnet test src/ForgeMission.slnx` pass with zero failures; the native verification names the exact
observations rather than inferring success from code review.
