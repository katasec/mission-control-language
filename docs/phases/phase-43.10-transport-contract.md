# Phase 43.10 — Transport contract (UI ↔ Client Runtime)

**Status: Design.** Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Depends on
[43.9](phase-43.9-client-runtime-authorization.md) (the dispatcher this transport carries requests
to). Third in the prerequisite chain: 43.8 → 43.9 → **43.10** → 43.11.

## Design

Per [forge-architecture.md](../design/forge-architecture.md#transport-is-infrastructure-not-architecture),
the Blazor WebAssembly UI does not depend on HTTP directly — it depends on a transport-independent
`IClientRuntimeChannel` contract. This spoke builds that contract and its first implementation.

**Why this needs its own spoke, not just "use HTTP":** under [43.11](phase-43.11-wasm-photino-shell.md),
the UI is sandboxed WASM and the Client Runtime is a separate, unsandboxed process — there is no
in-process call path between them (unlike the old Electron/Blazor Server shape, where UI and tool
execution shared one process). Every capability request and every response, including streaming
Mission Runtime output, has to cross this boundary. Getting the contract right here is what lets
the transport implementation change later (HTTP → gRPC) without touching UI code or Client Runtime
logic — exactly the "transport is infrastructure, not architecture" principle this spoke exists to
enforce structurally, not just state in prose.

## Locked decisions

- **First implementation is HTTP + SSE (and WebSockets where streaming needs it), not gRPC.**
  gRPC is a named future option in the architecture doc, not required for v1. Choosing HTTP first
  keeps this consistent with how the UI already needs to reach a Mission Runtime over `/v1/messages`
  — one transport shape to reason about, not two different ones for two different boundaries.
- **The UI talks only to `IClientRuntimeChannel`, never to a raw `HttpClient` pointed at the Client
  Runtime.** This isn't a style preference — it's what makes the transport actually swappable later.
  A UI component that reaches for `HttpClient` directly defeats the abstraction this spoke builds.
- **The channel carries both directions:** UI → Client Runtime (capability requests, forwarded to
  [43.9](phase-43.9-client-runtime-authorization.md)'s dispatcher) and Client Runtime → UI (mission
  progress, streaming text, pending-confirmation prompts per 43.9's `RequiresUserConfirmation`
  contract). This is not purely request/response — streaming and server-initiated notifications
  (a confirmation prompt the Client Runtime raises unprompted) both need a path.
- **The Client Runtime, not the UI, still owns the Mission Runtime orchestration loop** — unchanged
  from the existing, still-current decision in
  [forge-desktop-client-runtime.md](../design/forge-desktop-client-runtime.md#architecture-decision--orchestration-loop-lives-in-the-client-runtime-2026-07-27).
  This spoke's transport carries UI ↔ Client Runtime traffic; it does not change who initiates calls
  to the Mission Runtime.

## Interface shape

```csharp
namespace ForgeMission.ClientRuntime.Transport;

public interface IClientRuntimeChannel
{
    // Request/response — a capability dispatch, a session-setup call, etc.
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct);

    // Server-initiated stream — mission progress text, tool-call status updates, pending
    // confirmation prompts. The UI subscribes; the Client Runtime pushes.
    IAsyncEnumerable<ClientRuntimeEvent> Subscribe(CancellationToken ct);
}

public sealed class HttpClientRuntimeChannel : IClientRuntimeChannel
{
    // SendAsync -> HTTP POST to a local Client Runtime endpoint.
    // Subscribe -> Server-Sent Events (or a WebSocket where SSE's one-way shape doesn't fit,
    // e.g. a confirmation prompt that needs a UI response pushed back promptly).
}

// Future, not built now:
// public sealed class GrpcClientRuntimeChannel : IClientRuntimeChannel { }
```

`ClientRuntimeEvent` is a discriminated shape (mission text delta, tool-call status, confirmation
request, error) — exact schema is an implementation task, not a design decision blocking this
spoke's approval.

- **This spoke proves the transport against a plain out-of-process HTTP test client, not the real
  Blazor WASM UI.** Tasks 4–5 as originally scoped ("wire the Blazor WASM UI," "a WASM-hosted test
  harness") assume a WASM UI project exists — it doesn't yet; it's scaffolded in
  [43.11](phase-43.11-wasm-photino-shell.md) Task 2, which itself depends on this spoke being done
  first. Building a throwaway WASM project here just to prove the channel, then discarding it when
  43.11 scaffolds the real one, is wasted work the spoke doesn't need: the property actually being
  proven — "a capability request crosses a real network boundary (loopback), not an in-process
  shortcut" — doesn't require an actual browser WASM runtime, only a genuinely separate process
  making a real HTTP call. This spoke's end-to-end proof is a plain `HttpClient`-based test (or
  equivalent out-of-process client) hitting `HttpClientRuntimeChannel` over real loopback HTTP.
  [43.11](phase-43.11-wasm-photino-shell.md) Task 3 is where the *real* WASM UI consumes
  `IClientRuntimeChannel` for the first time and re-proves the same property end-to-end from an
  actual browser WASM context — that is not redundant with this spoke, it's the point where the
  literal "Blazor WASM UI" wiring this spoke's original task 4 named actually happens.

## Deferred — documented, not build-ready

- **`GrpcClientRuntimeChannel`** — named as the future option; no concrete need for it yet. Build
  when a real reason shows up (e.g., a thin native client that wants gRPC's stronger typing/
  performance characteristics over HTTP+SSE).
- **Multi-Client-Runtime routing** (a UI talking to more than one Client Runtime instance at once)
  — no scenario needs this; one UI, one Client Runtime, matches every other decision in this phase.

## Tasks

1. Define `IClientRuntimeChannel`, `ClientRuntimeEvent`, and the request/response DTO shapes for
   the capability-dispatch and session-setup calls [43.8](phase-43.8-capability-provider-pattern.md)/
   [43.9](phase-43.9-client-runtime-authorization.md) need to expose.
2. Implement `HttpClientRuntimeChannel`: HTTP POST for request/response, SSE (or WebSocket where
   needed) for the server-initiated stream.
3. Stand up the Client Runtime-side HTTP endpoints this channel calls — a small ASP.NET Core host
   distinct from the UI, exposing the capability dispatcher and the Mission Runtime's streaming
   output over this contract.
4. Establish the "no direct `HttpClient` against the Client Runtime" rule as an enforceable
   constraint (e.g. an architecture test / assembly-reference check) that 43.11's real WASM UI will
   be held to from its first commit — there is no UI project to apply this to yet (see the locked
   decision above), so this task produces the enforcement mechanism, not a UI to run it against.
5. End-to-end test: a capability request issued from a plain out-of-process HTTP test client (not
   an in-process shortcut), through `HttpClientRuntimeChannel`, through the dispatcher, to a real
   provider, and a real result returned — proving the transport itself works over a real loopback
   network boundary. [43.11](phase-43.11-wasm-photino-shell.md) Task 3/6 re-proves the same property
   from an actual WASM UI once that project exists.

## Done when

`HttpClientRuntimeChannel` correctly carries both request/response and streaming traffic, verified
by a capability request issued from a genuinely out-of-process HTTP client that reaches a real
provider and returns a real result over loopback — not an in-process call. The "no direct
`HttpClient` against the Client Runtime" constraint exists as an enforceable check ready for
[43.11](phase-43.11-wasm-photino-shell.md)'s WASM UI to be held to, even though no UI project
consumes it yet.
