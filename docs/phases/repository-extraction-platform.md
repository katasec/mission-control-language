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

## Next refinement — Billing package and cutover

| Item | Locked scope |
|---|---|
| Package | Publish `Katasec.Forge.Billing` **0.1.0** from `forge-platform`, with only its package metadata and package workflow. |
| Focused coverage | Move the four Billing-owned Postgres integration tests — `LedgerTests`, `PlatformKeyStoreTests`, `PlatformKeyResolverTests`, and `Api/BillingServiceClientTokenTests` — plus a Billing-only Postgres fixture. Do not take mixed Rooms or API coverage. |
| Root cutover | Replace the three MCL source consumers — `ForgeMission.Rooms.Tests`, `ForgeUI`, and `ForgeMission.Api` — with the 0.1.0 package; remove the Billing project from `src/ForgeMission.slnx` and delete `src/ForgeMission.Billing/` only after those references restore. |
| ForgeAPI restore | Add the private GitHub Packages restore plumbing required for `ForgeMission.Api` to restore `Katasec.Forge.Billing` in local and Actions builds; prove the Actions package-read grant with a real restore. |

Expected failures are the delivery mechanism: first destination package/test failures establish the
minimal test fixture and package metadata; then root restore/build failures establish the three
consumer cutovers and any missing private-package plumbing. Do not broaden this card to repair
unrelated Rooms or API failures.

**Verify:** `forge-platform` restores, builds, runs exactly the four moved integration tests, and
publishes/restores `Katasec.Forge.Billing` 0.1.0. The root then restores, builds, and tests the
three package consumers with no `ForgeMission.Billing` source project or project reference
remaining. These component and CI checks cannot close default-path acceptance. The final Platform
cutover must prove package-backed ForgeUI and ForgeAPI images, with `FORGE_PLATFORM_ENDPOINT` and
`FORGE_API_ENDPOINT` absent, using an approved existing credential for read-only `forge whoami`
and `POST /api/GetAccount`; both must report the same live account and balance.

**Out of scope:** the direct ForgeAPI `authbilling_db` credential path remains unchanged. Its
removal requires the separately designed ForgeAPI-to-Billing Tier-2 contract; it is not a
package-cutover fix.

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
