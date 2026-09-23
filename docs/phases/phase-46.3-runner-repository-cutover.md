# Phase 46.3 — Runner repository cutover

> **Status: done.** Part of
> [Phase 46](phase-46-domain-ownership-remediation.md). Source-move evidence is in the
> [completion record](phase-46.3-runner-repository-cutover_completed.md).

## Decision and component fit

`forge-runner` is now the sole owner of `ForgeMission.Runner`,
`ForgeMission.Runner.Contracts`, their focused tests, Runner image workflow, and the eight baked
fallback missions. Removing their duplicate source from this repository advances the Runner
boundary: provider-backed execution no longer appears to be owned by the temporary coordination
repository.

The temporary local source-link cutover has been replaced by the package boundary.
`Katasec.Forge.Runner.Contracts` and `Katasec.Forge.Runner` `0.1.0` are published from the Runner
repository and consumed from GitHub Packages. No consumer retains a relative source path to
`~/progs/forge-runner`.

## Locked consumer changes

| Consumer | Required cutover |
|---|---|
| `ForgeMission.Billing` | Replace the source link with `Katasec.Forge.Runner.Contracts` `0.1.0`. |
| `ForgeUI` | Replace the source link with `Katasec.Forge.Runner.Contracts` `0.1.0`. |
| `ForgeMission.Api` | Use a direct `Katasec.Forge.Runner.Contracts` `0.1.0` dependency; it must not receive the wire only through Billing. |
| `ForgeMission.Rooms.Tests` | Replace the source links with `Katasec.Forge.Runner` and `Katasec.Forge.Runner.Contracts`, both `0.1.0`. |

The test-only host package reference is a temporary Type-2 exception. The Runner assembly already
grants `InternalsVisibleTo("ForgeMission.Rooms.Tests")`, so the existing tool-round-trip test keeps
its narrow access without a source-project link. A later test-boundary task replaces it with
black-box HTTP coverage and removes both this host package dependency and that friend-assembly
entry. Production projects consume Contracts only.

## Architecture and failure boundaries

This task changes no HTTP wire, public entry point, store, secret, identity, or default runtime
configuration. Runner remains the Tier-2 compute owner; its cache stays its only Tier-3 store.
The build-graph change is Type 2: it is reversed by restoring the prior package version or, only
as an emergency local recovery, the temporary sibling source links.

The package boundary is explicit: a checkout lacking read access to the private packages, or an
unavailable pinned version, fails during restore rather than resolving source from a sibling
checkout. The recovery owner is the repository maintainer: restore GitHub Packages read access or
publish the pinned version. Focused evidence is a clean monorepo restore/build/test with no Runner
sibling-source references, plus the Runner workflow's package artifact evidence.

## Completion

The duplicated source, tests, Runner Dockerfile/workflow, and eight fallback mission trees are
gone from this repository. All five source links are package references and the monorepo restores,
builds, and tests without a sibling Runner source reference. See the
[completion record](phase-46.3-runner-repository-cutover_completed.md) for the source-move
inventory and verification observations.

Default-path acceptance for runtime behavior is N/A: the published Runner image and its normal
configuration are unchanged. The applicable default build path is a normal monorepo restore with
only its configured GitHub Packages source; it passed without a sibling Runner source reference.
An image rebuild/deploy remains a separate task once CI OIDC is moved.
