# Recommended end state

**Design complete; implementation pending.** Baseline and provenance: [index](README.md). Contracts: [contracts](contracts.md). Evidence keys below refer to [reference evidence](references.md). These are fixed decisions for the ownership refactor, not a description of already-extracted code.

## Actors and boundaries

| Owner | Responsibility | Does not own |
|---|---|---|
| Presentation | Input, rendering, navigation, focus and view state | Project rules, filesystem access, mission execution, credentials |
| Application services | Project lifecycle, content, selection, submission/reconciliation, client conversation lifecycle and history access | Durable remote run authority, model reasoning, capability policy decisions |
| Application Host | Local HTTP/SSE binding, DI composition, JSON, readiness and static assets | Business rules, process supervision, a second run loop |
| Client Runtime (Bob) | Scoped local capabilities, policy/confirmation enforcement, execution, audit and execution cleanup | Project manifests, mission names, conversation events, HTTP, UI, platform tokens |
| Conversation Host | Durable command admission, deduplication, ordered run/event state and delivery coordination | Client Project files or mission-specific reasoning |
| Mission Worker / Mission Runtime | Mission interpretation, experts/models, tool requests and execution recovery | Conversation-store writes, local capability authorization |
| Desktop Supervisor / Orchestration | Resolve dependencies, start/check/stop owned children and local bridge | Project use cases, tools, UI framework behavior |
| Platform identity and billing owners | Platform authentication and accounting under existing API/service contracts | Local file/terminal policy or client application state |

These are responsibility boundaries. Host and Worker remain existing deployed components. Application services and Bob share one local process; they are not new distributed services.

```text
Desktop / future TUI
        |
        v
Application.Transport (shared actions and event vocabulary)
        |
        v
Application.Host (loopback HTTP/SSE, composition, static assets)
        |
        +--> Application services --> Project manifest/content adapters
        |             |
        |             +--> Conversation/Mission protocol adapters --> remote owners
        |             |
        |             +--> ClientRuntime (Bob) --> local capability providers
        |
        +--> Presentation static assets

Desktop Supervisor starts Application.Host and native Host;
Orchestration resolves remote/local runtime dependencies before injection.
```

Application-owned file I/O is real local I/O: the Project owner reads/writes its manifest and resolves requested content. Bob owns **agent capability execution**, not every disk access in the process. User-selected Project content has its own narrow containment/size/hash checks; it is not a general filesystem escape hatch for a model. [D1, D2, D6]

## Disposition of all fourteen concerns

| # | Concern | Final owner and action | Reason / reference |
|---|---|---|---|
| 1 | [Project management](01-project-management.md) | `Application/Projects/ProjectService`: move and rename `ProjectStore`; keep `ProjectManifestFile` as its private persistence adapter. Own identity, goal, home, manifest validation, transactions and journal writes. | A Project is a domain entity, not an execution session. Avoid a forwarding wrapper around an unchanged second owner. [M1, D2] |
| 2 | [Mission selection](02-mission-selection.md) | `ProjectService.SelectMissionAsync` owns the Project mutation. Rename `ProjectMissions` to `MissionCatalog` under `Application/Missions`; keep the existing fixed `ProjectMissionNames` vocabulary. | Catalog availability and a Project's selection are distinct; no registry/OCI service is needed for two supported missions. [M1, D1] |
| 3 | [Submission and recovery](03-mission-submission-and-recovery.md) | Rename `ProjectMissionApplication` to `Application/Missions/MissionSubmissionService`. It prepares, sends and reconciles immutable commands using ProjectService and the Host adapter. | Client submission intent is distinct from Host admission and Worker execution. [M2, O1] |
| 4 | [Conversation management](04-conversation-management.md) | `Application/Conversations/ConversationService` owns client start/follow-up identity and its scoped lifetime. `ConversationHostClient` remains the single durable HTTP adapter. | Do not duplicate remote persistence. Existing legacy conversation identity survives only its existing local lifetime; no new durable reopen facility is implied. [M3, H1] |
| 5 | [Run history and observation](05-run-history-and-observation.md) | `Application/Runs/RunHistoryService` owns verified bounded reads. `RunObservationService` owns subscription/reconnect/invalidation. Extract both from `ProjectMissionReadSession`; reuse `ProjectRunReadState` where currently consumed. | Querying and observing have different lifetimes; neither owns execution or canonical history. [M4, D3, O2] |
| 6 | [Workbench content](06-workbench-content.md) | Rename the remainder of `ProjectWorkbenchService` to `Application/Projects/ProjectContentService` after moving selection to ProjectService. Keep its narrow file reader private to that owner. | Manifest semantics and document identity belong to Projects; layout belongs to Presentation. [M1, D2] |
| 7 | [Client conversation orchestration](07-client-conversation-orchestration.md) | Keep compatibility round trips in `Application/Adapters/Missions/LegacyMissionProtocolClient` and `LegacyCloudMissionProtocolClient`. Extract `LegacyJanusToolDelivery` from ConversationRuntimeSession. Do not introduce `MissionInteractionCoordinator`. | Protocol continuation is not a new universal agent loop. Durable mission reasoning remains remote. [M3, D4, H1] |
| 8 | [Application sessions](08-application-session-management.md) | `Application/Sessions/ApplicationSessionService` owns the session map, validated Project binding, immutable runtime/mission selection and disposal. Bob owns a separate `ClientExecutionSession`, whose root/policy/lifetime are supplied by Application. | Domain identity, client attachment and execution authority have different lifetimes. [M5, D5] |
| 9 | [Presentation hosting](09-presentation-hosting.md) | `Application.Host` serves WASM/assets and transport; `Presentation` owns UI code. Move Program, endpoint binding, ready response and SSE delivery out of Bob. | Serving an API or asset does not confer ownership of the domain behind it. [D6, H1] |
| 10 | [Supervision](10-runtime-supervision-versus-execution.md) | Preserve `Desktop`, `Desktop.Host`, `Desktop.Abstractions` and `Orchestration` responsibilities. Rename the supervised child adapter to `ApplicationHostProcess`. | Already substantially separated; update wiring, not the process architecture. [M6] |
| 11 | [Durability versus reasoning](11-durable-coordination-versus-mission-reasoning.md) | Preserve ConversationHost admission/store/outbox and Worker execution/checkpoint/progress ownership. No remote code relocation is required by this refactor. | Host authoritative state, Worker recovery state and model decisions are different facts. [M7, D3, O1] |
| 12 | [Platform access/accounting](12-platform-access-and-accounting.md) | Existing platform identity/API/Billing owners remain authoritative. Supervisor supplies platform credential; Application Host injects it into the appropriate HTTP client. Legacy cloud adapter carries the existing stable billing token. | Credential transport is not authentication ownership; billing tokens are not capability grants. [M8, H2] |
| 13 | [Janus versus generic infrastructure](13-generic-infrastructure-versus-janus-specific-behaviour.md) | Keep Worker Janus mapping in `Worker/Janus`. Put the existing participant/name checks in `Application/Adapters/Janus/LegacyJanusToolDelivery`. Keep `ProjectMissionToolRefusal` in the Application protocol path. | Generic Bob sees local operations only. Preserve the Implementer check at the adapter and capability policy at Bob; do not delete either. No new grant protocol or plugin framework. [M3, M7, D1] |
| 14 | [Shared API versus ownership](14-shared-api-versus-domain-ownership.md) | Rename the shared channel/DTO assembly to `Application.Transport`. Every surface invokes the same Application action. Thin `ApplicationApi` dispatches to internal owners; endpoint groups bind HTTP only. | API parity is independent of domain ownership. DeepSeek's own ClientRuntime is a client object layer, not Bob. [D6] |

## Libraries and process placement

| .NET project | Kind / allowed direct dependencies | Disposition |
|---|---|---|
| `ForgeMission.Application` | Library: Application.Transport, ClientRuntime, Core, Conversations.Contracts; HTTP/client abstractions as needed | One library containing Projects, Missions, Conversations, Runs, Sessions, Interaction and Adapters folders. No ASP.NET, Blazor, native Host, Supervisor or provider SDK implementation. |
| `ForgeMission.ClientRuntime` | Library: Core capability contracts/providers only | Convert the name to Bob; no entry point, web SDK, Application, Transport, Conversations.Contracts, Orchestration or Presentation reference. Broad existing Core dependency is retained, not a Core reorganization. |
| `ForgeMission.Application.Host` | .NET 10 ASP.NET Native AOT executable: Application, Application.Transport, ClientRuntime, Presentation | Takes over today's ClientRuntime executable packaging and Program. No extra local child process. |
| `ForgeMission.Application.Transport` | Library: existing Conversations.Contracts dependency | Rename ClientRuntime.Transport, including `IApplicationChannel`, `HttpApplicationChannel`, `ApplicationEvent`, `ApplicationEventKind`, `ApplicationJsonContext`; keep DTO record names and all wire fields/routes/numeric enum values. It owns the client HTTP channel and shared action/event vocabulary, not server endpoints. |
| `ForgeMission.Presentation` | Existing WASM UI, renamed from ClientRuntime.Presentation | References Application.Transport and existing presentation-only libraries. No Application, Bob, Core or direct runtime HTTP/storage access. |
| `ForgeMission.Application.TransportProbe` | Existing probe, renamed from ClientRuntime.TransportProbe | Surface-neutral proof of the shared channel. |
| Existing Desktop / Host / Orchestration / Host and Worker projects | Existing processes and dependencies | Only necessary local launch/build references change; no new hosted deployment. |

Service classes do not each receive a project or interface. Add no DI discovery/reflection, plugin loader, event bus framework, generic repository, distributed queue or settings knobs. Use concrete owners and existing typed interfaces at real boundaries. `Core/Tools` remains shared; reclassifying all Core code is outside this change. Bob does not expose its provider registry, even though the existing Core registry internally contains tool-schema machinery.

The supervised child becomes the sibling executable `ForgeMission.Application.Host`. It still listens on `127.0.0.1:0`, serves the same `/transport/*`, `/ready` and static paths, and emits the existing `FORGE_CLIENT_RUNTIME_URL=` readiness line. Retaining that exact wire marker is intentional compatibility, not a second runtime. Build/publish scripts and Supervisor change together. Desktop's published entry point, zero-argument invocation, runtime endpoint defaults and bridge ownership do not change. Rename internal identifiers and diagnostics to Application Host; do not require users to configure new names.

## Security and engineering review

| Gate | Decision and proof required |
|---|---|
| Data owners | Local ProjectService alone mutates `forge.project.json` through its existing atomic transaction. Submission journal is local intent/receipt, not remote truth. ConversationHost alone owns its durable store; Worker retains its own recovery state and reports via the existing contract. No cross-store access. |
| Entry points / tiers | Application Host remains local loopback, not an internet-facing datastore service. Hosted edge → internal Conversation/Mission/Billing owner → owner store remains unchanged. Existing local Kind proof exception remains bounded by the deployment design; it is not authorization for a public direct Host endpoint. |
| Credentials | Presentation/native Host/Bob receive no platform/provider/datastore credentials through DI. Supervisor retains current credential acquisition and injects only platform HTTP credentials into Application Host; remote provider keys remain with their existing runtime owners. Same-process libraries are compile-time ownership boundaries, not an OS secret sandbox; existing subprocess secret isolation must remain tested. |
| Authority | Project mission declarations remain empty; their protocol handler has no Bob dependency. Legacy Janus adapter preserves participant checks; Bob enforces policy and confirmations for each dispatched operation. Direct capability requests retain policy checks. Application content readers accept only existing Project entry identities and preserve containment rules. |
| Type classification | Domain/data ownership and allowed dependency direction are Type 1. Local assembly/executable renames and shared-process placement are reversible Type 2; revert coordinated packaging and references without changing stored/wire formats. No new security exception. |
| Surface parity | PASS by design: all actions use Application.Transport and identical domain errors below Presentation. Update architecture tests to prove actual dependency/behavior boundaries rather than retaining the old “ClientRuntime owns everything” naming assumption. |
| Desktop quality | Required behavior: same launch, Project actions and cleanup. Supervisor owns child lifetime; native Host remains replaceable. No Photino callback/API change is required or assumed. Replacement-boundary tests and published zero-argument Desktop exit observations prove the outcome. Window close, native Host exit and Supervisor exit remain distinct signals. |
| UI | No visual redesign, new control, token or accessibility behavior. Reuse reconstruction's existing renderer and its recorded reference/states. Any visible drift is a regression requiring comparison under Desktop Interaction Principles and UI Design System, not a reason to invent a new reference. |
| Complexity / failures | One Project write owner, one adapter per remote protocol, one execution boundary, bounded read observation and explicit lifecycle drain. Detailed failure matrix and completion evidence are in [contracts](contracts.md) and [implementation plan](../../phases/phase-43.23-domain-ownership.md). |

## Deliberately retained limits

The refactor does not implement durable local tool-result storage, cross-restart exactly-once effects, new mission grants, stop/resume, catalog downloads, billing redesign or legacy protocol retirement. Existing per-process legacy tool-result caching is not a crash-safe execution ledger. Compatibility adapters keep their current prompt/transcript lifetime and token semantics. These limitations are explicit retained behavior, not unanswered questions for the implementer.
