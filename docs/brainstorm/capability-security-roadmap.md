# Brainstorm: capability-secured brain-to-hands execution

> **Status: future architecture direction — not decided, not scoped as a phase, and not an
> implementation handoff.** Captured 2026-09-24. This develops the existing durable
> conversation and Mission Hands foundation; it does not change the active Phase 45/46 work.
> If adopted, turn the selected slice into a phase hub and dependency-ordered spokes before
> implementation.

## Why this exists

Forge already deliberately separates remote reasoning (**brain**) from local capability execution
(**hands**, currently Bob): the Worker never receives a Project root, local tool provider,
credential, or direct route to the machine. The Conversation Host is already the durable
interceptor between them.

The next design direction is to make that separation a platform capability system. A brain may
propose a typed action, but it never owns authority to perform it. The platform alone issues a
short-lived, bounded grant; Hands alone may exercise it; independent local/egress enforcement
rejects any action outside that grant.

This is analogous to operating-system user mode and kernel-mediated handles. It borrows Docker's
default-deny, add-only-the-required-privilege philosophy, while recognising that Docker/Linux
capabilities alone do not control ordinary outbound HTTPS or application-level authority.

## Existing foundation to preserve

| Existing owner | Current foundation | Must remain true |
|---|---|---|
| `ForgeMission.ConversationWorker` | Executes an immutable package and publishes semantic progress. | It remains untrusted for local authority, conversation sequencing, and durable state. |
| `ForgeMission.ConversationHost` / `ConversationGrain` | Validates and correlates a Hands request, owns canonical events/sequences, and exposes claimable work through the durable conversation. | It remains the sole authority transition owner; no Worker-to-Hands bypass appears. |
| `ForgeMission.Application` | Binds a locally approved launch to a fresh ephemeral attachment. | It never lets a UI or caller choose arbitrary local authority. |
| `ForgeMission.ClientRuntime` | Enforces capability policy, confirmation, audit, cancellation, sandboxing, and cleanup. | It remains the only local executor; a conversation or UI never executes a capability itself. |
| `ForgeMission.Conversations.Contracts` | Carries typed, versioned messages without Project roots, credentials, or Bob handles. | Durable messages stay value-only and do not become bearer authority. |

The current `NoHands`, `ProjectWorkspace`, and `ProjectWorkspaceAndTerminal` profiles are useful
coarse ceilings. They must not become the final authorization decision for a particular action.

## Target execution model

```text
Brain / Worker (user mode)
  proposes a typed action
        |
        v
Conversation Host / policy actor (authority kernel)
  validates → reviews → issues or refuses a bounded grant
        |
        v
Independent enforcement plane
  local sandbox + capability broker + egress gateway
        |
        v
Hands / Bob (constrained executor)
  exercises only the issued grant and records the result
```

The **placement** of this path is the hard guarantee: no message shape, retry, model output, or
compromised Worker may cause Hands to run an operation without an accepted grant. A classifier or
review model improves the decision; it is never the containment boundary.

## Platform capabilities and grants

Use a small platform vocabulary rather than provider- or product-specific privilege families:

```text
FilesystemRead       FilesystemWrite
ProcessExecute       NetworkRead       NetworkWrite
IdentityUse          UserInteraction
```

A role, profile, or mission declaration decides which capabilities *may* be issued. It gives no
ambient authority. A concrete capability grant names the subject, resource, operation, limits,
lifetime, provenance, revocation handle, and audit identity.

```text
FilesystemWrite
  subject: one Hands attachment
  resource: one manifest-identified Project document
  operation: exact edit
  precondition: expected old-content hash
  limits: one use, bounded content size
  expires: 60 seconds
```

Authority narrows monotonically:

```text
Platform policy ceiling
  ∩ mission-declared ceiling
  ∩ Project/session entitlement
  ∩ operator approval when required
  ∩ per-action policy/review verdict
  = executable capability grant
```

No layer may widen a previous one. The brain receives neither a workspace root nor a reusable
capability token; it receives at most the typed tool vocabulary and the resulting durable facts.

## Durable action lifecycle

The Conversation Host actor owns the durable lifecycle for one proposed action. The labels below
are a design direction, not yet a contract:

```text
Proposed
  → StructurallyValid
  → PolicyAllowed
  → ReviewAllowed | ReviewDenied | AwaitingOperator
  → CapabilityIssued
  → Claimed
  → Executing
  → ResultRecorded | Revoked | Expired | Interrupted
```

The actor must persist the input, decision provenance, grant identity, and terminal outcome needed
for replay and audit. It must make the only transition that exposes a request to the matching Hands
attachment. Bob revalidates the grant immediately before execution; expiry, revocation, and an
attachment mismatch fail closed.

## Deterministic policy before semantic review

The first gate is deterministic and non-negotiable: pinned profile, typed operation shape,
resource scope, expected-content precondition, sandbox/platform support, lease validity, and
attachment correlation. A proposal that fails any of these gates never reaches a semantic model.

A future semantic reviewer may classify the normalized action as `allow`, `deny`, or
`require_operator`, with explicit risk categories such as scope divergence, destructive change,
credential discovery, persistence, privilege escalation, or exfiltration. Jev is a candidate for
this narrow structured-decision role, not a selected dependency or a security proof. Low
confidence, malformed output, unavailable reviewer, or an unrecognised category must fail closed
or request an explicit operator decision.

The reviewer assesses the action's observable risk, not an unknowable inner motive. Structural
containment remains the defence against an incorrect classification.

## Network authority: default deny, generic read primitive

`NetworkRead` is the generic network fetch capability. Package installation, Docker pulls, NuGet,
GitHub, model registries, and ordinary web reads use the same primitive; Forge does not grow
separate `NuGetCapability`, `DockerCapability`, or `GitHubCapability` families.

```text
NetworkRead
  subject: one Hands attachment
  allowed origins: exact host + port
  operations: GET, HEAD
  request body and credentials: absent by default
  redirect: each hop must satisfy the grant again
  limits: request count, byte count, duration, concurrency
  expires: short-lived
```

`NetworkWrite` is separate. Uploads, `POST`, authenticated mutations, WebSockets, tunnels, or any
public egress outside a specific read grant require an explicit, independently reviewed grant.
Public Internet access is absent from the platform default.

An egress gateway, not the Hands process, enforces a grant. It owns resolution, TLS validation,
destination policy, and audit. Hands has no direct socket or DNS route, and cannot use IP literals,
loopback, private ranges, link-local metadata addresses, proxy environment overrides, or a
redirect to escape the original grant. Identity use is separately mediated; a network grant does
not hand a credential to the brain or the process.

The package-proxy escape in the 2026 OpenAI/Hugging Face incident illustrates why this boundary
must be generic and external: a service allowed to obtain packages became a general outbound relay
after compromise. The lesson is not merely "block the Internet"; it is that no reachable
dependency may act as an unbounded network capability. See [OpenAI's incident report](https://openai.com/index/hugging-face-incident-and-the-road-ahead/).

## Future roadmap — decide before building

| Order | Future decision/deliverable | Completion condition before the next item |
|---|---|---|
| 1 | Lock the Type-1 authority boundary: grant issuer, durable owner, resource taxonomy, revocation authority, and external enforcement owner. | Security Architecture review names tiers, identities, stores, public entry points, and enforcement observation. |
| 2 | Define additive typed contracts for `ProposedAction`, decision, grant, claim, revocation, and result. | AOT-safe source-generated wire shapes, idempotency, replay, and expiry semantics are specified; no credential/root/bearer token crosses the wire. |
| 3 | Add deterministic capability issuance to the existing Host → Application → Client Runtime path. | A request outside profile/resource/lease scope is never claimable or executable; focused negative-path proof covers expiry and revocation. |
| 4 | Build the external local-enforcement seam and the `NetworkRead` egress gateway. | Direct socket/DNS and metadata/private-network bypasses are structurally impossible for Hands; an exact allowed read succeeds and each forbidden path is observed to fail. |
| 5 | Evaluate a semantic reviewer such as Jev against a fixed adversarial corpus and explicit fallback policy. | Measured false allow/deny rates, calibration, latency/cost, data handling, vendor/license, outage semantics, and a removal path are accepted. |
| 6 | Add operator-facing review and trace evidence only after the action and grant model is stable. | The surface shows proposal, scope, verdict, operator decision when needed, grant lifecycle, and result without becoming a second authority store. |
| 7 | Complete default-path acceptance on the published Desktop with ordinary configuration and a disposable Project. | The zero-argument artifact exercises one allowed action and one denied action against the normal local/runtime dependencies, with durable observations. |

Each implemented slice must follow the Codex supervisor workflow and have its own active spoke;
this table deliberately does not authorize implementation.

## Questions still open

- Which resources are stable Forge identities versus platform-native handles, and how can a grant
  name one without exposing a raw host path?
- Is a local enforcement broker a process, an OS sandbox adapter, or both for each supported
  platform? What is the fail-closed macOS/Windows/Linux support matrix?
- What exact network policy language is small enough to be understandable while still preventing
  DNS rebinding, redirect, SSRF, and metadata-service bypasses?
- Which operator decisions are mandatory versus policy/configuration decisions made before launch?
- What is the shortest useful grant lifetime and how should an in-flight action behave at expiry?
- Can a semantic reviewer see enough normalized context to evaluate scope without receiving source
  secrets, private file contents, or a reusable authority token?
- Does a hosted reviewer introduce a new network/identity boundary that defeats the local default,
  and is a local model required for some modes?

## Related foundations

- [Security Architecture](../design/security-architecture.md)
- [Engineering Philosophy](../design/engineering-philosophy.md)
- [Default-Path Acceptance](../design/default-path-acceptance.md)
- [Phase 45 — Forge Desktop mission conversations](../phases/phase-45-mission-conversations.md)
- [Conversation Host component boundary](../../src/ForgeMission.ConversationHost/README.md)
- [Client Runtime component boundary](../../src/ForgeMission.ClientRuntime/README.md)
