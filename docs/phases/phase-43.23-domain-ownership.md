# Phase 43.23 — domain ownership

**2026-09-06: design finalized; implementation not started.** Next: Claude prepares an implementation plan for Task 1, then Codex reviews it under the normal [handoff protocol](../design/claude-codex-workflow.md). The reconstruction's Codex-only exception does not carry into this new workstream.

Design verification — complete; see [design evidence](phase-43.23-domain-ownership_completed.md#design-finalization--2026-09-06).

## Design and baseline

| Read | Authority |
|---|---|
| [Ownership index](../retrospectives/phase-43-domain-ownership/README.md) | Baseline/provenance and the original fourteen concerns |
| [End state](../retrospectives/phase-43-domain-ownership/end-state.md) | Fixed owners, library/process topology, security and engineering review |
| [Contracts](../retrospectives/phase-43-domain-ownership/contracts.md) | Exact request/response sources, new Bob boundary, state/lifecycle/failure contracts |
| [Reference evidence](../retrospectives/phase-43-domain-ownership/references.md) | Pinned prior art and limits of adoption |

Base: reconstruction PR #99, `ef2e636cd37db6702364745c337d3156674541c6`, or its current-main descendant. Operator reports reconstruction finished and merged; source/merge verified. No unresolved reconstruction dependency is imposed by this plan. Earlier reconstruction acceptance text is historical evidence to reconcile, not authority to skip this refactor's own checks.

Outcome: all Project and conversation application behavior has a named Application owner; ClientRuntime is the local capability library; a thin Application Host exposes the same product actions. Desktop behavior, manifests, network routes and remote state ownership remain compatible. This is an ownership refactor, not a feature or UI redesign.

## Fixed migration order

Each task is an independently reviewed PR from current main. Obtain an approved implementation plan before code; complete the task's checks and default path before merge, then update the next task's source pointers. Completed evidence moves to `phase-43.23-domain-ownership_completed.md`; leave a one-line status here. No task is complete merely because files moved or a build passes.

| Task | Concrete change | Required result / Done when | State |
|---|---|---|---|
| 1 — establish library and host boundaries | Create Application library; rename old executable to Application.Host; create Bob library at ClientRuntime; move existing Project/mission/conversation/read services into Application without changing their algorithms. Replace WorkspaceState with the defined ClientExecutionSession and adapt existing callers to its declarations/dispatcher. Move confirmation bridge into Application, endpoint/ready/event hub into Host. Rename Transport, Presentation and Probe as specified; update solution, tests, Supervisor child adapter and build/publish scripts together. | Compiling/AOT-published topology matches end state; Bob has no HTTP/Project/conversation/UI dependency or public provider access. Existing shared actions and all numeric/wire formats remain compatible. Dispatch/shutdown/confirmation negative tests pass. Published Desktop launches and performs Project create/open, selection, run/history/document and clean exit through default routes. | Pending |
| 2 — separate Application owners | Rename ProjectStore to ProjectService and ProjectMissions to MissionCatalog; move selection out of workbench; rename ProjectMissionApplication and content remainder. Split ProjectMissionReadSession into bounded History/Observation plus its small scoped composition object; retain required protocol refusal before cursor advance. Name ApplicationSessionService and move its remaining application admission/lookup duties out of endpoint helpers. | Every Project/journal write has one owner; submission never holds a file lease across HTTP; history/observation have no Bob dependency. Table rows 1–6 and 8–9 of the end state match source. Required failure cases and same-command retry/default reopen journey pass, with unchanged UI. | Pending; after Task 1 |
| 3 — isolate compatibility protocol and delivery | Split ConversationRuntimeSession into ConversationService plus LegacyJanusToolDelivery; move/rename MissionRuntimeSession and CloudMissionRuntimeSession into named compatibility adapters. Keep participant validation at the Janus adapter, policy at Bob and cloud token behavior in cloud adapter. Preserve Host/Worker/Billing boundaries; audit dependencies and exact exceptions for concerns 7 and 10–14. | No Janus participant or protocol loop inside Bob; no second durable owner or new grant. Existing legacy prompt/delivery/token tests pass; cancellation/replacement and direct-dispatch tests prove containment. Project Mission hostile-tool proof still shows zero provider calls. Default Project journey still passes. | Pending; after Task 2 |
| 4 — complete ownership acceptance and documentation | Audit all fourteen dispositions and remove now-obsolete compatibility-internal names/wrappers/imports, retaining the deliberately unchanged readiness wire marker and legacy protocol clients. Update canonical architecture/current default-path source locations, user-neutral diagnostics and links. | All fourteen rows have changed/preserved source and evidence pointers. Required whole-solution tests, AOT publish, boundary checks, default journey, native-host failure/cleanup and regression visual observations pass. No new feature/legacy API is introduced. Codex reviews completion against this spoke. | Pending; after Task 3 |

Task 1's use of existing combined classes inside Application is a bounded structural migration, not final ownership. They may remain there only until Tasks 2–3 split the named concerns. No combined class remains inside Bob. Removal condition is Task 3 acceptance; intermediate PRs remain behavior-compatible and independently usable. This is an internal organization allowance, not a security/tier exception or a second enabled implementation mode.

## Source and test map

| Source today | Destination / work |
|---|---|
| `src/ForgeMission.ClientRuntime/Program.cs`, ReadyResponse, Transport/ClientRuntimeEndpoints, ClientRuntimeEventHub | Application.Host, grouped endpoints named for the owning application domain |
| `Services/ProjectStore`, `ProjectManifest*`, `ProjectMissions`, `ProjectWorkbenchService` | Application/Projects persistence/domain/content; Application/Missions/MissionCatalog |
| `Services/ProjectMissionApplication` | Application/Missions/MissionSubmissionService |
| `Services/ProjectMissionReadSession`, `ProjectRunReadState` | Application/Runs, with existing presentation-side use of ProjectRunReadState retained where applicable |
| `Transport/ClientRuntimeSessionStore`, PendingConfirmationHandler | Application/Sessions and Application/Interaction |
| `Services/ConversationHostClient`, ConversationTailReader, ProjectMissionToolRefusal | Application/Adapters/Conversations; separate protocol hook composed by the Project read scope |
| `Services/ConversationRuntimeSession` | Application/Conversations/ConversationService and Application/Adapters/Janus/LegacyJanusToolDelivery |
| `Services/MissionRuntimeSession`, CloudMissionRuntimeSession | Application/Adapters/Missions compatibility clients |
| `Services/WorkspaceState` | ClientRuntime/ClientExecutionSession; private existing Core workspace/registry/dispatcher |
| ClientRuntime.Transport / Presentation / TransportProbe projects | Application.Transport / Presentation / Application.TransportProbe; preserve wire formats and UI assets |
| Desktop/ClientRuntimeProcess, Desktop boot callers, Makefile, solution/project references and tests | Coordinated ApplicationHostProcess and sibling-executable/publish changes; keep Supervisor/native Host boundary |

Reuse the current tests that exercise these owners; relocate/update their namespaces and source-path assertions rather than cloning test projects per service. Architectural dependency tests belong alongside `src/ForgeMission.Tests/Architecture/ClientRuntimePresentationBoundaryTests.cs` and `DesktopSupervisorHostBoundaryTests.cs`, renamed where appropriate. Test observable behavior at the shared channel/dispatcher, not only source-string absence. Task 1 introduces the fully specified ApplicationApi from contracts so Host can call internal owners without making their state types public; move existing endpoint delegation into it without adding business rules.

Native AOT remains mandatory. Every moved JSON context includes the same DTOs; no reflective serialization fallback, assembly scanning, dynamic service discovery or new warning suppression. Update static-asset publish cleanup to the renamed Presentation assembly. The shared broad Core reference is retained; no Core/CLI parser/tool/provider reorganization belongs in this task.

## Default-path and visual acceptance

All implementation tasks change runtime wiring and must satisfy [Default-Path Acceptance](../design/default-path-acceptance.md). The documentation-only design task records N/A; that exception does not transfer to code.

| Fact | Required acceptance observation |
|---|---|
| Published artifact | `dist/forge-desktop/ForgeMission.Desktop`, launched with zero arguments, built with `make desktop-publish`; its Application.Host sibling is the new internal child. |
| Absent overrides | No positional URL; no user overrides to MissionRuntime mode/base URL, FORGE_API_ENDPOINT or ConversationRuntime base URL. Supervisor's owned injection is expected. |
| Normal dependencies | Default cloud Mission endpoint `https://api.forge.katasec.com`; Conversation endpoint `http://127.0.0.1:18080/`, `/health`, normal Supervisor-owned Kind bridge when needed. Record actual dependency revision/provenance; deployment only through existing forge-infra workflow. |
| Local transport | OS-assigned `127.0.0.1:0`, unchanged `/ready`, `/transport/*`, static paths and `FORGE_CLIENT_RUNTIME_URL=` marker. Verify the Supervisor navigates the native Host to that observed URL. |
| Safe state | Dedicated disposable Project with benign supported mission instruction and a small text asset. Never mutate an unrelated Project for proof. |
| Product journey | Create/open Project, select Janus and Naive, submit an instruction, observe the durable run and exact trace, reopen and see the same history, open a text document, then close. Record stable Project/command/run IDs, manifest schema 3 and user-visible outcomes. |
| Failure behavior | Focused controlled lost-response retry, stale session, policy/refusal and malformed/foreign history tests from the contracts matrix. Label controlled inputs separately; they do not substitute for default journey. |
| Lifecycle | Observe child PIDs before/after normal window close and unexpected native Host exit; no owned Application Host, capability operation or owned runtime bridge/container remains. Do not stop shared dependencies the Supervisor did not start. |
| UI | Existing reconstruction renderer/reference remains binding. Record no visual/interaction regression in the same covered states and viewports; follow [Desktop Interaction Principles](../design/desktop-interaction-principles.md) and [UI Design System](../design/ui-design-system.md). No new token/theme values or visual acceptance design is introduced. |

Run focused tests while changing owners, then required `dotnet build src/ForgeMission.slnx`, `dotnet test src/ForgeMission.slnx` and AOT Desktop publish before each task closes. Do not repeat a passing check without a subsequent relevant change. New tests must prove the new library/lifetime boundary or an affected failure contract; do not write tests that merely mirror unchanged getters or record declarations.

If the normal dependency fails, report the observed failure and repair through its existing owner/workflow; do not substitute an override and claim acceptance. No new cloud deployment, credential grant, database migration or remote service rewrite is authorized by this plan. Necessary unrelated repairs require their own scoped plan, not expansion of the extraction.

## Design review and assignment

Security, engineering, desktop replacement and surface-parity review: **PASS for the specified design**, with required implementation observations in the contracts matrix and table above. Type definitions are either fully specified in contracts or linked to exact existing source; migration changes no public wire/storage schema. The design does not promise crash-safe legacy tool delivery or new Project tool grants. No unresolved architecture choice is delegated to implementation.

### First assignment — Task 1 only

Role: Claude implementer. Read AGENTS.md, docs/plan.md, this spoke, its four design links, Default-Path Acceptance, Security Architecture, Engineering Philosophy, Desktop Interaction Principles and UI Design System. Do not write code until Codex approves your implementation plan.

Prepare Task 1: establish the Application, Bob and Application Host compile-time boundaries while preserving the merged reconstruction behavior. Start from current main containing PR #99 on a fresh `codex/` branch when implementation is approved. Source/test scope is the map above; Tasks 2–4 define later work and are not permission to fold unrelated features into Task 1.

Scope card: the tangible outcome is the same normally launched Desktop backed by the newly separated local libraries/host. Task 1 is the smallest coherent packaging change because the old executable name is being repurposed for Bob and Supervisor/publish references must change atomically. No new process, endpoint contract, Project schema, tool grant or UI feature. The existing reconstruction is the starting product, not a task to rebuild.

Done when: Task 1 row and the default-path table above. Include a file-by-file move/reference plan, AOT/static-asset handling, each relevant positive/negative precondition from contracts, shared-action ownership, exact evidence paths and disposal-race tests. Begin with the five Desktop Quality Gate PASS/FAIL answers. Record UI disposition as existing-renderer reuse/no redesign and name the regression comparison. Return any factual source drift for review; do not invent a different architecture to work around it.

Next step: return an implementation plan only and wait for Codex approval. Completion summaries must state actual evidence and retain any failing default-path condition; source relocation alone is not task completion.
