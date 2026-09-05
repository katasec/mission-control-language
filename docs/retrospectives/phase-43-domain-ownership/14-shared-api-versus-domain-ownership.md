# Concern 14 — Shared API versus domain ownership

Recorded: 2026-09-05. Status: cross-cutting architectural hypothesis; review after Project Mission reconstruction reaches a verified baseline. Evidence inspected through reconstruction commit `5faf6f2`. This inventory does not supersede the active architecture or approve implementation.

## Observed rule and history

Commit `ed0616f` added presentation-surface parity on August 30: every product action must be expressible through a named Client Runtime contract. The same change introduced Project records and made Project opening the only route to an initial execution session.

See [Forge Architecture](../../design/forge-architecture.md#presentation-surface-parity--non-negotiable), `src/ForgeMission.ClientRuntime/Transport/ClientRuntimeEndpoints.cs`, `src/ForgeMission.ClientRuntime.Transport/ClientRuntimeContracts.cs`, and `src/ForgeMission.Tests/Architecture/ClientRuntimePresentationBoundaryTests.cs`.

## Boundary concern

Keeping business rules out of Presentation and sharing actions across Desktop/TUI are valuable constraints. Neither implies Bob must own every product domain. A shared application API can route to separately owned Project, conversation, submission, and capability responsibilities.

The historical pattern appears to be: work cannot belong in the UI, so it is assigned to Client Runtime. This conflates API entry point, domain owner, package, and process. It is an interpretation of the changes, not evidence that every endpoint needs a new service.

## Later discussion

Define what Client Runtime names: Bob's execution engine, an application facade, or today's combined executable. Map each command to a domain owner before choosing package/process boundaries. Preserve surface parity and authorization while updating any tests or documentation that encode the old ownership assumption. The other thirteen notes are review areas, not thirteen mandatory actors. Default-path acceptance: N/A — documentation only.
