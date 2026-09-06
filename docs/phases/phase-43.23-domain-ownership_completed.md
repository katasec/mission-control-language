# Phase 43.23 — completed design evidence

## Design finalization — 2026-09-06

**Design documentation complete. No implementation task is complete.** Active work and assignment: [43.23](phase-43.23-domain-ownership.md). Fixed ownership/contracts remain in the linked design documents because future tasks depend on them.

| Check | Observation |
|---|---|
| Baseline | `git fetch origin` and merged-PR query confirmed reconstruction PR #99 at `ef2e636cd37db6702364745c337d3156674541c6`. Operator reported reconstruction finished; this review did not rerun its rollout/visual checks. |
| Scope | Fourteen numbered concern rows are present exactly once, in order 1–14; each inventory file points to the finalized disposition. |
| Source evidence | Pinned MCL, DeepSeek, OpenHands and OpenCode source paths checked against their inspected local checkouts; bibliography records all revisions and limits. |
| Contract review | Preserved manifest version, journal identities, numeric DTO enums, separate conversation string-enum JSON context, route/errors, cloud-mode selection, default mission labels and readiness marker. Public ApplicationApi and Bob boundaries explicitly define every newly introduced response/exception type by signature or existing source reference. |
| Failure review | Named owners/results/recovery and focused verification for Project transactions, lost acceptance, foreign history, replay, local policy, confirmation, lifecycle races and process cleanup. Legacy non-durable result delivery is explicitly retained, not claimed fixed. |
| Architecture/security | Type-1 ownership, stores and credential roles recorded; no new remote store access or Project mission tool authority. Same-process isolation limit stated. Supervisor/native Host boundary preserved. |
| Documentation checks | Relative link/source target validation, fourteen-row coverage check and `git diff --check` passed. Hub remains one row per top-level phase; active ownership work removed from backlog. |
| Runtime acceptance | N/A — Markdown-only change. No build, tests, model runs or deployment performed for this design task. Implementation must provide the separate acceptance evidence in the active spoke. |

Review corrections incorporated before handoff: distinguish Application hosting from Bob; keep internal state behind a typed public facade; retain the two different event JSON enum settings; preserve legacy mode/default/error behavior; keep zero-authority refusal before tail cursor advance; document actual legacy result-delivery limits; use the merged reconstruction baseline without inventing another prerequisite.

## Amendment — 2026-09-06

The transport contract is now explicitly Type 1: shared action/event vocabulary, request/response fields, wire enum values, error semantics and identity/retry meaning are expensive product commitments. The Application.Transport assembly rename remains Type 2.

The proposed generic `ApplicationApi.InvokeAsync<TRequest, TResponse>` dispatcher was removed. ApplicationComposition retains Create and DisposeAsync as the composition/lifetime owner; Application Host calls public typed owner interfaces over Transport DTOs. This preserves compile-time request/response pairing and keeps internal domain/session types non-public.

## Task 1 — library and Host boundaries (2026-09-06)

**Complete and merged in PR #103, commit `9d663ff`.** The existing combined Client Runtime executable was separated into `ForgeMission.Application`, Bob (`ForgeMission.ClientRuntime`), `ForgeMission.Application.Host`, `ForgeMission.Application.Transport`, `ForgeMission.Presentation`, and `ForgeMission.Application.TransportProbe`. The exact action/event contract, numeric DTO enums, route/error behavior, readiness marker, manifest schema, Host/Worker ownership, and Desktop Supervisor/native Host boundary remain compatible.

| Check | Observation |
|---|---|
| Typed ownership | Application Host routes inject only named typed owners, including the approved `IApplicationSessionService.ReplaceAsync` seam for the existing session-setup route. `ApplicationHostRouteBoundaryTests` rejects a generic dispatcher. |
| Bob boundary and lifecycle | `ClientExecutionBoundaryTests` proves Bob has its sole Core project reference and no Application/Transport/conversation/HTTP/UI/provider source dependency. `ClientExecutionSessionTests` proves closed admission and admitted-dispatch cancellation/drain; `ApplicationSessionServiceTests` proves replacement-versus-disposal and concurrent disposal joins. |
| Automated verification | `dotnet build src/ForgeMission.slnx --no-restore`: 0 warnings/0 errors. `dotnet test src/ForgeMission.slnx --no-build --no-restore`: Worker 60, Conversation Host 153, Runner 5, Rooms 97, ForgeMission.Tests 596 (5 intentional skips), all passing. After final review correction: affected Test project build 0 warnings/0 errors and 31 affected tests passed. |
| AOT package | `make desktop-publish` passed. `dist/forge-desktop` contains Desktop, Application Host, native Host, Photino asset, index and WASM assets; obsolete Presentation runtimeconfig is absent. The only output was the existing macOS Homebrew OpenSSL/Brotli minimum-OS linker warnings. |
| Default artifact and dependencies | Zero-argument `dist/forge-desktop/ForgeMission.Desktop`, with no positional or runtime URL override, started the Supervisor-owned Kind bridge, Application Host on OS-assigned loopback, and native Host. The child reported the intentionally retained `FORGE_CLIENT_RUNTIME_URL=` marker; the observed local route served HTTP 200. |
| Default product journey | In dedicated Project `46ccc3e6-ee5c-41c3-938d-dcfe95e5f525` (manifest schema 3), Janus then Naive were selected. Command `bd393367-cd8c-40bc-96e4-a029f0d22595` created durable run `3783f6f7-79a6-588a-a2a8-c064ccbd249f`; the reopened product surface displayed Completed, one expert turn, zero tool calls. The rendered trace contained events 1–4, user text `PASS`, and Completed. Project Explorer opened the dedicated `mcl.lock` text asset. |
| Visual regression | Browser inspection of the default Application Host presentation showed the existing Workbench renderer and tokenized Forge theme with Explorer, Missions, Settings, completed run, trace, and document states; no Task 1 visual change or regression was observed. The native interactive window was separately confirmed to remain open. |
| Lifecycle | Normal native-window close removed Desktop `29173`, native Host `29174`, Application Host `29179`, and owned Kind bridge `29175`. Forced unexpected native Host exit removed Desktop `32309`, native Host `32310`, Application Host `32318`, and owned bridge `32311`. |
| Controlled tests | Fake HTTP/temp-profile/out-of-process, policy, stale-session, malformed/foreign-history, refusal, confirmation, readiness, and race tests are controlled evidence only; they do not replace the preceding default journey. |

The deliberately retained limits remain unchanged: no durable local tool-result ledger, cross-restart exactly-once local effects, new mission grant, stop/resume, catalog, billing redesign, or legacy protocol retirement.

## Task 2 — separate Application owners (2026-09-06)

**Complete and verified.** `ProjectService` is the sole Project/manifest/journal-write owner; `MissionCatalog`, `MissionSubmissionService`, and `ProjectContentService` now carry their named application responsibilities. `ApplicationSessionService` owns attachment admission/replacement/disposal. The former combined interaction implementation retains only conversation, direct capability, and confirmation behavior.

| Check | Observation |
|---|---|
| Read ownership and protocol | **Task 2 state:** a scoped `RunHistoryService` owned verified state/list/detail/event reads, while `ProjectMissionReadScope` composed it with `RunObservationService` and a scope-owned `ProjectMissionToolRefusal`; refusal ran before the tail cursor advanced. Task 4 finalized the boundary as host-facing `RunHistoryService` plus scoped `ProjectMissionHistoryReader`, removing the transitional endpoint wrapper. Observation retains the existing one-queued-invalidation plus dirty-replacement bound. |
| Failure and retry boundaries | Focused Project, submission, history, session, tail, and refusal checks passed. Lost-response/same-command retry coverage remains on `MissionSubmissionService`; a deliberately changed second intent was refused with the existing “submission changed” result rather than overwriting the accepted journal. |
| Root review | Review removed stale duplicate Project/session/submission/history/content workflows from `ApplicationInteractionServices`, restored bounded invalidation, and required the final scoped History/Observation/refusal split. `git diff --check` passed. |
| Automated verification | `dotnet build src/ForgeMission.slnx --no-restore`: 0 warnings/0 errors. `dotnet test src/ForgeMission.slnx --no-build --no-restore`: passed. Focused final `RunHistoryServiceTests|ClientRuntimeEndpointsTests`: 3 passed; prior owner/history/session/refusal focused set: 76 passed. |
| AOT package | `make desktop-publish` produced Desktop, Application Host, and native Host. The only output was the existing macOS Homebrew OpenSSL/Brotli minimum-OS linker warnings. |
| Default product journey | Zero-argument published Desktop, with no positional/runtime URL override, started the Supervisor-owned bridge, Application Host `127.0.0.1:64929`, and native Host. Dedicated Project `9e09096f-59de-404a-9dea-7554dc4ff9fd` (schema 3) selected Janus, created command `d61f6927-a279-459c-8b16-dde4480dc7d2` and run `835dedd8-8c66-56d1-bf75-11727c74420a`, then selected Naive. After reload/open, the UI showed persisted Naive selection and the completed Janus run: 5 expert turns, 0 tool calls; trace events 1–15 contained `Return exactly PASS.` Project Explorer opened the dedicated `mcl.lock` text asset. |
| UI and lifecycle | The existing Forge Workbench renderer showed the unchanged Missions, trace, Explorer, persisted selection/history, and text-document states. Forced native Host exit removed Desktop `76985`, native Host `76986`, Application Host `76991`, and owned bridge `76987`. A separate normal window close removed Desktop `80756`, native Host `80757`, Application Host `80778`, and owned bridge `80758`. |

## Task 3 — compatibility protocol and delivery (2026-09-06)

**Complete and verified.** `ConversationService` now owns prompt routing and selected-session lifetime; `LegacyJanusToolDelivery` owns the Janus-specific tool protocol. `LegacyMissionProtocolClient` and `LegacyCloudMissionProtocolClient` are the named per-prompt compatibility adapters. `CapabilityActionService` and `InteractionService` replace the final transitional aggregate without changing the typed host boundary.

| Check | Observation |
|---|---|
| Protocol and authority | Janus participant/name/tool validation, deterministic result IDs, and the in-memory result cache are isolated in `LegacyJanusToolDelivery`; Bob remains the sole policy/confirmation/execution decision point. Project Mission refusal remains in the separate Application protocol path and has no capability dependency. Bob names no Janus participant/protocol loop. |
| Compatibility behavior | `ConversationService` preserves start-versus-follow-up identity and serialized replacement/disposal. Local and cloud adapters retain their existing transcript/tool continuation behavior. Cloud client tokens remain one token per logical prompt, reused for continuations and refreshed for the next prompt. Host, Worker, Billing, and API ownership remain unchanged. |
| Automated verification | `dotnet build src/ForgeMission.slnx --no-restore`: 0 warnings/0 errors. `dotnet test src/ForgeMission.slnx --no-build --no-restore`: passed. Focused compatibility/lifecycle/boundary tests: 31 passed; real transport/direct-dispatch tests: 19 passed. `git diff --check` passed. |
| AOT package | `make desktop-publish` passed. The only linker output was the pre-existing macOS Homebrew OpenSSL/Brotli minimum-OS warnings. |
| Default product journey | Zero-argument published Desktop, with no positional/runtime URL override, started the normal owned bridge, Application Host `127.0.0.1:49703`, and native Host. Dedicated Project `97992919-0a59-4271-8b8b-403e74db5d84` (schema 3) used Naive after the default Janus selection; command `a2095ec0-e4d8-458f-ba96-f7456006c326` created run `31b6b2f1-c103-5b22-8123-df7652ca40c3`. The UI showed Completed, one expert turn, zero tools, and trace events 1–4 with user text `TASK3-PASS`. Reload/open preserved selection and history; Project Explorer opened the dedicated `mcl.lock` asset. |
| UI and lifecycle | Existing Mission, trace, Explorer, persisted run/history, and document states rendered without a Task 3 visual or interaction change. Forced native Host exit removed Desktop `90229`, native Host `90249`, Application Host `90251`, and bridge `90250`. A separate normal window close removed Desktop `91943`, native Host `91944`, Application Host `91951`, and bridge `91945`. |

## Task 4 — ownership acceptance and documentation (2026-09-06)

**Complete and verified.** The final review removed the transitional history endpoint wrapper. The
typed host boundary is now `RunHistoryService`; its explicitly bounded
`ProjectMissionHistoryReader` remains inside the existing read scope so observation and replacement
lifetime do not become a second session store. Current architecture, default-path documentation,
diagnostics and tests use Application/Application Host terminology. The deliberate
`FORGE_CLIENT_RUNTIME_URL=` readiness wire marker and named legacy protocol clients remain.

| # | Disposition | Current source or preserved authority | Evidence |
|---|---|---|---|
| 1 | Project management | `Application/Projects/ProjectService` and its private manifest adapters | Project/transport tests; default Project create/open |
| 2 | Mission selection | `ProjectService.SelectMissionAsync` plus `Missions/MissionCatalog` | Naive selection persisted through reopen |
| 3 | Submission/recovery | `Missions/MissionSubmissionService` and Host adapter | existing retry/journal focused coverage; full suite |
| 4 | Conversation management | `Conversations/ConversationService`, `Adapters/Conversations/ConversationHostClient` | conversation lifecycle focused coverage |
| 5 | History/observation | host-bound `Runs/RunHistoryService`; scoped `ProjectMissionHistoryReader` and `RunObservationService` | 42 focused architecture/history/transport/Desktop checks |
| 6 | Workbench content | `Projects/ProjectContentService` | Project Explorer opened the dedicated `mcl.lock` asset |
| 7 | Compatibility protocol | named `LegacyMissionProtocolClient`, `LegacyCloudMissionProtocolClient`, `LegacyJanusToolDelivery` | compatibility and direct-dispatch coverage remains in full suite |
| 8 | Application sessions | `Sessions/ApplicationSessionService` | session/replacement focused coverage |
| 9 | Presentation hosting | `Application.Host/Program.cs`, `Application.Transport`, `Presentation` | published static application and existing workbench states rendered |
| 10 | Supervision | Desktop `ApplicationHostProcess` and existing Orchestration | forced native Host and normal window-close cleanup |
| 11 | Durability/reasoning | existing Conversation Host and Worker | no source relocation; full suite preserved their boundary |
| 12 | Platform access/accounting | existing platform/API/Billing owners and cloud adapter token path | no new credential/grant source; full suite |
| 13 | Janus/generic infrastructure | Worker Janus mapping; `LegacyJanusToolDelivery`; Application refusal path; Bob policy | Bob boundary and hostile-tool/direct-dispatch coverage |
| 14 | Shared API/ownership | `Application.Transport`, typed Application owners, `ApplicationComposition` | architecture route/compatibility checks reject the removed wrapper/generic boundary |

| Check | Observation |
|---|---|
| Cleanup review | Obsolete Application-facing Client Runtime diagnostics/names moved to Application or Application Host; clear Application and Host test fixtures moved accordingly. Genuine Bob package references and capability tests remain. `git diff --check` passed. |
| Automated verification | `dotnet build src/ForgeMission.slnx --no-restore`: 0 warnings/0 errors. Focused architecture/history/transport/Desktop coverage: 42 passed. `dotnet test src/ForgeMission.slnx --no-build --no-restore`: passed (the existing integration skips remain intentional). |
| AOT package | `make desktop-publish` passed. Published `ForgeMission.Desktop` and `ForgeMission.Application.Host` were produced. The only linker output was the existing macOS Homebrew OpenSSL/Brotli minimum-OS warnings. |
| Default product journey | Zero-argument published Desktop, without positional/runtime URL overrides, created dedicated Project `ce7870e2-5c30-4529-abca-dc7052ed9330` (schema 3) at `/Users/ameerdeen/Forge/Projects/task-4-final-ownership-acceptance`. Default Janus was switched to Naive; command `8762ac1d-2d14-456b-8ed1-a9b50269a435` created run `1f4c2cdf-c18e-57bf-b2fb-1041c121c0a1`, completed as one expert turn and zero tool calls. Its trace showed events 1–4, user text `TASK4-PASS`, and Completed. Reload then explicit open preserved Naive selection and history; Project Explorer displayed the dedicated `mcl.lock` asset. |
| Visual regression | Existing Forge Workbench navigation, Missions, completed run, trace, Explorer, persisted history/selection and text-asset states rendered with the existing theme. No Task 4 visual or interaction regression was observed. |
| Lifecycle | Before forced exit: Desktop `98388`, native Host `98392`, Application Host `98395`, owned bridge `98394`. Killing native Host removed all four after 12 seconds. A separate zero-argument launch followed by normal window close left no Desktop, native Host, Application Host, or owned bridge process. |
