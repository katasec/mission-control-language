# Turn 05 — Final plan v4

## 1. Codex prompt

````text
+------------------------------------------------------------------+
| FINAL PLAN REVISION REQUEST                                      |
+------------------------------------------------------------------+

Read the updated “Manifest v1 schema and launcher boundary” and Task 1
requirements in docs/phases/phase-43.20-project-workbench-mvp.md.

Revise the plan without writing code:

1. Replace separate create/open response shapes with the shared,
   surface-neutral ProjectOperationResponse:
   - Created / Opened -> ProjectSession
   - GoalRequired -> ProjectHomeProposal
   - Failed -> ProjectOperationError { code, message }

   Expected Project domain failures must be typed responses that every
   surface—including a future TUI—can render identically. Unexpected
   process/transport failures may still fail the transport normally.

2. Enforce SessionSetupRequest’s replacement-only rule in Client Runtime,
   not just in Home.razor:
   - ReplacesSessionId is mandatory.
   - It must identify the current session.
   - WorkspaceRoot must equal that session’s Project home.
   - Only project create/open may establish a first session/root.
   Add contract tests for rejected no-replacement and mismatched-root calls.

3. Correct the derivation test wording: an empty goal is always rejected.
   The “project” slug fallback applies only when a non-empty title
   normalizes to no usable slug characters.

Keep the real-process, surface-free transport contract test and the
Desktop-only bunit scope. Reply with the revised plan only, then wait for
explicit approval.
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| FINAL IMPLEMENTATION PLAN v4 - Phase 43.20 Task 1 (Claude -> Codex)     |
| Shared ProjectOperationResponse, runtime-enforced replacement rule,     |
| corrected derivation wording. No code written; no branch yet.           |
|                                                                         |
| --- WHAT CHANGED SINCE v3 -------------------------------------------   |
|                                                                         |
| A. One surface-neutral ProjectOperationResponse for create AND open,    |
|    carrying Created/Opened -> ProjectSession, GoalRequired ->           |
|    ProjectHomeProposal, Failed -> ProjectOperationError{code,message}.  |
| B. SessionSetupRequest's replacement-only rule now lives in the         |
|    Client Runtime endpoint/session store, not in Home.razor. Two        |
|    named store methods replace one permissive CreateAsync, so no code   |
|    path can establish a first root from an arbitrary folder.            |
| C. Derivation wording corrected: an empty goal is ALWAYS rejected;      |
|    the "project" slug fallback applies only when a non-empty title      |
|    normalizes to no usable slug characters.                             |
|                                                                         |
| --- DESKTOP QUALITY GATE (re-affirmed) ------------------------------   |
|                                                                         |
| Q1 Behaviour: boot creates no directory, session, subscription or       |
|    tool authority. One goal produces a named Project with a             |
|    deterministic home + complete v1 manifest; that home is the sole     |
|    local execution root.                                                |
| Q2 Owner: Client Runtime owns forge.project.json, every filesystem      |
|    touch, every Project rule, and now the session-replacement rule.     |
|    Presentation renders state and invokes named contracts. Supervisor   |
|    and Host own none of this.                                           |
| Q3 Adapter: verified by reading ClientRuntimeEndpoints.cs,              |
|    HttpClientRuntimeChannel.cs and Home.razor -                         |
|    /transport/session/default is referenced only by Home.razor, the     |
|    channel route table, the JSON context and one test fake. No          |
|    IDesktopHost/Photino/Supervisor/native callback involved.            |
| Q4 Replacement boundary: no Host API, callback, process-lifetime or     |
|    credential change. Two new routes on the runtime that already owns   |
|    local execution.                                                     |
| Q5 Proof: ProjectStore unit tests; full-shape manifest round-trip; a    |
|    surface-free transport contract test covering create, open,          |
|    GoalRequired, every typed failure code, and the two rejected         |
|    SessionSetupRequest shapes; bunit tests for zero-call boot, render   |
|    state and contract invocation; Presentation System.IO boundary       |
|    test; dotnet test + make install (AOT).                              |
| RESULT: PASS.                                                           |
|                                                                         |
| --- 1. SHARED ProjectOperationResponse ------------------------------   |
|                                                                         |
|   enum ProjectOperationOutcome                                          |
|       { Created, Opened, GoalRequired, Failed }                         |
|                                                                         |
|   ProjectOperationResponse(                                             |
|       ProjectOperationOutcome Outcome,                                  |
|       ProjectSession? Session,        // Created | Opened               |
|       ProjectHomeProposal? Proposal,  // GoalRequired                   |
|       ProjectOperationError? Error)   // Failed                         |
|                                                                         |
|   ProjectSession(SessionId, AvailableCapabilities, Project)             |
|   ProjectSummary(ProjectId, Title, Goal, Home)                          |
|   ProjectHomeProposal(HomePath, ProposedTitle)                          |
|   ProjectOperationError(ProjectOperationErrorCode Code, string Message) |
|                                                                         |
|   ProjectCreateRequest(Goal, Title?, HomePath?, Mission?, Runtime)      |
|       -> ProjectOperationResponse                                       |
|   ProjectOpenRequest(HomePath, Mission?, Runtime)                       |
|       -> ProjectOperationResponse                                       |
|   Routes: transport/project/create, transport/project/open.             |
|   Deleted: DefaultWorkspaceSessionRequest/Response and                  |
|   /transport/session/default.                                           |
|                                                                         |
|   ProjectOperationErrorCode covers exactly the expected domain          |
|   failures the spoke names:                                             |
|       InvalidGoal                 empty/whitespace goal                 |
|       InvalidHome                 malformed or non-absolute home        |
|       HomeNotFound                open target does not exist            |
|       InvalidManifest             malformed JSON / missing field        |
|       UnsupportedManifestVersion  schemaVersion newer than 1            |
|       InvalidPath                 asset/context path validation         |
|       CollisionAttemptsExhausted  bounded suffix search exhausted       |
|   Every one is a 200 response a TUI renders exactly as Desktop does.    |
|   Codes are a typed enum, not free strings, so a surface cannot         |
|   silently mis-handle one; the message is the human-readable detail.    |
|   Unexpected process/transport failures (host down, socket reset,       |
|   malformed wire payload) still fail the transport normally and are     |
|   not laundered into a domain code.                                     |
|                                                                         |
| --- 2. REPLACEMENT-ONLY RULE, ENFORCED IN CLIENT RUNTIME ------------   |
|                                                                         |
| ClientRuntimeSessionStore's single permissive CreateAsync is split      |
| into two named methods, so the rule is structural rather than a         |
| convention any caller must remember:                                    |
|                                                                         |
|   CreateForProjectAsync(projectHome, mission, runtime)                  |
|       the ONLY way a first session/root is established. Called only     |
|       by the project create/open endpoints, after ProjectStore has      |
|       produced/validated that home.                                     |
|   ReplaceAsync(replacesSessionId, workspaceRoot, mission, runtime)      |
|       the ONLY path behind /transport/session/setup. It requires        |
|       replacesSessionId, requires it to identify a live session in      |
|       the store, and requires workspaceRoot to equal that session's     |
|       existing root. Any violation is rejected; nothing is created.     |
|                                                                         |
| SessionSetupRequest.ReplacesSessionId becomes non-optional in the       |
| contract. A rejected call fails the transport with 400 and a named      |
| reason rather than a ProjectOperationError: it is a misuse/stale-race   |
| guard, not a Project domain outcome a surface renders as a normal       |
| state. Home already surfaces such a failure through its existing        |
| sessionError banner, so a stale mission switch after a replacement      |
| race shows a plain error instead of silently minting a fresh root.      |
| If you would rather this also be a typed domain response, say so -      |
| it is a one-line change, but I would rather fail loudly than add a      |
| renderable outcome for a call no correct surface should ever make.      |
|                                                                         |
| --- 3. DERIVATION (Client Runtime only), CORRECTED ------------------   |
|                                                                         |
|   Goal is required. Empty or whitespace -> InvalidGoal, always. There   |
|   is no goal fallback anywhere.                                         |
|   Title: supplied override, else derived from the goal's first line,    |
|   trimmed, word-boundary truncated at 60 chars.                         |
|   Slug: title lowercased, ASCII alphanumerics kept, everything else     |
|   collapsed to '-', trimmed, truncated at 40 chars. ONLY when a         |
|   non-empty title normalizes to no usable slug characters (for          |
|   example "***" or a fully non-ASCII title) does the slug fall back     |
|   to "project". The title itself is preserved verbatim in the           |
|   manifest; the fallback affects the directory name only.               |
|   Home: <user-profile>/Forge/Projects/<slug>, unless HomePath is        |
|   supplied. Deterministic, no model call.                               |
|   Collision: if <slug> exists at all, take <slug>-2, <slug>-3, ...      |
|   The manifest is written with FileMode.CreateNew, so a race against    |
|   a second Forge instance fails at the OS and advances to the next      |
|   suffix rather than overwriting. Bounded attempts, then                |
|   CollisionAttemptsExhausted.                                           |
|   ProjectStore takes its projects root as a constructor argument with   |
|   a profile-resolving default - the shape DefaultWorkspace.CreateNext   |
|   uses today. No configuration knob is added.                           |
|                                                                         |
| --- PARITY: WHAT PROVES A TUI COULD DO THIS -------------------------   |
|                                                                         |
| No TUI project is added in Task 1.                                      |
| Tests/ClientRuntime/ProjectTransportContractTests.cs starts the real    |
| ForgeMission.ClientRuntime process and drives it through the            |
| production HttpClientRuntimeChannel / IClientRuntimeChannel using the   |
| shared transport DTOs. The test IS a second, non-Desktop surface: it    |
| references no Blazor, bunit, Photino, Desktop or Host type.             |
|                                                                         |
| It reuses the existing ClientRuntimeHostProcess helper from             |
| ClientRuntimeTransportOutOfProcessTests; I will extract that helper     |
| into one shared internal test file rather than copy it, so process      |
| start-up keeps a single owner.                                          |
|                                                                         |
| Profile isolation without a production knob: the child process gets     |
| HOME (and USERPROFILE on Windows) pointed at a temp directory, so       |
| <user-profile>/Forge/Projects resolves inside the test's sandbox and    |
| the developer's real home is never written to. .NET resolves            |
| SpecialFolder.UserProfile from those variables; I will confirm that     |
| empirically in step 2 by asserting the temp profile received the        |
| directory, and report the result rather than assume it. If it does      |
| not hold, I fall back to constructing the store with an explicit root   |
| behind the same contract - never to a shipped config option.            |
|                                                                         |
| Surface-free contract cases:                                            |
|   create from goal only     -> Created, deterministic home, complete    |
|                                v1 manifest, session + capabilities      |
|   create with title/home overrides            -> Created                |
|   create with an empty goal                   -> Failed/InvalidGoal     |
|   create a colliding title  -> Created at the -2 suffix, both           |
|                                manifests intact                         |
|   open a created Project    -> Opened, same projectId, root is the      |
|                                Project home                             |
|   open a directory with no manifest -> GoalRequired + proposal,         |
|                                nothing created on disk                  |
|   open a nonexistent directory                -> Failed/HomeNotFound    |
|   open a malformed manifest                   -> Failed/InvalidManifest |
|   open a newer schemaVersion  -> Failed/UnsupportedManifestVersion      |
|   dispatch a capability on a created Project's session, proving the     |
|   authorization boundary is the one a TUI would also get                |
|   SessionSetupRequest with no ReplacesSessionId    -> rejected          |
|   SessionSetupRequest with a root that is not that session's home       |
|                                                   -> rejected           |
|   after each rejection: no new session exists and no directory was      |
|   created                                                               |
|                                                                         |
| --- bunit SCOPE (Desktop interaction only) --------------------------   |
|                                                                         |
| HomeSessionOperationTests keeps its fake IClientRuntimeChannel and      |
| asserts ONLY:                                                           |
|   - boot issues zero requests and zero subscriptions                    |
|   - the create form submits ProjectCreateRequest with exactly the       |
|     typed goal and any typed overrides, and sends nothing when the      |
|     goal is blank                                                       |
|   - Created/Opened renders title/home and starts exactly one            |
|     subscription                                                        |
|   - GoalRequired renders the proposal's home/title and issues no        |
|     create until confirmed                                              |
|   - Failed renders Error.Message as an error state and leaves no        |
|     session or subscription                                             |
|   - mission switch sends SessionSetupRequest with the open Project's    |
|     home and the current session ID, and is not offered while no        |
|     Project is open                                                     |
|   - existing replacement / cancellation / gap-notice coverage           |
| It asserts NO derived title, NO slug, NO collision suffix, NO           |
| manifest content, NO path, and NO error-code semantics beyond           |
| rendering the message - those live in Client Runtime tests.             |
| Structurally this holds because Home.razor computes none of them: it    |
| sends what the user typed and renders what the contract returned.       |
|                                                                         |
| --- COMPLETE v1 MANIFEST SHAPE (unchanged) --------------------------   |
|                                                                         |
|   ProjectManifest                                                       |
|     schemaVersion                int = 1                                |
|     projectId                    Guid                                   |
|     title, goal                  non-empty strings                      |
|     assets                       ProjectAssetDescriptor[] = []          |
|     selectedMission              ProjectMissionReference                |
|     attachedContext              ProjectContextDescriptor[] = []        |
|     missionControlConversationId Guid? = null                           |
|     runs                         ProjectRunMetadata[] = []              |
|                                                                         |
|   ProjectAssetDescriptor  { kind, relativePath, contentHash? }          |
|       kind: Mission | Expert | LockFile                                 |
|   ProjectMissionReference { origin, reference, digest? }                |
|       origin: BuiltIn | Local | Oci                                     |
|       initial { BuiltIn, "Janus", null } - no content hash on the       |
|       mutable selection; localMissionContentHash lives only in          |
|       ProjectLaunchSnapshot, written once by Task 4                     |
|   ProjectContextDescriptor{ id, kind, displayName, reference,           |
|                             contentHash? }                              |
|       kind: SourceRoot | File | Artifact                                |
|   ProjectRunMetadata      { runId, title, status, predecessorRunId?,    |
|                             launchSnapshot }                            |
|       status is the durable ConversationRunStatus from                  |
|       ForgeMission.Conversations.Contracts - no local parallel enum     |
|   ProjectLaunchSnapshot   { mission, localMissionContentHash?,          |
|                             resolvedExperts[], context[],               |
|                             gitRevision?, artifacts[] }                 |
|   ResolvedExpertReference { reference, digest }                         |
|   ProjectContextSnapshot  { contextId, contentHash? }                   |
|   ProjectArtifactSnapshot { artifactId, contentHash? }                  |
|                                                                         |
| Exactly the locked table; no createdUtc is invented.                    |
| JSON: ProjectManifestJsonContext, source-generated, mirroring           |
| ConversationContractsJsonContext - camelCase + UseStringEnumConverter   |
| (so ConversationRunStatus keeps its JsonStringEnumMemberName values)    |
| plus WriteIndented, and deliberately NOT WhenWritingNull so an          |
| explicit "missionControlConversationId": null stays readable.           |
| AOT-safe; no runtime JsonSerializerOptions anywhere.                    |
|                                                                         |
| Store-owned validation on read and before write: schemaVersion,         |
| non-empty projectId/title/goal, selectedMission present, asset          |
| relativePath normalized and contained within the home, and              |
| SourceRoot/File carry a local path while Artifact carries an opaque     |
| ID. Each failure maps to a typed code above and names the manifest      |
| path in its message.                                                    |
|                                                                         |
| --- GOAL IS NEVER EMPTY; OPEN HAS ONE ROUND-TRIP --------------------   |
|                                                                         |
|   manifest found  -> Opened: discovered, projectId preserved, session   |
|                      created rooted there.                              |
|   manifest absent -> GoalRequired: the response carries that            |
|                      directory as the proposed home plus a title        |
|                      derived from the directory name. Nothing is        |
|                      created - no file, no session, no authority. The   |
|                      surface then calls create with that home.          |
|   directory absent-> Failed/HomeNotFound; never created blind.          |
| The goal is validated in the store, not only in a form, so every        |
| surface gets the same rule and no path persists an empty goal.          |
|                                                                         |
| --- NO RECENTS, NO AUTO-RESUME --------------------------------------   |
|                                                                         |
| Boot makes zero channel calls: no open, no session, no subscription,    |
| no directory, no capability authority. No profile-level index is        |
| written or scanned anywhere.                                            |
|                                                                         |
| --- MIGRATION / INVALID MANIFEST ------------------------------------   |
|                                                                         |
| schemaVersion greater than 1 -> Failed/UnsupportedManifestVersion.      |
| Malformed JSON or a missing required field -> Failed/InvalidManifest.   |
| Never silently overwrite or repair a broken manifest. No older          |
| version exists, so no migration step is written yet; Task 1 writes a    |
| manifest only at creation. Task 2's conversation-ID write-back is       |
| where round-trip fidelity starts to matter, which is why the full v1    |
| graph is typed now.                                                     |
|                                                                         |
| --- FILES -----------------------------------------------------------   |
|                                                                         |
| CREATE (ForgeMission.ClientRuntime)                                     |
|   Services/ProjectManifest.cs           full v1 record graph + enums    |
|   Services/ProjectManifestJsonContext.cs                                |
|   Services/ProjectStore.cs   sole filesystem owner and sole owner of    |
|       derivation, collision, validation and typed failures              |
|                                                                         |
| MODIFY                                                                  |
|   ClientRuntime.Transport/ClientRuntimeContracts.cs   project DTOs +    |
|       ProjectOperationResponse/Error; SessionSetupRequest's             |
|       ReplacesSessionId becomes required                                |
|   ClientRuntime.Transport/ClientRuntimeJsonContext.cs                   |
|   ClientRuntime.Transport/HttpClientRuntimeChannel.cs route table       |
|   ClientRuntime/Transport/ClientRuntimeEndpoints.cs   two project       |
|       endpoints replace /transport/session/default; session/setup       |
|       enforces the replacement rule                                     |
|   ClientRuntime/Transport/ClientRuntimeSessionStore.cs  split into      |
|       CreateForProjectAsync / ReplaceAsync; drop                        |
|       _defaultWorkspaceRoot and CreateDefaultAsync                      |
|   ClientRuntime/Program.cs  register ProjectStore; delete the           |
|       Workspace:InitialRoot read and                                    |
|       AddScoped(_ => new WorkspaceState(initialRoot)) - verified dead   |
|       (nothing injects WorkspaceState; the session store constructs     |
|       it directly). Also drop WorkspaceState's unused initialRoot       |
|       ctor parameter and its one test, leaving no boot-time root.       |
|   ClientRuntime.Presentation/Pages/Home.razor  first-use flow           |
|                                                                         |
| DELETE                                                                  |
|   ClientRuntime/Services/DefaultWorkspace.cs                            |
|   Tests/ClientRuntime/DefaultWorkspaceTests.cs                          |
|                                                                         |
| --- TESTS -----------------------------------------------------------   |
|                                                                         |
| new Tests/ClientRuntime/ProjectStoreTests.cs (rules, in-process)        |
|   - create writes the complete v1 shape: typed empty assets /           |
|     attachedContext / runs, selectedMission BuiltIn+Janus+null          |
|     digest, null conversation ID, non-empty goal                        |
|   - derivation table: goal -> title -> slug, with truncation, and       |
|     the "project" slug fallback ONLY for a non-empty title that         |
|     normalizes to no usable slug characters                             |
|   - empty/whitespace goal -> InvalidGoal, nothing created (this is a    |
|     rejection case, never a fallback)                                   |
|   - deterministic home; collision -> -2, -3; exhausted -> typed code    |
|   - full-shape round-trip: a hand-authored fixture populated with       |
|     runs, launchSnapshot, resolved experts, context and artifacts       |
|     parses and re-serializes losslessly (proves v1 completeness now,    |
|     not in Tasks 3/4)                                                   |
|   - open with manifest -> projectId preserved                           |
|   - open without manifest -> GoalRequired, directory unchanged          |
|   - open nonexistent -> HomeNotFound, nothing created                   |
|   - newer schemaVersion / malformed JSON / asset path escaping the      |
|     home -> typed refusal, file untouched                               |
|                                                                         |
| new Tests/ClientRuntime/ProjectTransportContractTests.cs                |
|   the surface-free cases listed above, including both rejected          |
|   SessionSetupRequest shapes, over the real process and shared DTOs;    |
|   no Blazor/bunit/Desktop reference                                     |
|                                                                         |
| update Tests/ClientRuntime/ClientRuntimeSessionStoreTests.cs            |
|   cover CreateForProjectAsync / ReplaceAsync, including rejection of    |
|   an unknown replaces-ID and of a mismatched root. The current          |
|   "unknown ReplacesSessionId is a no-op" test is replaced, because      |
|   that behaviour is exactly what the new rule forbids.                  |
|                                                                         |
| refactor Tests/ClientRuntime/ClientRuntimeTransportOutOfProcessTests    |
|   extract ClientRuntimeHostProcess into one shared internal helper      |
|                                                                         |
| rewrite Tests/Presentation/HomeSessionOperationTests.cs                 |
|   render/invoke assertions only, as scoped above                        |
|                                                                         |
| extend Tests/Architecture/ClientRuntimePresentationBoundaryTests.cs     |
|   marked Presentation projects may not use System.IO APIs               |
|   (Presentation is currently clean: grep for                            |
|   System.IO|Directory.|File.|Path. returns nothing)                     |
|                                                                         |
| delete Tests/ClientRuntime/DefaultWorkspaceTests.cs                     |
|                                                                         |
| I am not adding a source-scan test asserting "Home.razor contains no    |
| Project rule" in general - it would be a fragile regex over             |
| prose-like markup. The enforceable parts are covered: no filesystem     |
| API in Presentation, the runtime-side replacement rule, and the         |
| surface-free contract test, which fails the moment a rule leaks         |
| upward and stops working outside Home.razor.                            |
|                                                                         |
| --- SEQUENCING ------------------------------------------------------   |
|                                                                         |
|   1. Branch codex/phase-43.20-project-manifest.                         |
|   2. Manifest graph + JSON context + ProjectStore + ProjectStoreTests,  |
|      green before anything else moves. Confirm the HOME-redirect        |
|      isolation here and report the result.                              |
|   3. Transport DTOs (ProjectOperationResponse, required                 |
|      ReplacesSessionId), JSON context, route table.                     |
|   4. Session store split, endpoints, Program.cs cleanup; delete         |
|      DefaultWorkspace + its tests; update session-store tests.          |
|   5. Host-process helper extraction + ProjectTransportContractTests     |
|      green BEFORE any Presentation work, so the contract and the        |
|      replacement rule are proven with no UI in the loop.                |
|   6. Home.razor first-use flow (create / open / GoalRequired /          |
|      Failed).                                                           |
|   7. Rewrite and extend Presentation + boundary tests.                  |
|   8. dotnet build -> dotnet test -> make install (AOT).                 |
|   9. Spoke Task 1 status + evidence, build narrative to                 |
|      phase-43.20-project-workbench-mvp_completed.md, PR to main.        |
|                                                                         |
| --- REMAINING ASSUMPTIONS -------------------------------------------   |
|                                                                         |
|   1. ProjectSummary carries projectId, title, goal and home only.       |
|      Presentation renders title + home; mission and run surfaces are    |
|      Tasks 3/4.                                                         |
|   2. A rejected SessionSetupRequest is a 400, not a typed domain        |
|      outcome - see section 2; flag it if you want it typed instead.     |
|   3. Transport DTO enums keep ClientRuntimeJsonContext's existing       |
|      numeric default; only the on-disk manifest uses string enums.      |
|      Both transport sides share one context, so nothing on the          |
|      existing wire format changes.                                      |
|                                                                         |
| Awaiting explicit approval before creating the branch or writing code.  |
+-------------------------------------------------------------------------+
````

## 3. Fault found

It labelled the plan “final” while the visual ownership and acceptance method were still unsettled.

## Requested

Close the remaining action, failure, and manifest-design questions before implementation.

## Better prompt

“Do not call the plan final. Run the build-readiness checklist and report any unanswered visual,

## Prevention

Use “build-ready” only after a checklist verifies contracts, visual references, themes, viewport
