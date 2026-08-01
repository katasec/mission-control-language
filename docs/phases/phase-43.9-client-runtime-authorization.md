# Phase 43.9 — Client Runtime security & authorization layer

**Status: Done (2026-08-01).** Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md).
Depends on [43.8](phase-43.8-capability-provider-pattern.md) (something has to exist to authorize
dispatch to). Second in the prerequisite chain: 43.8 → **43.9** → 43.10 → 43.11. **Next: 43.10.**

Implemented on branch `codex/phase-43.9-client-runtime-authorization` (commit `7a1aa61`).
`CapabilityDispatcher` (in `ForgeMission.Core.Tools`, following 43.8's placement precedent) owns
all provider resolution and invocation — tool executors receive only `ICapabilityDispatcher` and
pass plain, data-only `ICapabilityRequest` records (no closures, no provider references reach
executor code). `CapabilityAuthorizationPolicy.Default` auto-approves `file`/`terminal` (no
regression from today's unrestricted behavior); every dispatch produces an audit record via
`InMemoryCapabilityAuditLog`; `AgenticSession`'s separate `approveToolCall` hook was retired in
favor of the single dispatcher path (its observational `notifyToolCall` hook was preserved
unchanged). Verified independently: `dotnet build src/ForgeMission.slnx` clean except the same
pre-existing, unrelated `Rooms.Tests` warning; `dotnet test src/ForgeMission.slnx` — 441 passed /
11 environment-gated skipped / 0 failed, reproduced exactly; `grep TryGetProvider` across `src`
confirms it is called only inside `CapabilityDispatcher` (and the pre-existing `CapabilityRegistry`)
— no executor or session class resolves a provider directly; `CapabilityDispatcherTests` proves
denial blocks the provider and is audited, confirmation is evaluated per-request not cached, and an
executor given a denying dispatcher never reaches the provider.

## Design

Per [forge-architecture.md](../design/forge-architecture.md#security-responsibility), the Client
Runtime is the desktop's security enforcement point. Capability providers ([43.8](phase-43.8-capability-provider-pattern.md))
stay focused on execution; they must never contain their own authorization logic. This spoke builds
the single enforcement point that sits between "the Mission Runtime requested a capability" and "a
provider actually runs it."

**This is a genuinely new layer, not a migration** — unlike 43.8, nothing in the current codebase
does this today. `Bash` is currently unrestricted with no confirmation gate at all
([43.1](phase-43.1-tool-execution-engine.md)'s locked decision, reasonable for a single local
user with no multi-tenant exposure). This spoke doesn't reverse that default; it builds the
mechanism that makes "unrestricted by default policy" an explicit, inspectable decision rather than
"no mechanism exists to restrict it even if wanted."

**Relationship to mission-level human-in-the-loop — deliberately two different things, not one
renamed:**

```
Mission Runtime:   "What should happen next?"        — kind: human / Suspended (43.5), MCL-level
Client Runtime:    "Is this capability authorized?"   — this spoke, capability-level
Operating System:  "Can this physically be done?"     — OS permissions, out of scope here
```

A mission can have zero human-in-the-loop steps and still have every tool call pass through this
spoke's authorization layer. A capability-level policy (e.g., "confirm before any `Bash` call that
looks like a destructive command") operates independently of whatever the mission itself gates.
Don't build these as one mechanism wearing two names — they answer different questions, at
different layers, and can evolve independently.

## Locked decisions

- **Providers never self-authorize.** A capability provider ([43.8](phase-43.8-capability-provider-pattern.md))
  receives an already-authorized request and executes it. If a provider needs to reject something,
  that's an execution-time failure (the operation genuinely can't be done), not an authorization
  decision — those are different failure modes and should return different result shapes.
- **Policy outcomes are one of four, not a binary allow/deny:** automatically approved,
  automatically denied, administrator-controlled, or requires explicit user confirmation. The
  authorization layer's job is to resolve a request to one of these four given the current policy,
  not to hardcode any particular default — defaults are a policy configuration, not baked into the
  enforcement code.
- **Authorization is capability-scoped, not global.** A policy decision is made per
  `(capability, request)` pair, not once per session. Confirming one `Bash` call does not
  pre-authorize the next one, unless the policy explicitly says so (e.g., "confirm once per
  session for this capability" is a valid policy shape to support, but it must be an explicit
  policy choice, not an implicit side effect of the first confirmation).
- **Auditing is part of this layer, not bolted on separately.** Every dispatched capability request
  — approved or denied, automatic or user-confirmed — gets an audit record. This is what makes "the
  Client Runtime enforces policy" a checkable claim later, not just an assertion.

## Interface shape

```csharp
namespace ForgeMission.ClientRuntime.Security;

public enum AuthorizationOutcome
{
    AutoApproved,
    AutoDenied,
    RequiresUserConfirmation,
    // AdministratorControlled is a policy SOURCE (where the decision comes from), not a fourth
    // outcome value here — it resolves to one of the three above once evaluated.
}

public interface ICapabilityAuthorizer
{
    Task<AuthorizationOutcome> AuthorizeAsync(
        string capabilityName,
        object request,          // the specific capability request, e.g. a Bash command or a file path
        CancellationToken ct);
}

// The dispatch point every capability request passes through before reaching a provider.
public interface ICapabilityDispatcher
{
    Task<ToolExecutionResult> DispatchAsync(
        string capabilityName,
        object request,
        CancellationToken ct);
    // Internally: AuthorizeAsync -> (if RequiresUserConfirmation) prompt -> audit -> invoke provider -> audit result
}
```

The exact shape of "prompt for user confirmation" (a UI dialog, a notification, a CLI prompt) is a
Presentation-layer concern reached through the Client Runtime API — this spoke defines the contract
the dispatcher calls into, not the UI itself.

## Deferred — documented, not build-ready

- **Administrator-managed policy distribution** (a central policy an admin pushes to multiple
  Client Runtime instances) — no multi-machine/enterprise deployment exists yet to need this;
  policy is per-machine local config for v1.
- **Per-capability rate limiting / abuse prevention** — relevant once a hosted, multi-tenant
  scenario exists (echoes [Phase 39](phase-39-metered-runtime-marketplace.md)'s domain); not needed
  for a single local user.

## Tasks

1. Define `AuthorizationOutcome`, `ICapabilityAuthorizer`, and `ICapabilityDispatcher` per the shape
   above.
2. Implement a default policy: `ITerminalProvider`/`IFileProvider` requests auto-approved (matching
   today's existing unrestricted-`Bash` behavior — this spoke must not silently tighten what's
   already a locked, deliberate decision), with the policy itself expressed as inspectable
   configuration, not hardcoded in the dispatcher.
3. Wire audit logging: every dispatched request (capability, request summary, outcome, timestamp)
   recorded, queryable for later inspection.
4. Route the existing tool executors through `ICapabilityDispatcher` instead of calling
   `IFileProvider`/`ITerminalProvider` directly — this is the point where 43.8's providers actually
   become gated rather than directly callable.
5. Define (not necessarily fully implement) the `RequiresUserConfirmation` contract's shape — what
   the dispatcher needs from the Presentation layer to resolve a pending confirmation — since
   [43.11](phase-43.11-wasm-photino-shell.md)'s UI will need to implement the other end of it.

## Done when

Every capability request from a Mission Runtime passes through `ICapabilityDispatcher`, no
capability provider is reachable without going through authorization first (test-verified: a
provider called directly, bypassing the dispatcher, is not how the executors reach it), the default
policy preserves today's existing unrestricted-`Bash`/confined-file-access behavior exactly (no
regression), and every dispatched request produces an audit record.
