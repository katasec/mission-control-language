# Turn 06 — Final plan v5

## 1. Codex prompt

````text
+------------------------------------------------------------------+
| FINAL PLAN REVISION — DRAFT + TUI GATE                           |
+------------------------------------------------------------------+

Do not implement yet.

Read:
- docs/design/forge-architecture.md
- docs/design/engineering-philosophy.md
- docs/phases/phase-43.20-project-workbench-mvp.md

Revise the plan to add the now-locked ProjectDraftRequest contract:

- ProjectDraftRequest(goal, titleOverride?, homeOverride?) returns the
  Client Runtime-derived title/home for display, or the same typed
  ProjectOperationError for invalid input.
- It is side-effect free: no directory, manifest, session, capability
  authority, or collision reservation.
- Desktop and a future TUI call it only after the user enters a goal;
  boot still makes zero channel calls.
- Home.razor displays returned values as editable overrides and performs
  no project derivation itself.
- Create recomputes the draft and remains authoritative for collision-safe
  final-home selection.
- Add surface-free contract coverage plus Desktop bunit interaction coverage.

Also start the revised plan with the mandatory Presentation-surface parity
gate result from Engineering Philosophy:

PASS only if a TUI can invoke every Task 1 product action—draft, create,
open, and session replacement—through the same Client Runtime contracts
with the same authorization, outcomes, and failure semantics. Do not treat
Desktop layout, focus, keyboard handling, or window behavior as TUI product
requirements.

Reply with the revised plan only, then wait for my explicit approval.
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| FINAL IMPLEMENTATION PLAN v5 - Phase 43.20 Task 1 (Claude -> Codex)     |
| Adds the locked ProjectDraftRequest. No code written; no branch yet.    |
|                                                                         |
| --- PRESENTATION-SURFACE PARITY GATE (mandatory, first) -------------   |
|                                                                         |
| Q: Could a TUI invoke this same product action with the same            |
|    authorization, outcome, and failure semantics?                       |
|                                                                         |
|   draft   ProjectDraftRequest -> ProjectDraftResponse. Derivation       |
|           runs in ProjectStore; no surface computes a title, slug or    |
|           home. Side-effect free, so a TUI gets the identical values    |
|           with no authorization involved.                               |
|   create  ProjectCreateRequest -> ProjectOperationResponse. All         |
|           derivation, collision and validation in ProjectStore;         |
|           expected failures are typed codes, not exceptions a UI        |
|           interprets.                                                   |
|   open    ProjectOpenRequest -> ProjectOperationResponse, including     |
|           the GoalRequired outcome, from the same store.                |
|   session ProjectStore/session store establish the root; the            |
|   replace  replacement rule is enforced in the Client Runtime           |
|           endpoint, so a TUI is bound by exactly the same guard and     |
|           cannot mint a first root from an arbitrary folder.            |
|   proof   Tests/ClientRuntime/ProjectTransportContractTests.cs          |
|           exercises all four over the real Client Runtime process       |
|           through the production IClientRuntimeChannel, referencing     |
|           no Desktop/Blazor/bunit/Photino/Host type.                    |
|                                                                         |
| Excluded by the gate, correctly: Desktop layout, focus, keyboard        |
| handling, window behaviour, and when the draft call is triggered by a   |
| given surface. No Project, mission, run, capability or conversation     |
| rule lives above the contract.                                          |
|                                                                         |
| RESULT: PASS.                                                           |
|                                                                         |
| --- DESKTOP QUALITY GATE (re-affirmed) ------------------------------   |
|                                                                         |
| Q1 Behaviour: boot creates no directory, session, subscription or       |
|    tool authority. One goal produces a named Project with a             |
|    deterministic home + complete v1 manifest; that home is the sole     |
|    local execution root.                                                |
| Q2 Owner: Client Runtime owns forge.project.json, every filesystem      |
|    touch, every Project rule, derivation, and the session-replacement   |
|    rule. Presentation renders state and invokes named contracts.        |
|    Supervisor and Host own none of this.                                |
| Q3 Adapter: verified by reading ClientRuntimeEndpoints.cs,              |
|    HttpClientRuntimeChannel.cs and Home.razor -                         |
|    /transport/session/default is referenced only by Home.razor, the     |
|    channel route table, the JSON context and one test fake. No          |
|    IDesktopHost/Photino/Supervisor/native callback involved.            |
| Q4 Replacement boundary: no Host API, callback, process-lifetime or     |
|    credential change. Three new routes on the runtime that already      |
|    owns local execution.                                                |
| Q5 Proof: ProjectStore unit tests; full-shape manifest round-trip;      |
|    the surface-free contract test above (draft, create, open,           |
|    GoalRequired, every typed failure code, both rejected                |
|    SessionSetupRequest shapes, and draft's absence of side effects);    |
|    bunit tests for zero-call boot, draft rendering and contract         |
|    invocation; Presentation System.IO boundary test; dotnet test +      |
|    make install (AOT).                                                  |
| RESULT: PASS.                                                           |
|                                                                         |
| --- 1. ProjectDraftRequest (new, side-effect free) ------------------   |
|                                                                         |
|   ProjectDraftRequest(Goal, TitleOverride?, HomeOverride?)              |
|       -> ProjectDraftResponse(ProjectHomeProposal? Draft,               |
|                               ProjectOperationError? Error)             |
|   Route: transport/project/draft.                                       |
|                                                                         |
| It reuses ProjectHomeProposal - the same {HomePath, ProposedTitle}      |
| "here is what would be used" shape the GoalRequired outcome already     |
| returns - and the same typed ProjectOperationError. It is a separate    |
| response record rather than a fifth ProjectOperationOutcome, because    |
| Session/Created/Opened are meaningless for a draft and an exhaustive    |
| two-field response is easier for any surface to handle correctly.       |
|                                                                         |
| Purity: the endpoint calls a pure ProjectStore.Draft(...) that          |
| performs no filesystem work at all - no directory creation, no          |
| manifest, no session, no capability authority, and deliberately NO      |
| collision probing, since a suffix search would be both filesystem       |
| access and an implied reservation. The draft therefore returns the      |
| base home for the derived slug. Create recomputes the derivation and    |
| performs the authoritative collision-safe write, so a concurrent        |
| creation may legitimately land on a different suffix than the draft     |
| displayed. That is the locked behaviour, not a defect.                  |
|                                                                         |
| Validation in draft is the pure subset: empty/whitespace goal ->        |
| InvalidGoal; malformed or non-absolute home override -> InvalidHome.    |
| HomeNotFound is not a draft outcome, because existence is a             |
| filesystem question draft must not ask.                                 |
|                                                                         |
| Surface usage: called once after the user has entered a goal (on        |
| explicit continue/blur, never per keystroke - a fixed convention, not   |
| a configurable one), never at boot. Home.razor renders the returned     |
| title and home into editable override fields and sends whatever the     |
| user leaves there straight back to create. Home derives nothing.        |
| The GoalRequired flow uses the same contract: after the user supplies   |
| a goal for an existing directory, Home calls draft with HomeOverride    |
| set to that directory and displays what comes back, so even that        |
| title comes from the runtime.                                           |
|                                                                         |
| --- 2. SHARED ProjectOperationResponse (unchanged from v4) ----------   |
|                                                                         |
|   enum ProjectOperationOutcome                                          |
|       { Created, Opened, GoalRequired, Failed }                         |
|   ProjectOperationResponse(                                             |
|       Outcome,                                                          |
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
|   ProjectOperationErrorCode:                                            |
|       InvalidGoal                 empty/whitespace goal                 |
|       InvalidHome                 malformed or non-absolute home        |
|       HomeNotFound                open target does not exist            |
|       InvalidManifest             malformed JSON / missing field        |
|       UnsupportedManifestVersion  schemaVersion newer than 1            |
|       InvalidPath                 asset/context path validation         |
|       CollisionAttemptsExhausted  bounded suffix search exhausted       |
|   Each is a 200 response a TUI renders exactly as Desktop does. Codes   |
|   are a typed enum, not free strings. Unexpected process/transport      |
|   failures still fail the transport normally and are never laundered    |
|   into a domain code.                                                   |
|                                                                         |
| --- 3. REPLACEMENT-ONLY RULE, ENFORCED IN CLIENT RUNTIME ------------   |
|                                                                         |
| ClientRuntimeSessionStore's single permissive CreateAsync splits into   |
| two named methods, so the rule is structural, not a convention:         |
|                                                                         |
|   CreateForProjectAsync(projectHome, mission, runtime)                  |
|       the ONLY way a first session/root is established; called only     |
|       by the project create/open endpoints after ProjectStore has       |
|       produced/validated that home.                                     |
|   ReplaceAsync(replacesSessionId, workspaceRoot, mission, runtime)      |
|       the ONLY path behind /transport/session/setup. Requires           |
|       replacesSessionId, requires it to identify a live session, and    |
|       requires workspaceRoot to equal that session's existing root.     |
|       Any violation is rejected and nothing is created.                 |
|                                                                         |
| SessionSetupRequest.ReplacesSessionId becomes non-optional. A           |
| rejected call fails the transport with 400 and a named reason rather    |
| than a ProjectOperationError: it is a misuse/stale-race guard, not a    |
| Project domain outcome a surface renders as a normal state. Home        |
| already surfaces such a failure through its existing sessionError       |
| banner. Flag it if you want it typed instead - it is a one-line         |
| change, but I would rather fail loudly than add a renderable outcome    |
| for a call no correct surface should ever make.                         |
|                                                                         |
| --- 4. DERIVATION (Client Runtime only) -----------------------------   |
|                                                                         |
|   Goal is required. Empty or whitespace -> InvalidGoal, always; there   |
|   is no goal fallback anywhere.                                         |
|   Title: supplied override, else derived from the goal's first line,    |
|   trimmed, word-boundary truncated at 60 chars.                         |
|   Slug: title lowercased, ASCII alphanumerics kept, everything else     |
|   collapsed to '-', trimmed, truncated at 40 chars. ONLY when a         |
|   non-empty title normalizes to no usable slug characters (for          |
|   example "***" or a fully non-ASCII title) does the slug fall back     |
|   to "project". The title is preserved verbatim in the manifest; the    |
|   fallback affects the directory name only.                             |
|   Home: <user-profile>/Forge/Projects/<slug>, unless a home override    |
|   is supplied. Deterministic, no model call.                            |
|   Collision (create only): if <slug> exists at all, take <slug>-2,      |
|   <slug>-3, ... The manifest is written with FileMode.CreateNew, so a   |
|   race against a second Forge instance fails at the OS and advances     |
|   to the next suffix rather than overwriting. Bounded attempts, then    |
|   CollisionAttemptsExhausted.                                           |
|   ProjectStore takes its projects root as a constructor argument with   |
|   a profile-resolving default - the shape DefaultWorkspace.CreateNext   |
|   uses today. No configuration knob is added.                           |
|                                                                         |
| --- 5. COMPLETE v1 MANIFEST SHAPE (unchanged) -----------------------   |
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
| ID. Each failure maps to a typed code and names the manifest path.      |
|                                                                         |
| --- 6. OPEN, BOOT, MIGRATION (unchanged) ----------------------------   |
|                                                                         |
| Open, one round-trip:                                                   |
|   manifest found  -> Opened: discovered, projectId preserved, session   |
|                      created rooted there.                              |
|   manifest absent -> GoalRequired: proposal carries that directory as   |
|                      the home plus a title from the directory name.     |
|                      Nothing created - no file, session or authority.   |
|   directory absent-> Failed/HomeNotFound; never created blind.          |
| Boot makes zero channel calls: no draft, open, session, subscription,   |
| directory or capability authority, and no profile-level index is        |
| written or scanned anywhere.                                            |
| schemaVersion > 1 -> Failed/UnsupportedManifestVersion; malformed       |
| JSON or a missing field -> Failed/InvalidManifest. A broken manifest    |
| is never silently overwritten or repaired. No older version exists,     |
| so no migration step is written yet; Task 1 writes a manifest only at   |
| creation. Task 2's conversation-ID write-back is where round-trip       |
| fidelity starts to matter, which is why the full v1 graph is typed      |
| now.                                                                    |
|                                                                         |
| --- FILES -----------------------------------------------------------   |
|                                                                         |
| CREATE (ForgeMission.ClientRuntime)                                     |
|   Services/ProjectManifest.cs           full v1 record graph + enums    |
|   Services/ProjectManifestJsonContext.cs                                |
|   Services/ProjectStore.cs   sole owner of derivation (pure Draft),     |
|       filesystem work, collision, validation and typed failures         |
|                                                                         |
| MODIFY                                                                  |
|   ClientRuntime.Transport/ClientRuntimeContracts.cs   project DTOs,     |
|       ProjectDraftRequest/Response, ProjectOperationResponse/Error;     |
|       SessionSetupRequest's ReplacesSessionId becomes required          |
|   ClientRuntime.Transport/ClientRuntimeJsonContext.cs                   |
|   ClientRuntime.Transport/HttpClientRuntimeChannel.cs route table       |
|   ClientRuntime/Transport/ClientRuntimeEndpoints.cs   draft/create/     |
|       open endpoints replace /transport/session/default;                |
|       session/setup enforces the replacement rule                       |
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
|   - Draft is pure: derived title/home returned, and the projects root   |
|     does not exist afterwards (no directory, no manifest, no probe)     |
|   - Draft honours title/home overrides; empty goal -> InvalidGoal;      |
|     malformed/relative home override -> InvalidHome                     |
|   - Draft of a name whose base home already exists still returns the    |
|     base home (no suffix, no reservation), while create of the same     |
|     goal lands on -2 - the locked draft-vs-create divergence, proven    |
|     rather than assumed                                                 |
|   - create writes the complete v1 shape: typed empty assets /           |
|     attachedContext / runs, selectedMission BuiltIn+Janus+null          |
|     digest, null conversation ID, non-empty goal                        |
|   - derivation table: goal -> title -> slug with truncation, and the    |
|     "project" slug fallback ONLY for a non-empty title that             |
|     normalizes to no usable slug characters                             |
|   - empty/whitespace goal on create -> InvalidGoal, nothing created     |
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
|   (surface-free; real Client Runtime process; production                |
|   IClientRuntimeChannel; no Blazor/bunit/Desktop reference)             |
|   - draft returns derived title/home and creates nothing on disk and    |
|     no session                                                          |
|   - draft with an empty goal -> Failed/InvalidGoal                      |
|   - create from goal only -> Created, deterministic home, complete v1   |
|     manifest, session + capabilities                                    |
|   - create with overrides -> Created; colliding title -> -2 suffix      |
|     with both manifests intact                                          |
|   - open a created Project -> Opened, same projectId, root is the       |
|     Project home                                                        |
|   - open a directory with no manifest -> GoalRequired + proposal,       |
|     nothing created                                                     |
|   - open nonexistent / malformed / newer-version -> the matching        |
|     typed codes                                                         |
|   - a created Project's session dispatches a capability, proving the    |
|     authorization boundary a TUI would also get                         |
|   - SessionSetupRequest with no ReplacesSessionId -> rejected           |
|   - SessionSetupRequest with a root that is not that session's home     |
|     -> rejected; after each rejection no new session and no directory   |
|                                                                         |
| update Tests/ClientRuntime/ClientRuntimeSessionStoreTests.cs            |
|   cover CreateForProjectAsync / ReplaceAsync, including rejection of    |
|   an unknown replaces-ID and of a mismatched root. The current          |
|   "unknown ReplacesSessionId is a no-op" test is replaced, because      |
|   that behaviour is exactly what the new rule forbids.                  |
|                                                                         |
| refactor Tests/ClientRuntime/ClientRuntimeTransportOutOfProcessTests    |
|   extract ClientRuntimeHostProcess into one shared internal helper,     |
|   so process start-up keeps a single owner. Its child gets HOME (and    |
|   USERPROFILE on Windows) pointed at a temp directory so                |
|   <user-profile>/Forge/Projects resolves inside the test sandbox and    |
|   the developer's real home is never written to - no shipped config     |
|   option. I will confirm that redirect empirically in step 2 and        |
|   report the result; if it does not hold I fall back to an explicit     |
|   store root behind the same contract, never to a production knob.      |
|                                                                         |
| rewrite Tests/Presentation/HomeSessionOperationTests.cs                 |
|   Desktop interaction only:                                             |
|   - boot issues zero requests and zero subscriptions                    |
|   - entering a goal issues exactly one ProjectDraftRequest, and none    |
|     is issued at boot or per keystroke                                  |
|   - the draft response's title/home render into editable fields, and    |
|     edits are sent verbatim on ProjectCreateRequest                     |
|   - a draft error renders its message and blocks create                 |
|   - Created/Opened renders title/home and starts exactly one            |
|     subscription                                                        |
|   - GoalRequired renders the proposal and issues no create until        |
|     confirmed                                                           |
|   - Failed renders Error.Message and leaves no session/subscription     |
|   - mission switch sends SessionSetupRequest with the open Project's    |
|     home and current session ID, and is not offered with no Project     |
|   - existing replacement / cancellation / gap-notice coverage           |
|   It asserts NO derived title, NO slug, NO collision suffix, NO         |
|   manifest content, NO path, and no error-code semantics beyond         |
|   rendering the message. Structurally this holds because Home.razor     |
|   computes none of them: it sends what the user typed and renders       |
|   what the contract returned.                                           |
|                                                                         |
| extend Tests/Architecture/ClientRuntimePresentationBoundaryTests.cs     |
|   marked Presentation projects may not use System.IO APIs               |
|   (Presentation is currently clean: grep for                            |
|   System.IO|Directory.|File.|Path. returns nothing)                     |
|                                                                         |
| delete Tests/ClientRuntime/DefaultWorkspaceTests.cs                     |
|                                                                         |
| I am not adding a source-scan test asserting "Home.razor contains no    |
| Project rule" in general - a regex over prose-like markup would be      |
| fragile. The enforceable parts are covered: no filesystem API in        |
| Presentation, the runtime-side replacement rule, and the surface-free   |
| contract test, which fails the moment a rule leaks upward and stops     |
| working outside Home.razor.                                             |
|                                                                         |
| --- SEQUENCING ------------------------------------------------------   |
|                                                                         |
|   1. Branch codex/phase-43.20-project-manifest.                         |
|   2. Manifest graph + JSON context + ProjectStore (pure Draft first,    |
|      then Create/Open) + ProjectStoreTests, green before anything       |
|      else moves. Confirm the HOME-redirect isolation here and report    |
|      the result.                                                        |
|   3. Transport DTOs (draft, ProjectOperationResponse, required          |
|      ReplacesSessionId), JSON context, route table.                     |
|   4. Session store split, three project endpoints, Program.cs           |
|      cleanup; delete DefaultWorkspace + its tests; update               |
|      session-store tests.                                               |
|   5. Host-process helper extraction + ProjectTransportContractTests     |
|      green BEFORE any Presentation work, so draft, create, open and     |
|      the replacement rule are proven with no UI in the loop.            |
|   6. Home.razor first-use flow (goal -> draft -> editable overrides     |
|      -> create; open; GoalRequired; Failed).                            |
|   7. Rewrite and extend Presentation + boundary tests.                  |
|   8. dotnet build -> dotnet test -> make install (AOT).                 |
|   9. Spoke Task 1 status + evidence, build narrative to                 |
|      phase-43.20-project-workbench-mvp_completed.md, PR to main.        |
|                                                                         |
| --- REMAINING ASSUMPTIONS -------------------------------------------   |
|                                                                         |
|   1. ProjectDraftResponse is its own two-field record reusing           |
|      ProjectHomeProposal and ProjectOperationError, rather than a       |
|      fifth ProjectOperationOutcome - see section 1.                     |
|   2. ProjectSummary carries projectId, title, goal and home only.       |
|      Presentation renders title + home; mission and run surfaces are    |
|      Tasks 3/4.                                                         |
|   3. A rejected SessionSetupRequest is a 400, not a typed domain        |
|      outcome - see section 3; flag it if you want it typed instead.     |
|   4. Transport DTO enums keep ClientRuntimeJsonContext's existing       |
|      numeric default; only the on-disk manifest uses string enums.      |
|      Both transport sides share one context, so nothing on the          |
|      existing wire format changes.                                      |
|                                                                         |
| Awaiting explicit approval before creating the branch or writing code.  |
+-------------------------------------------------------------------------+
````

## 3. Fault found

The plan became more detailed without performing an order-of-magnitude check against the modest

## Requested

Strengthen the shared-action/TUI proof and lock the Task 1 plan.

## Better prompt

“First provide an order-of-magnitude card: user-visible output, expected files, expected tests,

## Prevention

Add a mandatory scope card to every plan: tangible output, files expected, dependencies, and why
