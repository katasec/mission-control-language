# Turn 02 — Revised implementation plan

## 1. Codex prompt

````text
+------------------------------------------------------------------+
| PLAN REVISION REQUEST                                            |
+------------------------------------------------------------------+

Your ownership, adapter-boundary, and verification approach pass review.
Do not implement yet; revise the plan to incorporate these now-locked
decisions:

1. Implement the complete v1 manifest shape from
   docs/phases/phase-43.20-project-workbench-mvp.md#manifest-v1-schema-and-launcher-boundary,
   including typed empty assets, attachedContext, and runs collections.
   Do not omit them because their first populated values arrive in later tasks.

2. selectedMission initially is the built-in Janus reference. Its local content
   hash belongs to an immutable Task 4 launch snapshot, not the mutable Project
   selection.

3. Persisted goal is non-empty. An existing directory without forge.project.json
   must enter the same goal-confirmation/create flow with that directory retained
   as the proposed home; never create an empty-goal manifest.

4. There is no recent-project index or automatic resume in Task 1. Desktop boot
   must make no project-open call, session, event subscription, directory, or
   capability authority. The first screen offers create-from-goal and explicit
   existing-directory open.

5. Home.razor must never use SessionSetupRequest to establish an arbitrary
   unmanifested root. Retain that request only if needed internally to replace
   a session for an already-open Project; create/open project transport calls
   establish the initial session and return its Project/session data.

Keep the approved quality-gate evidence, AOT-safe source-generated JSON, and
test plan. Reply with the revised implementation plan only, then wait for my
explicit approval.
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| REVISED IMPLEMENTATION PLAN - Phase 43.20 Task 1 (Claude -> Codex)      |
| All five locked decisions incorporated. No code written; no branch yet. |
|                                                                         |
| --- DESKTOP QUALITY GATE (unchanged, re-affirmed) --------------------  |
|                                                                         |
| Q1 Behaviour: boot creates no directory, session, subscription, or      |
|    tool authority. One goal produces a named Project with a             |
|    deterministic home + complete v1 manifest; that home is the sole     |
|    local execution root.                                                |
| Q2 Owner: Client Runtime owns forge.project.json and every              |
|    filesystem touch. Presentation calls IClientRuntimeChannel only.     |
|    Desktop Supervisor and Host own none of this.                        |
| Q3 Adapter: verified by reading ClientRuntimeEndpoints.cs,              |
|    HttpClientRuntimeChannel.cs, Home.razor -                            |
|    /transport/session/default is referenced only by Home.razor, the     |
|    channel route table, the JSON context and one test fake. No          |
|    IDesktopHost/Photino/Supervisor/native callback involved.            |
| Q4 Replacement boundary: no Host API, callback, process-lifetime or     |
|    credential change. Two new routes on the runtime that already        |
|    owns local execution.                                                |
| Q5 Proof: ProjectStore unit tests; a full-shape manifest round-trip     |
|    test; bunit tests for zero-call boot and for the SessionSetupRequest |
|    restriction; Presentation System.IO boundary test; dotnet test +     |
|    make install (AOT).                                                  |
| RESULT: PASS.                                                           |
|                                                                         |
| --- 1. COMPLETE v1 MANIFEST SHAPE (locked table, implemented in full)-  |
|                                                                         |
| Task 1 defines and writes every type below, with typed empty            |
| collections. Field set is exactly the locked table - I am NOT adding    |
| a createdUtc field it does not name.                                    |
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
|   ProjectContextDescriptor{ id, kind, displayName, reference,           |
|                             contentHash? }                              |
|       kind: SourceRoot | File | Artifact                                |
|   ProjectRunMetadata      { runId, title, status, predecessorRunId?,    |
|                             launchSnapshot }                            |
|       status is the durable ConversationRunStatus from                  |
|       ForgeMission.Conversations.Contracts - no local parallel enum.    |
|       (ClientRuntime already references that project for                |
|       ConversationRuntimeSession, so this adds no new dependency.)      |
|   ProjectLaunchSnapshot   { mission, localMissionContentHash?,          |
|                             resolvedExperts[], context[],               |
|                             gitRevision?, artifacts[] }                 |
|   ResolvedExpertReference { reference, digest }                         |
|   ProjectContextSnapshot  { contextId, contentHash? }                   |
|   ProjectArtifactSnapshot { artifactId, contentHash? }                  |
|                                                                         |
| JSON: ProjectManifestJsonContext, source-generated, mirroring           |
| ConversationContractsJsonContext's options - camelCase +                |
| UseStringEnumConverter (so ConversationRunStatus serializes under its   |
| declared JsonStringEnumMemberName values), plus WriteIndented. I will   |
| NOT set DefaultIgnoreCondition.WhenWritingNull: an explicit             |
| "missionControlConversationId": null is clearer in a file a human may   |
| read. AOT-safe, no runtime JsonSerializerOptions.                       |
|                                                                         |
| Validation owned by the manifest store, on read and before write:       |
| schemaVersion (newer -> refuse), non-empty projectId/title/goal,        |
| selectedMission present, asset relativePath normalized and contained    |
| within the Project home, and the context-descriptor rule that           |
| SourceRoot/File carry a local path while Artifact carries an opaque     |
| ID. Each failure is a named error naming the manifest path.             |
|                                                                         |
| --- 2. SELECTED MISSION ---------------------------------------------   |
|                                                                         |
| Initial value: { origin: BuiltIn, reference: "Janus", digest: null }.   |
| No content hash is stored on the selection. localMissionContentHash     |
| exists only inside ProjectLaunchSnapshot, written once by Task 4.       |
|                                                                         |
| --- 3. GOAL IS NEVER EMPTY ------------------------------------------   |
|                                                                         |
| Opening an existing directory has two outcomes, in one round-trip:      |
|   manifest found  -> Opened: discovered (projectId preserved), a        |
|                      Client Runtime session is created rooted there.    |
|   manifest absent -> GoalRequired: the response carries that            |
|                      directory as the proposed home plus a title        |
|                      derived from the directory name. NOTHING is        |
|                      created - no file, no session, no authority.       |
|                      Presentation shows the same goal-confirmation      |
|                      form with the home pre-filled and locked to that   |
|                      directory; confirming runs the ordinary create     |
|                      path, which writes the manifest there.             |
| A directory that does not exist is a named error; it is never created   |
| blind. There is no code path that persists an empty goal.               |
|                                                                         |
| --- 4. NO RECENTS, NO AUTO-RESUME -----------------------------------   |
|                                                                         |
| Boot (Home.OnInitializedAsync) makes zero channel calls: no project     |
| open, no session, no SSE subscription, no directory, no capability      |
| authority. No profile-level index is written or scanned anywhere.       |
| First screen: create-from-goal, plus explicit existing-directory open.  |
| Proven by a bunit test asserting zero requests and zero subscriptions   |
| after render.                                                           |
|                                                                         |
| --- 5. SESSIONSETUPREQUEST IS NOT A ROOT ESTABLISHER -----------------  |
|                                                                         |
| The free-path "+ Add folder" menu is removed from Home.razor.           |
| Project create and project open are the only calls that establish an    |
| initial session, and each returns its Project + session data.           |
| SessionSetupRequest is retained solely to REPLACE a session for an      |
| already-open Project (mission switch): its WorkspaceRoot is always      |
| the open Project's home and ReplacesSessionId is always set. Enforced   |
| by a bunit test asserting Home never sends SessionSetupRequest with     |
| any other root and never sends one while no Project is open.            |
|                                                                         |
| --- TRANSPORT CONTRACTS ---------------------------------------------   |
|                                                                         |
|   ProjectCreateRequest(Goal, Title?, HomePath?, Mission?, Runtime)      |
|       -> ProjectSession                                                 |
|   ProjectOpenRequest(HomePath, Mission?, Runtime)                       |
|       -> ProjectOpenResponse(Outcome, Session?, Proposal?)              |
|          Outcome: Opened | GoalRequired                                 |
|   ProjectSession(SessionId, AvailableCapabilities, Project)             |
|   ProjectSummary(ProjectId, Title, Goal, Home)                          |
|   ProjectHomeProposal(HomePath, ProposedTitle)                          |
| Routes: transport/project/create, transport/project/open.               |
| Deleted: DefaultWorkspaceSessionRequest/Response and                    |
| /transport/session/default.                                             |
| One two-outcome open response rather than an inspect-then-open pair:    |
| it keeps open as a single operation with no intermediate state and no   |
| window in which the directory changes between the two calls.            |
|                                                                         |
| --- FILES -----------------------------------------------------------   |
|                                                                         |
| CREATE (ForgeMission.ClientRuntime)                                     |
|   Services/ProjectManifest.cs   full v1 record graph + enums            |
|   Services/ProjectManifestJsonContext.cs                                |
|   Services/ProjectStore.cs      sole filesystem owner: Create /         |
|       Open, slug derivation, collision loop, read, validate, write,     |
|       named failures.                                                   |
|                                                                         |
| MODIFY                                                                  |
|   ClientRuntime.Transport/ClientRuntimeContracts.cs   swap DTOs         |
|   ClientRuntime.Transport/ClientRuntimeJsonContext.cs                   |
|   ClientRuntime.Transport/HttpClientRuntimeChannel.cs route table       |
|   ClientRuntime/Transport/ClientRuntimeEndpoints.cs   two project       |
|       endpoints replace /transport/session/default                      |
|   ClientRuntime/Transport/ClientRuntimeSessionStore.cs  drop            |
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
| --- HOME DERIVATION AND COLLISION (unchanged) -----------------------   |
|                                                                         |
| goal -> first line, trimmed, word-boundary truncated at 60 -> title;    |
| title -> lowercase, non-alphanumeric collapsed to '-', trimmed, 40      |
| chars, empty -> "project" -> slug;                                      |
| home = <user-profile>/Forge/Projects/<slug>. Deterministic, no model.   |
| Title and location are shown pre-filled and editable; only the goal     |
| is required.                                                            |
| Collision: if <slug> exists at all, take <slug>-2, <slug>-3, ...        |
| The manifest is written with FileMode.CreateNew, so a race against a    |
| second Forge instance fails at the OS and advances to the next suffix   |
| rather than overwriting. Bounded retries, then a named error.           |
|                                                                         |
| --- MIGRATION / INVALID MANIFEST ------------------------------------   |
|                                                                         |
| schemaVersion greater than 1 -> refuse ("created by a newer version     |
| of Forge"). Malformed JSON, missing required field, or a validation     |
| failure above -> refuse, naming the path. Never silently overwrite or   |
| repair a broken manifest. No older version exists, so no migration      |
| step is written yet; Task 1 writes a manifest only at creation, so      |
| there is no lossy-rewrite risk. Task 2's conversation-ID write-back     |
| is where round-trip fidelity starts to matter, which is why the full    |
| v1 graph is typed now.                                                  |
|                                                                         |
| --- TESTS -----------------------------------------------------------   |
|                                                                         |
| new Tests/ClientRuntime/ProjectStoreTests.cs                            |
|   - create writes the complete v1 shape: typed empty assets /           |
|     attachedContext / runs, selectedMission BuiltIn+Janus+null          |
|     digest, null conversation ID, non-empty goal                        |
|   - deterministic home for a given title; collision -> -2, -3           |
|   - full-shape round-trip: a hand-authored fixture populated with       |
|     runs, launchSnapshot, resolved experts, context and artifacts       |
|     parses and re-serializes losslessly (proves v1 completeness         |
|     without waiting for Tasks 3/4)                                      |
|   - open existing dir with manifest -> Opened, projectId preserved      |
|   - open existing dir without manifest -> GoalRequired, directory       |
|     byte-for-byte unchanged                                             |
|   - open nonexistent dir -> named error, nothing created                |
|   - newer schemaVersion / malformed JSON / empty goal / asset path      |
|     escaping the home -> named refusal, file untouched                  |
| rewrite Tests/Presentation/HomeSessionOperationTests.cs                 |
|   - boot: zero requests, zero subscriptions (replaces the old           |
|     DefaultWorkspaceReady focus test's boot assumption)                 |
|   - create-from-goal and open both yield exactly one subscription       |
|   - GoalRequired path renders the pre-filled goal form and issues no    |
|     create until confirmed                                              |
|   - mission switch sends SessionSetupRequest only with the open         |
|     Project home and only while a Project is open                       |
|   - existing replacement/cancellation/gap-notice coverage retained,     |
|     helpers repointed from AddFolderAsync to project create/open        |
| extend Tests/Architecture/ClientRuntimePresentationBoundaryTests.cs     |
|   - marked Presentation projects may not use System.IO APIs             |
|     (Presentation is currently clean: grep for                          |
|     System.IO|Directory.|File.|Path. returns nothing)                   |
| delete Tests/ClientRuntime/DefaultWorkspaceTests.cs                     |
|                                                                         |
| --- SEQUENCING ------------------------------------------------------   |
|                                                                         |
|   1. Branch codex/phase-43.20-project-manifest.                         |
|   2. Manifest graph + JSON context + ProjectStore + unit tests,         |
|      green before anything else moves.                                  |
|   3. Transport DTOs, JSON context, route table.                         |
|   4. Endpoints, session-store and Program.cs cleanup; delete            |
|      DefaultWorkspace + its tests.                                      |
|   5. Home.razor first-use flow (create / open / GoalRequired).          |
|   6. Rewrite and extend Presentation + boundary tests.                  |
|   7. dotnet build -> dotnet test -> make install (AOT).                 |
|   8. Spoke Task 1 status + evidence, build narrative to                 |
|      phase-43.20-project-workbench-mvp_completed.md, PR to main.        |
|                                                                         |
| --- REMAINING ASSUMPTIONS -------------------------------------------   |
|                                                                         |
|   1. ProjectSummary carries projectId, title, goal and home only.       |
|      Presentation renders title + home; mission and run surfaces are    |
|      Tasks 3/4, so nothing more is exposed yet.                         |
|   2. Transport DTO enums keep ClientRuntimeJsonContext's existing       |
|      numeric default; only the on-disk manifest uses string enums.      |
|      Both sides of the transport share one context, so this is          |
|      internally consistent and changes no existing wire format.         |
|                                                                         |
| No open questions remain. Awaiting explicit approval before creating    |
| the branch or writing code.                                             |
+-------------------------------------------------------------------------+
````

## 3. Fault found

It improved the engineering gates but still treated visual design as a later implementation detail.

## Requested

Revise the initial plan after feedback about ownership and implementation scope.

## Better prompt

“Revise only after adding a binding visual acceptance section: named states, approved reference

## Prevention

Make an approved state specification a hard predecessor of implementation, alongside API and
