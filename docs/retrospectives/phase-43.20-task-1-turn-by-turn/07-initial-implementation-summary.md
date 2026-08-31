# Turn 07 — Initial implementation summary

## 1. Codex prompt

````text
+------------------------------------------------------------------+
| IMPLEMENTATION APPROVAL — Phase 43.20, Task 1                    |
+------------------------------------------------------------------+

Your v5 implementation plan is approved. You may now implement it exactly
as planned.

Work on the existing branch:
  codex/phase-43.20-project-manifest

It already contains the active design-document updates. Do not work directly
on main and do not discard those changes.

Non-negotiables:
- Keep ProjectDraftRequest, Project create/open, and session replacement
  surface-neutral Client Runtime contracts.
- Desktop/Home.razor renders and invokes contracts only; it owns no
  derivation, filesystem rule, or session-root authorization.
- Enforce SessionSetupRequest replacement-only behavior in Client Runtime.
- Preserve Native AOT safety with source-generated JSON.
- Do not add a TUI, database, Host/Supervisor change, or hosted service.

Before Presentation work, make the surface-free real-process transport
contract tests pass for draft, create, open, typed failures, and rejected
session replacement.

When finished, reply with an IMPLEMENTATION SUMMARY using the repository
template: files changed, named verification results, Done-when evidence,
deviations, open follow-ups, and both Desktop Quality Gate and
Presentation-surface parity gate results. Do not claim completion until the
full required suite and AOT publish verification pass.
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| IMPLEMENTATION SUMMARY - Phase 43.20 Task 1 (Claude -> Codex)           |
| Project home and local manifest. PR #78, awaiting your review.          |
| Branch codex/phase-43.20-project-manifest: 1 commit, pushed, clean.     |
| NOT merged - per the standing loop, merge is yours to approve.          |
|                                                                         |
| --- GATES ------------------------------------------------------------  |
|                                                                         |
| PRESENTATION-SURFACE PARITY GATE: PASS                                  |
|   Could a TUI invoke this same product action with the same             |
|   authorization, outcome, and failure semantics? Yes, for all four:     |
|     draft   ProjectDraftRequest -> ProjectDraftResponse                 |
|     create  ProjectCreateRequest -> ProjectOperationResponse            |
|     open    ProjectOpenRequest  -> ProjectOperationResponse             |
|     replace SessionSetupRequest, guarded in Client Runtime              |
|   ProjectTransportContractTests is itself a second, non-Desktop         |
|   surface: it drives the real Client Runtime process through the        |
|   production IClientRuntimeChannel and references no Blazor, bunit,     |
|   Photino, Desktop, or Host type. ForgeMission.ClientRuntime.           |
|   TransportProbe was migrated to the same create contract, so no        |
|   out-of-process client can establish a root any other way.             |
|                                                                         |
| DESKTOP DESIGN AND IMPLEMENTATION QUALITY GATE: PASS                    |
|   Behaviour  boot creates no directory, session, subscription, or tool  |
|              authority; one goal yields a named Project whose home is   |
|              the sole execution root.                                   |
|   Owner      Client Runtime owns the manifest, every filesystem touch,  |
|              every Project rule, and the session-replacement rule.      |
|              Presentation renders and invokes.                          |
|   Adapter    no IDesktopHost, Photino, Supervisor, or native callback   |
|              is involved; three routes were added to the runtime that   |
|              already owns local execution.                              |
|   Boundary   no Host API, process-lifetime, or credential change;       |
|              replacing Photino changes nothing in this diff.            |
|   Proof      the verification table below, including a packaged-app     |
|              browser observation.                                       |
|                                                                         |
| --- FILES CHANGED ----------------------------------------------------  |
|                                                                         |
| ADDED (ForgeMission.ClientRuntime)                                      |
|   Services/ProjectManifest.cs            complete v1 record graph       |
|   Services/ProjectManifestJsonContext.cs source-generated, AOT-safe     |
|   Services/ProjectStore.cs               sole owner of derivation,      |
|       collision, validation, filesystem work, typed failures            |
|                                                                         |
| CHANGED                                                                 |
|   ClientRuntime.Transport/ClientRuntimeContracts.cs                     |
|       draft/create/open DTOs, ProjectOperationResponse/Error,           |
|       SessionSetupRequest.ReplacesSessionId now required                |
|   ClientRuntime.Transport/ClientRuntimeJsonContext.cs                   |
|   ClientRuntime.Transport/HttpClientRuntimeChannel.cs                   |
|   ClientRuntime/Transport/ClientRuntimeEndpoints.cs                     |
|       transport/project/{draft,create,open}; session/setup enforces     |
|       the replacement rule                                              |
|   ClientRuntime/Transport/ClientRuntimeSessionStore.cs                  |
|       CreateForProject / ReplaceAsync split                             |
|   ClientRuntime/Program.cs, Services/WorkspaceState.cs                  |
|   ClientRuntime.Presentation/Pages/Home.razor                           |
|   ClientRuntime.TransportProbe/Program.cs                               |
|                                                                         |
| REMOVED                                                                 |
|   Services/DefaultWorkspace.cs and its tests                            |
|   /transport/session/default, DefaultWorkspaceSessionRequest/Response   |
|   Workspace:InitialRoot read + dead scoped WorkspaceState registration  |
|   and its unused initialRoot constructor parameter                      |
|                                                                         |
| TESTS                                                                   |
|   ClientRuntime/ProjectStoreTests.cs            24 new                  |
|   ClientRuntime/ProjectTransportContractTests.cs 14 new (surface-free)  |
|   ClientRuntime/ClientRuntimeHostProcess.cs      extracted, one owner   |
|   ClientRuntimeSessionStoreTests, ConversationSessionSlotTests,         |
|   WorkspaceStateTests, HomeSessionOperationTests  updated               |
|   Architecture/ClientRuntimePresentationBoundaryTests.cs                |
|       + Presentation may use no filesystem API                          |
|                                                                         |
| --- NAMED VERIFICATION RESULTS ---------------------------------------  |
|                                                                         |
|   dotnet build src/ForgeMission.slnx                                    |
|       Build succeeded. 0 Warning(s), 0 Error(s).                        |
|   dotnet test src/ForgeMission.slnx                                     |
|       834 passed, 11 skipped, 0 failed                                  |
|       (Tests 551, ConversationHost 139, Rooms 97, Worker 42, Runner 5)  |
|   make desktop-publish (Native AOT, osx-arm64)                          |
|       published clean; grep -icE "IL[0-9]{4}" over the log = 0          |
|       (only pre-existing ld macOS-version warnings)                     |
|   make install (CLI AOT publish)                                        |
|       Installed: ~/.local/bin/forge                                     |
|   Packaged Client Runtime + empty temp profile + browser                |
|       after loading Desktop the profile was still completely empty:     |
|       no directory, no session, no event subscription                   |
|   Live create of "Todos API"                                            |
|       one home at <profile>/Forge/Projects/todos-api with the v1        |
|       manifest; the draft step alone created nothing                    |
|   Live reopen of that home                                              |
|       same title and home, used as the sole execution root, no second   |
|       directory                                                         |
|   Live second create of "Todos API"                                     |
|       todos-api-2 with a new projectId; the first manifest's projectId  |
|       unchanged                                                         |
|   ~/Forge does not exist on this machine                                |
|       the test/child-process HOME redirect held; the developer's real   |
|       home was never written to                                         |
|                                                                         |
| --- DONE-WHEN EVIDENCE -----------------------------------------------  |
|                                                                         |
|   empty profile creates neither directory nor session merely by         |
|   opening Desktop                                                       |
|       -> live packaged-app observation above; bunit                     |
|          Boot_IssuesNoRequestAndOpensNoSubscription                     |
|   creating "Todos API" produces one deterministic Project home and      |
|   manifest with collision handling                                      |
|       -> live todos-api then todos-api-2; ProjectStoreTests collision   |
|          and suffix cases; contract test over the real process          |
|   reopening uses that Project home as the sole local execution root     |
|       -> live reopen; Open_ACreatedProject_RestoresTheSameIdentityAnd   |
|          Home; ACreatedProjectsSession_ExecutesACapabilityInsideItsOwn  |
|          Home                                                           |
|   numbered-workspace tests replaced                                     |
|       -> DefaultWorkspaceTests deleted, ProjectStoreTests added         |
|   create/open transport contract proves the same results independent    |
|   of a Desktop surface                                                  |
|       -> ProjectTransportContractTests, 14 passing                      |
|   Client Runtime/Presentation boundary tests + normal suite pass        |
|       -> 4 boundary tests; full suite above                             |
|                                                                         |
| --- DEVIATIONS FROM THE APPROVED PLAN --------------------------------  |
|                                                                         |
| 1. Create owns the final home, including a confirmed draft's.           |
|    Found by live browser verification, not by a test: the launcher      |
|    sends the drafted location straight back, so an ordinary create was  |
|    taking the "explicit home" branch and REFUSING a second Todos API    |
|    instead of suffixing - the collision path was unreachable from any   |
|    surface that shows a draft before confirming it. The rule now lives  |
|    in ProjectStore.Create: a home directly inside <user-profile>/Forge/ |
|    Projects is Forge-managed and takes the next free -2/-3, while a     |
|    home outside that root is a directory the person named themselves    |
|    and is used exactly (a collision there is InvalidHome, never a       |
|    silent relocation). This is what the spoke's "create recomputes the  |
|    draft and performs the authoritative collision-safe write" requires; |
|    the plan's flat "an explicit home is exact" was too coarse.          |
|                                                                         |
| 2. GoalRequired prefills from the response instead of issuing a second  |
|    draft call. The proposal already carries a runtime-derived home and  |
|    title, so the extra round-trip added nothing. No derivation moved    |
|    into Presentation.                                                   |
|                                                                         |
| 3. ProjectStore raises ProjectOperationException internally and         |
|    ClientRuntimeEndpoints maps it to the typed ProjectOperationError in |
|    exactly one place. The wire contract is unchanged; this keeps the    |
|    store's happy path linear. Unexpected faults (permissions, full      |
|    disk) are deliberately not caught and fail the transport.            |
|                                                                         |
| 4. Manifest fields are exactly the locked table - no createdUtc was     |
|    invented, as flagged in the plan.                                    |
|                                                                         |
| 5. CreateForProject is synchronous (nothing async happens in creation); |
|    only ReplaceAsync awaits, since it disposes the outgoing session.    |
|                                                                         |
| --- ASSUMPTIONS THAT SURVIVED REVIEW ---------------------------------  |
|                                                                         |
|   A rejected SessionSetupRequest is a 400 with a named reason, not a    |
|   typed domain outcome - it is a misuse/stale-race guard no correct     |
|   surface trips. Say the word and it becomes a typed response.          |
|   ProjectSummary carries projectId, title, goal, home only.             |
|   Transport DTO enums keep the existing numeric wire default; only the  |
|   on-disk manifest uses string enums.                                   |
|                                                                         |
| --- OPEN FOLLOW-UPS --------------------------------------------------  |
|                                                                         |
| 1. No recents index or auto-resume exists, by design. A later           |
|    recent-project experience needs its own bounded design.              |
| 2. Task 2's conversation-ID write-back is the first REWRITE of an       |
|    existing manifest. Task 1 only writes at creation, which is why the  |
|    full v1 graph is typed and round-trip-tested now.                    |
| 3. Test profile isolation is a property of the child process            |
|    (HOME/USERPROFILE redirect in ClientRuntimeHostProcess), not a       |
|    shipping config knob. Keep it that way.                              |
| 4. ProjectTransportContractTests starts one Client Runtime process per  |
|    test (14 total, ~5s). If that becomes a drag as later tasks add      |
|    cases, a shared fixture is the fix, at the cost of cross-test state. |
|                                                                         |
| PR #78 is open and mergeable with no required checks configured.        |
| Awaiting your approval before merging to main.                          |
+-------------------------------------------------------------------------+
````

## 3. Fault found

Implementation reached PR #78 before a binding visual artifact and internal visual comparison

## Requested

Implement Task 1 from the approved contract plan and report the result.

## Better prompt

“Do not write source yet: first create and obtain approval for state-by-state visual references and

## Prevention

Do not start a presentation task until its state references are approved. A green test suite proves
