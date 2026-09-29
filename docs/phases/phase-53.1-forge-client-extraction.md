# Phase 53.1 — forge-client extraction

> **Status: build-ready (2026-09-29).** All four open questions closed; see [Locked decisions](#locked-decisions).
> Hub: [Phase 53](phase-53-forge-client.md).

**Goal:** a new `forge-client` repo publishes `Katasec.Forge.Hands` (from `ForgeMission.ClientRuntime`),
`Katasec.Forge.Client.Contracts` (from `ForgeMission.Application.Transport`), and
`Katasec.Forge.Client` (from `ForgeMission.Application`, including `Sessions/`). forge-desktop
consumes all three packages. No behaviour changes.

## Locked decisions

| Area | Decision |
|---|---|
| Nature of the move | Move, not rewrite: code moves byte-identical except named namespace/reference edits (same rule as the Phase 50 extraction). |
| Packages (Q1) | Three packages from one repo, each an existing project moved as-is: `Katasec.Forge.Hands` ← `ForgeMission.ClientRuntime`; `Katasec.Forge.Client.Contracts` ← `ForgeMission.Application.Transport`; `Katasec.Forge.Client` ← `ForgeMission.Application` (depends on Hands and Client.Contracts). |
| `Sessions/` goes with Client (Q1) | `ApplicationSessionService` owns Bob session lifetimes, not UI state; 11 Application files outside `Sessions/` depend on it and the composition root builds it. |
| Contracts is its own package (Q1) | Application's public service interfaces take and return Transport types, so the Client needs them. Folding them into Client would pull Mcl.Core, OnnxRuntime, and the ASP.NET framework reference into the Presentation WASM build, which references Transport only. |
| Stays in forge-desktop | UI shell (Desktop, Desktop.Host, Desktop.Contracts, Presentation), Application Host (`/transport/*` endpoints), Supervisor, Orchestration. |
| Namespaces and assemblies (Q2) | Keep every namespace and assembly name; set only `PackageId` (forge-mcl / forge-conversations precedent). `InternalsVisibleTo` and assembly-name architecture tests keep working. No rename backlog item. |
| Tests (Q3) | forge-client mirrors the layout: `src/ForgeMission.slnx` plus a test project named `src/ForgeMission.Tests`, so `InternalsVisibleTo("ForgeMission.Tests")` and the `RepositoryRoot()` helpers work unchanged. **Move as-is:** `ClientRuntime/ClientExecutionSessionTests`, `ClientRuntime/MissionExecutionProfileTests`; `Application/` — `ApplicationEndpointsTests`, `ApplicationSessionServiceTests`, `ConversationHostClientMessageTests`, `ConversationHostClientProjectTests`, `ConversationServiceCloudMissionTests`, `ConversationTailReaderTests`, `LegacyMissionProtocolClientTests` + `LegacyMissionProtocolFixture`, `MissionAuthoringServiceTests`, `MissionConversationServiceTests`, `MissionHandsConversationServiceTests`, `MissionVersionServiceTests`, `ProjectContentServiceTests`, `ProjectMissionToolRefusalTests`, `ProjectRunReadStateTests`, `ProjectServiceTests`, `RunHistoryServiceTests`; `Architecture/ApplicationCompatibilityBoundaryTests`, `Architecture/ClientExecutionBoundaryTests`. **Named edits:** `Application/MissionSubmissionServiceTests` moves minus its unused `using ForgeMission.Application.Host;` (confirmed by compiling in forge-client, not by inspection); `Architecture/MclPackageConsumptionTests` splits — Application/Transport package pins to forge-client, whole-solution checks stay. **Stay in forge-desktop:** `ProjectTransportContractTests`, `Orchestration/LocalDockerMissionRuntimeLauncherTests`, `DesktopSupervisorHostBoundaryTests`, and all other `Desktop/`, `Presentation/`, `Orchestration/`, `ApplicationHost/`, `Architecture/` tests. Client.Contracts gets no new tests (move-only). |
| Dependencies (Q4) | Pin every version exactly as in forge-desktop today: `Katasec.Forge.Mcl.Core` 0.1.0 (the only forge-mcl package used; brings Parser/Scout 0.1.0, AITools 0.1.8, Microsoft.Extensions.AI 10.7.0, OnnxRuntime 1.27.0, YamlDotNet 18.0.0), `Katasec.Forge.Conversations.Contracts` 0.4.0, and the test project's packages at their current versions. Copy `nuget.config` unchanged (nuget.org + `nuget.pkg.github.com/katasec`, `%NUGET_AUTH_TOKEN%`). No bumps. |
| Package access (Q4) | "Manage Actions access" grants are needed only for **private** packages, and must exist before the consuming repo's CI first runs (52.1 lesson: a missing grant fails restore with 403). forge-client needs grants on Mcl.Core, Mcl.Parser, Mcl.Scout, Conversations.Contracts (private; checked with `gh api orgs/katasec/packages/nuget/<name>` 2026-09-29). AITools and AnthropicServer are public: no grant. forge-desktop needs grants on all three new forge-client packages. |

## Gates

| Gate | Result |
|---|---|
| Architecture / security | Client-side only; no server, store, or credential change. Hands keeps its sandbox and policy unchanged; Client must reach tools only through Hands. |
| Engineering philosophy | Removes a future duplicate path (CLI reimplementing the client). No new abstractions: packages are the existing code. |
| Default path | Unchanged. Proof: the 52.1 Task 8 default-path procedure (published bundle, zero arguments, `/transport/*` Project turn against the cloud, one debit) passes after the Desktop switches to the packages. |

## Done when

1. `forge-client` builds and tests pass; all three packages are published.
2. forge-desktop references the packages instead of the three projects; its tests pass.
3. No test lost: the forge-desktop test count on `main` immediately before the move (re-measured then;
   2026-09-29 figure from the design session: 347 passed + 1 skipped = 348) equals forge-client's
   moved tests + forge-desktop's remaining tests, counting both halves of the
   `MclPackageConsumptionTests` split. The arithmetic is reported.
4. The 52.1 Task 8 default-path procedure passes on the published bundle.

## Tasks

| # | Task | Output |
|---|---|---|
| 1 | Create the `forge-client` repo with the mirrored `src/` layout; move the three projects and the Q3 test set; add `PackageId`/packaging metadata and one publish workflow per package (forge-mcl `publish-core-package.yml` template). | forge-client builds; tests pass; the MissionSubmissionServiceTests edit compiles. |
| 2 | Grant forge-client's Actions access on the four private dependencies; publish the three packages; grant forge-desktop access on them. | Three packages visible on the katasec org, private, linked to forge-client. |
| 3 | Switch forge-desktop to the packages; delete the three projects and moved tests; apply the MclPackageConsumptionTests split. | Desktop builds and tests pass; test-count arithmetic reported (Done when 3). |
| 4 | Run the 52.1 Task 8 default-path procedure on the published bundle. | Done when 4. |

## Open questions

1. ~~Exact cut line~~ — closed 2026-09-29: three packages; see Locked decisions.
2. ~~Namespaces~~ — closed 2026-09-29: keep names, `PackageId` only; see Locked decisions.
3. ~~Tests~~ — closed 2026-09-29: see Locked decisions and Done when 3.
4. ~~Core dependency~~ — closed 2026-09-29: see Dependencies and Package access in Locked decisions.

## Follow-ups after the move

| Item | Detail |
|---|---|
| `IApplicationChannel` / `HttpApplicationChannel` | Desktop UI → Application Host HTTP channel: Desktop wiring, not client-core contract. Carried into Client.Contracts unchanged (move-only); afterwards move it back into forge-desktop, or delete it with Phase 52.4. |
| Shared test-assembly name | Both repos' test assemblies are named `ForgeMission.Tests`, so `InternalsVisibleTo` on Client/Hands also exposes their internals to forge-desktop's tests. Acceptable for the move; no rename. Whether any Desktop test actually uses a Client/Hands internal is unverified (`LegacyMissionProtocolClient`, used by `LocalDockerMissionRuntimeLauncherTests`, is `public`) — compiling Desktop against the packages shows it. The Docker-launcher dependency goes away with the deferred Phase 52 Trim Orchestration. |
