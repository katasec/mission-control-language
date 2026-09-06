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
| Read ownership and protocol | A scoped `RunHistoryService` owns verified state/list/detail/event reads. `ProjectMissionReadScope` composes that reader, `RunObservationService`, and a scope-owned `ProjectMissionToolRefusal`; refusal runs before the tail cursor advances. The transport-facing `ProjectMissionHistoryEndpointService` performs only live-session lookup and scope entry. Observation retains the existing one-queued-invalidation plus dirty-replacement bound. |
| Failure and retry boundaries | Focused Project, submission, history, session, tail, and refusal checks passed. Lost-response/same-command retry coverage remains on `MissionSubmissionService`; a deliberately changed second intent was refused with the existing “submission changed” result rather than overwriting the accepted journal. |
| Root review | Review removed stale duplicate Project/session/submission/history/content workflows from `ApplicationInteractionServices`, restored bounded invalidation, and required the final scoped History/Observation/refusal split. `git diff --check` passed. |
| Automated verification | `dotnet build src/ForgeMission.slnx --no-restore`: 0 warnings/0 errors. `dotnet test src/ForgeMission.slnx --no-build --no-restore`: passed. Focused final `RunHistoryServiceTests|ClientRuntimeEndpointsTests`: 3 passed; prior owner/history/session/refusal focused set: 76 passed. |
| AOT package | `make desktop-publish` produced Desktop, Application Host, and native Host. The only output was the existing macOS Homebrew OpenSSL/Brotli minimum-OS linker warnings. |
| Default product journey | Zero-argument published Desktop, with no positional/runtime URL override, started the Supervisor-owned bridge, Application Host `127.0.0.1:64929`, and native Host. Dedicated Project `9e09096f-59de-404a-9dea-7554dc4ff9fd` (schema 3) selected Janus, created command `d61f6927-a279-459c-8b16-dde4480dc7d2` and run `835dedd8-8c66-56d1-bf75-11727c74420a`, then selected Naive. After reload/open, the UI showed persisted Naive selection and the completed Janus run: 5 expert turns, 0 tool calls; trace events 1–15 contained `Return exactly PASS.` Project Explorer opened the dedicated `mcl.lock` text asset. |
| UI and lifecycle | The existing Forge Workbench renderer showed the unchanged Missions, trace, Explorer, persisted selection/history, and text-document states. Forced native Host exit removed Desktop `76985`, native Host `76986`, Application Host `76991`, and owned bridge `76987`. A separate normal window close removed Desktop `80756`, native Host `80757`, Application Host `80778`, and owned bridge `80758`. |
