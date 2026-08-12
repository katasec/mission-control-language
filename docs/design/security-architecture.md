# Security architecture — tiering, data ownership, and design gate

> **Status: governing design gate, 2026-08-13.** Applies to every new or changed Forge
> architecture, infrastructure, service, datastore, identity, queue, and external endpoint. It
> operationalises the [Phase 42 north-star topology](../phases/phase-42-forge-cloud.md#3a-deployment-topology--the-north-star-locked-2026-07-18).

## Non-negotiable invariants (Type 1)

1. **Adjacent-only tiers.** The target is `CDN → tier 1 → tier 2 → tier 3`. Cross-tier traffic
   crosses one boundary at a time; tier 1 never directly reaches tier 3.
2. **Tier 1 is an edge, not a state owner.** Internet-facing services authenticate, authorize,
   validate, and route. They do not hold data-plane credentials, database connections, or direct
   datastore RBAC.
3. **Tier 2 owns application behaviour.** Internal services own business operations and expose
   explicit synchronous service contracts or durable-message contracts to other contexts.
4. **One datastore per bounded context.** A context's owning tier-2 service alone queries or
   mutates its store. Contexts exchange stable IDs, commands, events, or service responses — never
   cross-store queries, foreign keys, or shared write credentials.
5. **Tier 3 is not public.** Datastores and durable transport have no public ingress. Access is
   granted to the minimum tier-2 identity needed for the owning operation.
6. **Least privilege follows the call path.** Provider keys, billing credentials, and datastore
   roles are separately scoped to the service that requires them.

The logical model is independent of today's deployment substrate. Cilium/NSG policy, ACA ingress,
private endpoints, identity RBAC, and service authentication are enforcement mechanisms; the
invariant is the rule they must enforce.

## Tier model

| Tier | Purpose | May call | Must not hold |
|---|---|---|---|
| 1 — presentation / edge | CDN/WAF, browser/Desktop/API entry, authentication and routing. | Tier 2 services only. | Tier 3 credentials/RBAC or direct datastore paths. |
| 2 — application | Bounded-context services, mission workers, domain operations. | Its Tier 3 store; explicit internal Tier 2 contracts. | Another context's store credentials or public data endpoints. |
| 3 — data / durable transport | Per-context database, Table/Blob, cache, broker. | Nothing as an initiator. | Public ingress or shared, unconstrained credentials. |

Tier-2-to-tier-2 traffic is allowed only through a named service or durable-message contract. It
does not authorise a service to bypass another context and access its data store directly.

## Type 1 versus Type 2 decisions

| Decision | Classification | Handling |
|---|---|---|
| Tier boundary, bounded-context/data ownership, public entry point, and cross-context contract | Type 1 — one-way / expensive | Lock in a design doc before implementation. Reject designs that collapse a boundary without an explicitly approved transitional exception. |
| A service's direct data-plane role or secret scope | Type 1 unless demonstrably temporary and easily removed | State owner and least privilege before granting. Record removal path for any temporary access. |
| Internal transport mechanism (HTTP, gRPC, queue), app placement, image tag, scale value, port, or retry policy | Type 2 — reversible | Keep behind a named contract; document the migration path if it affects a Type 1 boundary. |
| Local proof-only credential, emulator, or temporary role | Type 2 only if bounded to non-production and removed by checked-in IaC | Name its expiry/removal condition; never allow it to become an undocumented production dependency. |

When uncertain, classify a choice as Type 1 until the design demonstrates a low-cost reversal.

## Mandatory architecture-security review

Before approving an implementation plan, the designer records concise answers in the relevant
phase/design spoke. “Not applicable” is an answer only when its reasoning is stated.

| Question | Required answer |
|---|---|
| What bounded context changes, and who owns its data? | Named owner service and exactly one datastore/context. |
| Where is the public entry point? | Tier-1 component, auth boundary, and its Tier-2 route. |
| Which components are Tier 2? | Named service contracts and internal-only communication paths. |
| What are the Tier-3 stores and transports? | No public ingress; named minimum identities/roles. |
| Can a service access another context's store? | “No”, or an explicitly approved transitional exception with removal path. |
| What secrets/credentials does each component receive? | Least-privilege matrix; edge holds no data-plane credentials. |
| Is the decision Type 1 or Type 2? | Rationale and, for Type 2, the reversal path. |
| How is this enforced and proven? | IaC policy/ingress/RBAC plus a named verification observation. |

An implementation task is not build-ready while any answer is missing. A Type-1 choice cannot be
left as “decide during implementation.”

## Transitional exceptions

An exception is permitted only when all of the following are recorded in the active design spoke:

1. the invariant being temporarily relaxed and why the immediate product proof needs it;
2. the exact scope (environment, identity, endpoint, and data affected);
3. why it remains Type 2 in practice — including the IaC/code change that removes it;
4. the condition or phase that removes it; and
5. the verification that it has not silently expanded.

No exception may be described as the target architecture. “Demo cut” or “temporary” alone is not
an exception record.

## Current conversation application implication

The durable conversation store is a distinct Tier-3 bounded context. Its state-owning Conversation
service belongs in Tier 2 and is internal-only; a Tier-1 edge routes requests to it without
holding conversation Table/Blob permissions. A separate mission Worker remains Tier 2 and reports
progress through the Conversation service's internal contract or durable message, rather than
mutating the conversation store directly. The exact Worker-to-service transport is Type 2, but the
ownership boundary is Type 1.
