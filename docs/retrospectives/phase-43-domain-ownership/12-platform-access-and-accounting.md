# Concern 12 — Platform access and accounting

> **2026-09-06 disposition:** resolved in the [finalized end state](end-state.md#disposition-of-all-fourteen-concerns); implementation follows [43.23](../../phases/phase-43.23-domain-ownership.md). The original inventory below is historical.

Recorded: 2026-09-05. Status: cross-cutting ownership review candidate; revisit after Project Mission reconstruction reaches a verified baseline. History and code inspected through reconstruction commit `5faf6f2`. This note does not assert a current billing or authentication defect.

## Responsibilities and present owners

Obtain and forward platform credentials, authenticate service requests, integrate the cloud gateway, identify a logical billable invocation, and settle billing without charging tool continuations or retries incorrectly.

Phase 43.14 extended the cloud mission path and specified terminal-only settlement with a stable client token. It explicitly distinguishes billing/idempotency identity from enrichment re-entrancy correlation. See the [cloud-mission design and evidence](../../phases/phase-43.14-desktop-cloud-missions.md).

Relevant locations include `src/ForgeMission.Api/`, `src/ForgeMission.Billing/`, `src/ForgeMission.Desktop/DesktopBoot.cs`, and `src/ForgeMission.ClientRuntime/Services/CloudMissionRuntimeSession.cs`.

## Boundary concern

Platform access/accounting is distinct from Bob's local capability authorization and from mission reasoning. Carrying a credential or correlation token does not make the carrier the owner of identity or billing rules. Authentication and accounting may themselves need separate owners; this inventory does not collapse them into one proposed actor.

## Later discussion

Trace identity, credential acquisition, token propagation, settlement authority, and retries independently. Verify that local permission policy, platform authentication, and billing cannot become interchangeable concepts during extraction. Do not expose provider or datastore credentials to Presentation. Default-path acceptance: N/A — documentation only.
