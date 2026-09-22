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

## Private v1 contract

`Katasec.Forge.Platform.Contracts` is a private `1.0.0` package. Rooms consumes exactly `[1.0.0]`.
It owns only the following wire records and the generated JSON metadata; it contains no Npgsql,
Billing/Rooms entity, connection string, HMAC, or other secret:

```csharp
public sealed record EnsureStartingCreditRequest(Guid MemberId);
public sealed record EnsureStartingCreditResponse(long BalanceMicroUsd, bool Granted);
public sealed record GetBalanceRequest(Guid MemberId);
public sealed record GetBalanceResponse(long BalanceMicroUsd);
public sealed record IssuePlatformKeyRequest(Guid MemberId);
public sealed record IssuePlatformKeyResponse(string Key, long BalanceMicroUsd);
public sealed record ResolvePlatformKeyRequest(string PresentedKey);
public sealed record ResolvePlatformKeyResponse(Guid MemberId, long BalanceMicroUsd);
public sealed record SettlementUsageV1(
    long InputTokens, long OutputTokens, double ComputeSeconds, string? Model);
public sealed record SettleRunRequest(
    Guid MemberId, string MissionRef, SettlementUsageV1 Usage, string ClientToken);
public sealed record SettleRunResponse(
    long DebitedMicroUsd, long BalanceMicroUsd, bool Replayed);
public sealed record PlatformAccountsError(string Code);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(EnsureStartingCreditRequest))]
[JsonSerializable(typeof(EnsureStartingCreditResponse))]
[JsonSerializable(typeof(GetBalanceRequest))]
[JsonSerializable(typeof(GetBalanceResponse))]
[JsonSerializable(typeof(IssuePlatformKeyRequest))]
[JsonSerializable(typeof(IssuePlatformKeyResponse))]
[JsonSerializable(typeof(ResolvePlatformKeyRequest))]
[JsonSerializable(typeof(ResolvePlatformKeyResponse))]
[JsonSerializable(typeof(SettlementUsageV1))]
[JsonSerializable(typeof(SettleRunRequest))]
[JsonSerializable(typeof(SettleRunResponse))]
[JsonSerializable(typeof(PlatformAccountsError))]
public partial class PlatformAccountsJsonContext : JsonSerializerContext { }
```

`SettlementUsageV1` deliberately copies the scalar Runner cost signals. Platform therefore owns
pricing/`CostMeter` without taking a Runner Contracts dependency. The Platform HTTP adapter and the
Rooms `PlatformAccountsClient` use only `PlatformAccountsJsonContext` type information: no
`new JsonSerializerOptions`, default `HttpClientJsonExtensions`, reflection, or entity
serialization is allowed.

The five internal ACA routes are `POST /internal/platform-accounts/v1/starting-credit`, `/balance`,
`/keys`, `/keys/resolve`, and `/settlements`. They require a dedicated Rooms managed-identity token
for audience `api://forge-platform-internal`; Platform validates issuer, audience, caller
application identity, and role `Forge.Platform.Rooms.Accounts`. Platform and Rooms must use distinct
identities: the current shared identity cannot enforce least privilege.

`MemberId` must be non-empty; `MissionRef` and `ClientToken` nonblank; token counts non-negative;
and `ComputeSeconds` finite and non-negative. Rooms uses the deterministic settlement token
`rooms:<triggerMessageId:N>:<agentMemberId:N>`. Platform stores and compares the request fingerprint
(member, mission, and all usage fields): the same token and fingerprint returns the original debit
and balance with `Replayed=true`; the same token with a different fingerprint is
`409 idempotency_conflict`; no replay can double-debit.

| HTTP | Error code | Meaning |
|---|---|---|
| 400 | `invalid_request` | Invalid contract field or value. |
| 401 | `invalid_workload_token` | Missing or invalid internal workload token. |
| 403 | `caller_not_authorized` | Valid workload identity lacks the required role or caller identity. |
| 404 | `invalid_platform_key` | Resolve only: malformed, unknown, wrong-secret, and revoked keys remain indistinguishable. |
| 409 | `idempotency_conflict` | A client token was reused with a different settlement fingerprint. |
| 503 | `service_unavailable` | Platform account or dependent-store availability failure. |
| 500 | `internal_error` | An unexpected server failure. |

Every error body is `PlatformAccountsError`; it exposes no exception, key, connection, or ledger
detail. Presented keys are never logged, traced, or returned in an error. Platform may retain the
existing at-most-30-second key/balance resolution cache, but only Platform owns it; Rooms retains no
Billing or key-resolution cache and has no local fallback.

## Rooms transition and failure behavior

The migration replaces these six Rooms-side behaviors in one release, without a dual write or
runtime flag:

| Existing owner/call | v1 operation and preserved behavior |
|---|---|
| `MemberProvisioningService.FindOrCreateAsync` starting grant | Persist/resolve the Rooms member, then call `EnsureStartingCredit`. Preserve the current best-effort grant: a redacted availability failure is logged and sign-in continues; no local ledger write or request retry is introduced. |
| `POST /platform/keys` | Rooms authenticates/scopes/provisions the member, calls `IssuePlatformKey`, and returns the existing `{ key, email, balanceMicroUsd }` shape. The plaintext key occurs only in the success response and is never stored or logged. |
| `GET /me` | Rooms extracts the bearer, calls `ResolvePlatformKey`, maps only `invalid_platform_key` to the existing public 401, and obtains email/display data solely from `forge_rooms`. Internal 401/403 is a Rooms configuration fault and is generic public 503, not user authentication. |
| `Account.razor` balance | Call `GetBalance`; no Rooms ledger read remains. |
| `RoomAgentInvoker` admission | Call `GetBalance` and preserve the strict `balance > 0` admission rule. A lookup failure follows the existing outer failure path and never starts the Runner. |
| `RoomAgentInvoker` post-answer debit | After a successfully posted answer, call `SettleRun` best-effort. A failure is redacted/logged and never retracts or changes the delivered answer; only an uncertain request may replay with the same deterministic token. |

Existing Forge API/Billing code moves together under Platform; it is not a Rooms RPC consumer. An
outage has no in-process Billing, HMAC, cached-credit, or alternate-store fallback.

## Security, evidence, and rollback

Platform alone has `authbilling_db`, the Key Vault Billing connection/HMAC, platform-key
mint/verify/resolve, ledger, and pricing authority. Rooms retains `forge_rooms`, member/membership
authority, public routes, and user authentication; it receives no Billing source/package, Npgsql
connection, HMAC, key hash/secret persistence, or direct database path. Runner receives neither.

Before handoff, `forge-infra` must prove separate managed identities, role assignment, internal
ingress/DNS, Rooms token acquisition, and telemetry redaction. Deployed observations must show
Rooms and Runner denied the Platform database and Key Vault paths. Contract tests serialize every
DTO and error through `PlatformAccountsJsonContext` against fixed v1 JSON fixtures. Acceptance
proves member provisioning/one grant, key issue, `whoami`, account balance, funded agent admission,
one settlement, exact replay, conflict rejection, invalid/revoked-key neutrality, and unchanged
public unauthorised behavior. Negative evidence proves invalid workload tokens cannot mutate
ledger/key data and a Platform outage follows the stated behavior.

The existing tables remain unchanged. A previous Rooms image rolls back against them; rollback never
recreates a cross-repository project reference, dual write, or shared credential.
