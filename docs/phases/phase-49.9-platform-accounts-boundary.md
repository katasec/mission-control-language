# Phase 49.9 — Platform Accounts boundary

> **Status:** Type-1 design locked. Implementation waits for its separate Platform/Rooms and
> `forge-infra` cards; this document authorizes no credential, deployment, or source change.

## Decision

`forge-platform` owns an internal-only Tier-2 Platform Accounts service and
`Katasec.Forge.Platform.Contracts` `1.0.0`. It alone accesses `authbilling_db` and owns starting
credit, balance, platform-key mint/resolve, and agent-run settlement. `forge-rooms` owns members,
membership, and `forge_rooms`; it uses only the exact private contract and receives neither Billing
source/package, `AuthBillingConnection`, nor the platform-key HMAC.

Existing public URLs remain behavior-compatible during migration: Rooms validates its user/member
then delegates `POST /platform/keys`; Rooms delegates `GET /me` key resolution then reads display
data only from its own store. The CLI default endpoint remains unchanged.

## Private contract and identity

The internal v1 contract has named operations for starting credit, credit lookup, key issue, key
resolve, and agent-run settlement. DTOs are source-generated JSON only: no Npgsql, Billing/Room
entities, or secret. Presented keys are never logged/traced/returned in errors. Settlement uses the
deterministic client token `rooms:<triggerMessageId:N>:<agentMemberId:N>` for idempotent replay.

Platform Accounts has internal ACA ingress. Rooms calls it with a dedicated managed-identity token
for audience `api://forge-platform-internal` and role `Forge.Platform.Rooms.Accounts`; Platform
validates issuer, audience, role, and caller application identity. Platform and Rooms require
distinct identities: the current shared identity cannot enforce least privilege. Platform alone has
Key Vault access to Billing connection/HMAC; Rooms and Runner lose both after the cutover.

## Transition, evidence, and rollback

Create the service/contract against the existing ledger/key data without schema migration; create
separate identities, role, ingress, and validation; then replace the four Rooms Billing call sites
in one release with no dual write or runtime flag. A previous Rooms image safely rolls back against
the unchanged tables.

Default acceptance proves login/key issue, whoami, member provisioning/one credit grant, one funded
agent run/one settlement, and unchanged public unauthorised behavior. Negative evidence proves
invalid workload tokens cannot mutate ledger/key data; replay does not double-debit; outage has no
in-process Billing fallback; and deployed Key Vault/ACA observations prove Rooms/Runner lack the
Billing/HMAC path. Before handoff, `forge-infra` must prove actual tenant/app-role values, internal
DNS/token acquisition, and telemetry redaction; none are inferred here.
