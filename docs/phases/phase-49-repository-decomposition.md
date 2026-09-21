# Phase 49 — Private repository decomposition

> **Status:** Program foundation in progress. No source, package, workflow, infrastructure, or
> repository extraction is authorized until the foundation and compatibility gates below pass.

## Why this phase exists

MCL currently contains several separately deployed products and bounded contexts in one solution.
That hides ownership and dependency direction, expands unrelated build graphs, and makes Native
AOT and delivery work unnecessarily broad. This phase moves one proven product boundary at a time
to a private repository while preserving the existing product behavior, contracts, data ownership,
and rollback route.

This is not permission to split projects cosmetically, invent a general shared library, change a
wire/store because files moved, or introduce a new deployment topology as an extraction shortcut.

## Locked program policy

| Policy | Decision |
|---|---|
| Privacy | Every new repository and every new NuGet package is private. Package consumers receive explicit GitHub Packages access; organization defaults are not assumed. |
| Source boundary | Products communicate through a named versioned package, HTTP contract, or durable-message contract. They never retain cross-repository `ProjectReference` links, shared store access, or credential sharing. |
| Change size | One accepted migration card has one owner and one behavior-preserving outcome. There is one code/IaC writer globally. |
| Delivery | A new repository first proves PR CI and a non-production artifact. Existing image names and deployed routes remain until the replacement artifact and normal product route are observed. |
| Infrastructure | `forge-infra` remains the deployment authority. New GitHub OIDC identities, package access, ACR roles, and deploy configuration are separately reviewed changes. No schema migration is coupled to an app deploy. |
| Rollback | The pre-program anchor is annotated tag `checkpoint-pre-repo-split-2026-09-22` at `06e11af30b6eb17d2a3c32e1e5b883bbe42c0d51`. Every accepted card adds exact source/package/image/infra rollback facts before old paths retire. |

## Proposed repository registry

| Repository | Privacy | Bounded owner | Planned source | External contracts / delivery owner |
|---|---|---|---|---|
| `forge-mcl` | Private | MCL syntax, generic execution, provider/retrieval adapters, CLI and mission-package client | Parser, Core, ChatClients, Scout after seam cleanup, Serve, CLI | MCL packages and CLI release |
| `forge-runner` | Private | Stateless hosted mission execution | Runner and Runner Contracts | Runner contract and `forge-runner` image |
| `forge-conversations` | Private | Durable conversation admission/state and mission work | Conversations Contracts, ConversationHost and ConversationWorker; shared conversation presentation placement remains pending D49-05 | Conversation contract and service images |
| `forge-desktop` | Private | Local Application, capabilities, UI, native host, supervision and local runtime orchestration | Application*, ClientRuntime, Presentation, Desktop* and Orchestration; Docker support placement remains pending D49-06 | Desktop packages and canonical Desktop artifact |
| `forge-platform` | Private | Public platform edge and account/ledger authority | Api and Billing | Platform client contract and `forge-api` image |
| `forge-rooms` | Private | Collaboration domain, Rooms store and browser product | Rooms, Rooms.Data, ForgeUI | Rooms browser image and migration bundle |
| `forge-missions` | Private, deferred | Independently versioned OCI mission/expert content only | No source move is approved yet | OCI content artifacts only |
| `forge-infra` | Existing authority | Deployment topology, identities, roles and migration-job definitions | No source move | Existing Makefile/Bicep authority |

`forge-missions` remains deferred until a separate design proves an independent content lifecycle.
The Platform API's public mission policy catalog is not the same owner as MCL OCI package retrieval.

## Dependency direction

`forge-mcl` is a package/toolchain dependency for Runner, Conversations and Desktop. Runner and
Conversations publish separate contracts. Desktop consumes the Conversation contract; Platform
consumes the Runner contract; Rooms consumes only Runner and Platform client contracts. These are
logical arrows, not authorization for direct store access.

Runner and durable Conversations are deliberately separate repositories: they have different
durable-state, release, and failure boundaries. A repository is not a security boundary; the
[Security Architecture](../design/security-architecture.md) continues to govern tiers, identities,
and datastore access.

## Program ledger

| Card | Outcome | Target | Status | Prerequisite | Evidence / rollback |
|---|---|---|---|---|---|
| 49.1 | Program governance, agent roles, decision/compatibility/rollback ledger and task-card standard | Current repository docs | **Implementing** | None | This spoke; pre-program tag above |
| 49.2 | Reproducible dependency, AOT, CI, package, identity and deployment baseline | Current repository docs | Not started | 49.1 accepted | Named observations; no behavior change |
| 49.3 | Resolve the active Phase 45/46 compatibility interlock | Current repository docs | Design blocked | Operator decision D49-01 | Versioned compatibility baseline |
| 49.4 | Remove proven CLI reverse dependencies and Core-to-concrete-Scout coupling | Current repository | Not started | 49.2, 49.3 | Focused/full/AOT evidence |
| 49.5 | Private package foundation and external-consumer proof | Package owners | Not started | 49.4 | Package versions and access observation |
| 49.6–49.11 | One repository extraction per accepted product boundary | Target repository above | Not started | Relevant package and compatibility gates | Per-card cutover checkpoints |
| 49.12 | Optional mission-content decision and monorepo retirement | Deferred | Not started | All accepted extractions | No cross-repo source references |

## Decision ledger

| ID | Classification | Question | Status | Blocks |
|---|---|---|---|---|
| D49-01 | Type 1 compatibility sequencing | Complete Phase 45.5/46's active Desktop/Conversation work first, or freeze it against an explicit versioned compatibility baseline before extraction? | Open — do not infer | 49.3 and Conversations/Desktop extraction |
| D49-02 | Type 2 repository disposition | Does this repository become/rename to private `forge-mcl`, or remain an archived coordination repository after a new `forge-mcl` is created? | Open — do not infer | `forge-mcl` bootstrap |
| D49-03 | Type 1 contract policy | Define supported consumer-version windows and NuGet/HTTP ownership for each public contract. | Open | Private package foundation |
| D49-04 | Type 1 platform boundary | Define the narrow Platform service/client route that removes Rooms' in-process Billing dependency without granting Rooms Billing-store access. | Open | Platform and Rooms extraction |
| D49-05 | Type 2 UI package placement | Decide whether shared conversation activity rendering travels with Conversations as a semantic package or a dedicated UI package. | Open | Conversations/Desktop/Rooms extraction |
| D49-06 | Type 2 reusable local-support placement | Decide the smallest owner/package boundary for Docker operations used by both the CLI and Desktop orchestration without introducing an MCL-to-Desktop dependency or duplicate implementation. | Open | MCL and Desktop extraction |

## Spokes

- [49.1 — Program foundation](phase-49.1-program-foundation.md) — active governance and agent protocol.
- 49.2 baseline, 49.3 compatibility, 49.4 MCL seam cleanup and later extraction spokes are created only when their prerequisites make their design build-ready. They must not be pre-filled with inferred contracts.

## Done when

Every proposed source move has passed its own approved task card, private repository/package
consumer proof, CI artifact observation, required default path, and reversible cutover. No product
retains a cross-repository source reference or reaches another bounded context's store. The final
aggregate solution has been retired only after all consumers run against released contracts.
