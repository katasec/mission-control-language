# Phase 43.8 — Capability contracts & Provider pattern

**Status: Design.** Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Prerequisite to
the desktop UI resuming — see [forge-architecture.md](../design/forge-architecture.md) for the
architecture this spoke builds toward. First in the prerequisite chain: 43.8 → 43.9 → 43.10 → 43.11.

## Design

This is a **migration and generalization, not a from-scratch build.** [43.7](phase-43.7-workspace-provider.md)
already unifies file I/O and process execution behind one interface (`IWorkspace`), backed by real
prior-art research (OpenHands, Claude Agent SDK, Codex CLI all converge on this shape). What's new
here is:

1. Reshaping that existing capability into the `IFileProvider`/`ITerminalProvider` naming and shape
   [forge-architecture.md](../design/forge-architecture.md) establishes, so future capabilities
   (Git, Docker, Browser, ...) follow the same pattern rather than each being designed from scratch.
2. A **Capability Registry** the Mission Runtime can query — replacing today's static, hand-written
   `AgentToolDeclarations` ([AgentToolDeclarations.cs](../../src/ForgeMission.Core/Tools/AgentToolDeclarations.cs))
   with something a client advertises dynamically.

**What this spoke does NOT do:** build `IGitProvider`/`IDockerProvider`/`IBrowserProvider`/
`IClipboardProvider`/`INotificationProvider`/`ISecretsProvider`/`ISshProvider`. Those are real,
named capability kinds in the architecture doc, but none has a real caller yet — building them now
would repeat the exact speculative-abstraction trap [43.7](phase-43.7-workspace-provider.md)'s own
locked decisions explicitly avoided for `ContainerWorkspace` ("an abstraction is not speculative
when it has a real caller today"). This spoke migrates the two capabilities that already have real
callers (`Read`/`Edit`/`Write` → `IFileProvider`, `Bash` → `ITerminalProvider`) and builds the
registry shape so the *next* capability is a small addition, not a redesign. See "Deferred" below.

## Locked decisions

- **`IFileProvider`/`ITerminalProvider` wrap `IWorkspace`, they do not replace its internals.**
  `LocalDiskWorkspace`'s confinement logic (`WorkspaceGuard`, symlink resolution, the
  `root`/`root-evil` string-prefix trap fix), `Edit`'s exact-match-replace contract, and `Bash`'s
  unrestricted-execution/full-environment-inheritance/5-minute-timeout behavior are all unchanged —
  only the interface shape and naming above them changes. Nothing about path confinement,
  environment safety, or the "provider key is never a process env var" invariant
  ([43.1](phase-43.1-tool-execution-engine.md)'s locked decision) is renegotiated by this spoke.
- **The Capability Registry is a client-side manifest, not a discovery protocol (v1).** It's a list
  the Client Runtime constructs from which providers are actually registered/available on this
  machine (e.g., `IFileProvider` and `ITerminalProvider` today), exposed to the Mission Runtime as
  part of session setup. A dynamic per-request capability negotiation protocol is not needed yet —
  build it when a client legitimately varies its available capabilities at runtime (e.g., a
  sandboxed environment with `ITerminalProvider` unavailable), not speculatively now.
- **Tool declarations become capability-derived, not hand-maintained in parallel.** Today
  `AgentToolDeclarations.All` is a fixed, hand-written list the Mission Runtime always sees
  regardless of client. Under the registry, the declarations a given session's Mission Runtime
  receives should be derived from what the connected Client Runtime's registry actually advertises.
  This spoke defines the registry shape; wiring the Mission Runtime side to consume it (rather than
  a fixed constant) is scoped here too, since a registry nobody reads isn't done.
- **Authorization is explicitly out of scope for this spoke.** The registry advertises what's
  *possible*; whether a specific request is *authorized* is [43.9](phase-43.9-client-runtime-authorization.md)'s
  job, layered on top of what this spoke builds. Don't conflate "capability exists" with
  "capability is currently allowed."

## Interface shape

```csharp
namespace ForgeMission.ClientRuntime.Capabilities;

// Marker convention every capability provider follows — deliberately minimal.
// A provider doesn't need to implement this directly; it's the shape the
// Capability Registry keys on.
public interface ICapabilityProvider
{
    string CapabilityName { get; }   // e.g. "file", "terminal" — stable, used in the registry manifest
}

public interface IFileProvider : ICapabilityProvider
{
    // Same operations IWorkspace already exposes for file I/O — Exists/Read/Write against an
    // already-resolved, confinement-checked path. This interface wraps IWorkspace; it does not
    // reimplement its confinement logic.
}

public interface ITerminalProvider : ICapabilityProvider
{
    // Same shape as IWorkspace.ExecuteAsync today — unrestricted Bash, full environment
    // inheritance, 5-minute default timeout, unchanged.
}

public sealed class CapabilityRegistry
{
    // Constructed from whichever ICapabilityProvider instances are actually registered on this
    // Client Runtime. Exposes the manifest the Mission Runtime consumes at session setup.
    public IReadOnlyList<string> AvailableCapabilities { get; }
}
```

Exact method signatures on `IFileProvider`/`ITerminalProvider` should mirror `IWorkspace`'s existing
methods as closely as possible — this is a reshaping exercise, not a redesign of already-verified
behavior.

## Deferred — documented, not build-ready, no task numbers below

Same convention [43.7](phase-43.7-workspace-provider.md) used for `ContainerWorkspace` — captured so
a future spoke doesn't have to re-derive this, but explicitly not part of this spoke's task list:

- **`IGitProvider`** — git status/diff/commit/branch as a requestable capability. Real candidate use
  case once a mission wants to reason about repo state directly rather than shelling out via
  `ITerminalProvider`, but no mission does that today.
- **`IDockerProvider`** — container lifecycle as a capability the Mission Runtime can request,
  distinct from the Client Runtime's own use of Docker to *host* a local Mission Runtime (that's
  infrastructure, not a capability the brain reasons about). No caller yet.
- **`IBrowserProvider`** — browser automation/screenshot as a capability. Notably, this project's
  own Claude-facing tooling already does something like this for verification purposes; a Mission
  Runtime-requestable version is a different, unbuilt thing.
- **`IClipboardProvider`, `INotificationProvider`, `ISecretsProvider`, `ISshProvider`** — no real
  caller yet for any of them. Build each when a concrete mission need shows up, following this
  spoke's established provider shape.
- **Dynamic per-session capability negotiation** (a client whose available capabilities can change
  mid-session) — the static client-side manifest is sufficient until a real client needs otherwise.

## Tasks

1. Define `ICapabilityProvider`, `IFileProvider`, `ITerminalProvider`, and `CapabilityRegistry` per
   the interface shape above, in a new `ForgeMission.ClientRuntime.Capabilities` (or equivalent)
   namespace.
2. Implement `IFileProvider`/`ITerminalProvider` as thin wrappers over the existing
   `IWorkspace`/`LocalDiskWorkspace` — no behavior change, confirmed by reusing 43.1/43.7's existing
   test suite against the new interface surface (adapt call sites, not test intent).
3. Wire the four existing tool executors (`ReadToolExecutor`/`EditToolExecutor`/`WriteToolExecutor`/
   `BashToolExecutor`) to dispatch through `IFileProvider`/`ITerminalProvider` instead of
   `IWorkspace` directly, preserving `ToolExecutorRegistry`'s existing name-keyed dispatch shape.
4. Build `CapabilityRegistry` construction from whichever providers are actually registered, and
   expose its manifest to session setup (the exact transport for this is
   [43.10](phase-43.10-transport-contract.md)'s job — this task only needs the manifest to exist and
   be queryable in-process).
5. Replace `AgentToolDeclarations.All`'s fixed-constant consumption with registry-derived
   declarations — the Mission Runtime side should read what's actually available, not assume a
   fixed four.
6. Update tests: reuse 43.1/43.7's existing real-filesystem/real-subprocess, no-mocks test
   discipline against the new interface surface; add a registry-construction test proving the
   manifest matches whichever providers were actually registered.

## Done when

`IFileProvider`/`ITerminalProvider` are the only interfaces the tool executors talk to (no direct
`IWorkspace` references left in executor code), the Capability Registry correctly reports what's
available, the Mission Runtime consumes registry-derived tool declarations instead of the fixed
constant, and the full existing 43.1/43.7 test suite passes against the new surface with zero
behavior change (same confinement, same environment-inheritance, same timeout behavior).
