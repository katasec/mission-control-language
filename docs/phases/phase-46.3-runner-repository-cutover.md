# Phase 46.3 — Runner repository cutover

> **Status: done.** Part of [Phase 46](phase-46-domain-ownership-remediation.md). Build and test
> evidence is in the [completion record](phase-46.3-runner-repository-cutover_completed.md).

## Decision and component fit

`forge-runner` is now the sole owner of `ForgeMission.Runner`,
`ForgeMission.Runner.Contracts`, their focused tests, Runner image workflow, and the eight baked
fallback missions. Removing their duplicate source from this repository advances the Runner
boundary: provider-backed execution no longer appears to be owned by the temporary coordination
repository.

The operator has selected a temporary local source-link cutover. The four identified consumers
use a relative C# project path from their project directory to the sibling checkout:
`..\\..\\..\\forge-runner\\src\\...`. This assumes both repositories are direct children of
`~/progs`. It is not the permanent distribution boundary. The removal condition is publication
and adoption of `Katasec.Forge.Runner.Contracts`; production consumers must then use that package.

## Locked consumer changes

| Consumer | Required cutover |
|---|---|
| `ForgeMission.Billing` | Replace its local Contracts reference with the sibling Contracts project. |
| `ForgeUI` | Replace its local Contracts reference with the sibling Contracts project. |
| `ForgeMission.Api` | Add a direct sibling Contracts project reference; its source imports these types and must not depend on Billing's transitive reference. |
| `ForgeMission.Rooms.Tests` | Replace its local Runner reference with the sibling Runner host and add a direct sibling Contracts reference. |

The test-only host reference is a temporary Type-2 exception. It retains the established
`InternalsVisibleTo("ForgeMission.Rooms.Tests")` boundary so the existing tool-round-trip test
continues to compile. A later test-boundary task replaces it with black-box HTTP coverage; no
production project may source-reference the Runner host.

## Architecture and failure boundaries

This task changes no HTTP wire, public entry point, store, secret, identity, or default runtime
configuration. Runner remains the Tier-2 compute owner; its cache stays its only Tier-3 store.
The build-graph change is Type 2: it is reversed by restoring the removed files from the pre-cutover
commit and their local references.

The new failure boundary is explicit: a checkout without sibling `~/progs/forge-runner` fails at
MSBuild project resolution instead of silently compiling duplicate Runner code. The recovery owner
is the repository maintainer: restore the sibling checkout or complete the Contracts package
cutover. Focused evidence is the monorepo build after deletion, the full test suite, and an
independent Runner-repository build/test.

## Completion

The duplicated source, tests, Runner Dockerfile/workflow, and eight fallback mission trees are
gone from this repository. The four consumers now compile through the sibling checkout. See the
[completion record](phase-46.3-runner-repository-cutover_completed.md) for the inventory and
verification observations.

Default-path acceptance is N/A for this code-motion cutover: the published Runner image and its
normal configuration are unchanged. Its last default-image health observation is recorded in the
Runner repository's `docs/extraction.md`; an image rebuild/deploy is a separate task once CI OIDC
is moved.
