# Repository extraction — Platform

> **Status:** active. This is the next repository cutover in the approved decomposition map.
> Its first intentionally incomplete move is Billing only; the resulting build and test failures
> refine the remaining Platform boundary. Live Mission Chat remains deferred until the full
> repository sequence is complete.

## Why this exists

`forge-platform` will own the hosted API and Billing boundary: account identity, platform keys,
pricing, balances, ledger, and the API entry point that composes those concerns with Runner.
Moving the existing bounded owners out of the temporary coordination repository makes their
dependencies, package contracts, and deployment responsibilities explicit without inventing a new
runtime or broadening product scope.

## Approved first move

Move — never copy — `src/ForgeMission.Billing/` and the clearly pure
`src/ForgeMission.Rooms.Tests/PlatformKeyMintingTests.cs` coverage into `forge-platform`. Create
only the destination solution/project and minimal restore/build configuration needed to attempt
its independent build and test. Do not move Rooms, ForgeUI, Desktop, Conversation, or the mixed
Rooms test fixture.

The implementer must make this move early. Expected failures are evidence, not a reason to expand
the inventory first: destination restore/build setup, the missing Platform-owned test fixture, and
former source consumers after Billing disappears. Record each observation, then use it to define
the next bounded move.

## Security and default-path disposition

Billing remains the owner of `authbilling_db`, platform-key resolution, credit policy, and ledger
mutation. This first move changes no public endpoint, data schema, credential scope, or deployed
identity. `ForgeMission.Api` remains outside the move because the public API currently receives an
`authbilling_db` connection and uses Billing in-process. That inherited Tier-1-to-datastore path is
not the target architecture and must not expand during extraction. Moving the API later requires a
separately locked Tier-1-to-Billing Tier-2 contract and removal of the API's datastore connection
and RBAC; it is not a packaging refinement for this task.

The final Platform cutover's default-path acceptance is: package-backed ForgeUI and ForgeAPI images,
with `FORGE_PLATFORM_ENDPOINT` and `FORGE_API_ENDPOINT` absent, use an approved existing test
credential for read-only `forge whoami` and `POST /api/GetAccount`. Both must report the same live
account and balance. The completion record must name the Billing package version, image revisions,
and normal hosted dependency provenance. It must also prove ForgeAPI's private-package restore
plumbing and Actions package-read grant before any API image is accepted.

## Done when

For this first move: `forge-platform` independently restores, builds, and runs the moved focused
test; the root consumer failure/refinement evidence is recorded; and both repositories are merged
back to clean `main`. This does not complete the Platform extraction or authorize an API move.
