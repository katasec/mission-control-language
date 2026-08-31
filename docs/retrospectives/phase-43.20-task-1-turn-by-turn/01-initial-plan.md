# Turn 01 — Initial implementation plan

## 1. Codex prompt

````text
+------------------------------------------------------------------+
| TASK ASSIGNMENT                                                  |
+------------------------------------------------------------------+

Role: implementer. Do not write or modify any code until I approve
your plan.

Read first (do not summarize these back to me):
- AGENTS.md
- docs/plan.md
- docs/phases/phase-43-forge-desktop.md
- docs/phases/phase-43.20-project-workbench-mvp.md
- docs/design/forge-architecture.md
- docs/design/durable-conversations.md
- docs/design/security-architecture.md
- docs/design/engineering-philosophy.md
- src/ForgeMission.ClientRuntime/Services/DefaultWorkspace.cs
- src/ForgeMission.ClientRuntime/Transport/ClientRuntimeSessionStore.cs
- src/ForgeMission.ClientRuntime.Presentation/Pages/Home.razor
- src/ForgeMission.Conversations.Contracts/ConversationContracts.cs
- src/ForgeMission.ConversationHost/Grains/ConversationGrain.cs
- src/ForgeMission.ConversationWorker/Messaging/MissionCommandProcessor.cs
- src/ForgeMission.ClientRuntime/Services/ConversationRuntimeSession.cs

Task:
Implement Phase 43.20, Task 1: replace the proof-era numbered anonymous
workspace bootstrap with a local, versioned forge.project.json Project record.
Expose Project creation/opening through Client Runtime transport contracts, and
make the Presentation first-use flow ask only for a goal.

Done when:
An empty profile creates neither a directory nor Client Runtime session merely
by opening Desktop. Creating “Todos API” produces one deterministic Project
home and manifest with collision handling; reopening uses that Project home as
the sole local execution root; numbered-workspace tests are replaced; Client
Runtime/Presentation boundary tests and the normal solution build/test suite
pass.

Constraints:
- After plan approval, create and work on
  codex/phase-43.20-project-manifest; do not work directly on main.
- Keep all filesystem access in ForgeMission.ClientRuntime.
- Use source-generated JSON; preserve Native AOT safety.
- Keep MissionControlConversationId optional for Task 2.
- Do not create a project directory, Client Runtime session, or tool authority
  at Desktop boot.
- Do not alter the Desktop Supervisor/Host contract, add a hosted Project
  database, or give Presentation direct filesystem access.
- Include migration/invalid-manifest behavior and collision handling in the
  plan; do not introduce speculative project/catalog abstractions.

Next step:
Reply with an implementation plan only: files you will touch or create, your
approach, sequencing, and any assumption or open question not already answered
in the docs above. Start with the five PASS/FAIL answers in Engineering
Philosophy’s Desktop Design and Implementation Quality Gate. Wait for my
explicit approval before implementing.
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| IMPLEMENTATION PLAN - Phase 43.20 Task 1 (Claude -> Codex)              |
| Project home and local manifest. No code written; no branch created.    |
|                                                                         |
| --- DESKTOP DESIGN AND IMPLEMENTATION QUALITY GATE ------------------   |
|                                                                         |
| Q1 What product behaviour is required?                                  |
|    Opening Forge creates no directory, no session, and no tool          |
|    authority. A person supplies one goal and gets a named Project       |
|    with a deterministic home + manifest; reopening that home is the     |
|    sole local execution root.                                           |
|                                                                         |
| Q2 Who owns it?                                                         |
|    Client Runtime (in-process, unsandboxed) owns forge.project.json,    |
|    slug/collision derivation, and every filesystem touch.               |
|    Presentation renders the goal prompt and calls                       |
|    IClientRuntimeChannel only. Desktop Supervisor and Host own none     |
|    of this behaviour.                                                   |
|                                                                         |
| Q3 What has been verified about the adapter?                            |
|    Verified by reading ClientRuntimeEndpoints.cs,                       |
|    HttpClientRuntimeChannel.cs and Home.razor:                          |
|    Presentation already reaches Client Runtime over the existing        |
|    HTTP+SSE channel, and /transport/session/default is referenced       |
|    ONLY by Home.razor, the channel route table, the JSON context and    |
|    one test fake. No IDesktopHost, Photino, Supervisor or native        |
|    callback is involved. This is a grep/read observation, not an        |
|    assumption.                                                          |
|                                                                         |
| Q4 Why does the proposal preserve the replacement boundary?             |
|    No Host API, callback, process-lifetime, credential hand-off or      |
|    capability-provider change. Two new HTTP routes on the runtime       |
|    that already owns local execution. Replacing Photino changes         |
|    nothing in this diff.                                                |
|                                                                         |
| Q5 What proves it?                                                      |
|    ProjectStore unit tests (deterministic home, collision, invalid      |
|    and newer-version manifest, discover-vs-create); a bunit test        |
|    asserting boot creates ZERO sessions and ZERO subscriptions; an      |
|    extended Presentation boundary test forbidding System.IO in          |
|    marked Presentation projects; full dotnet test plus make install     |
|    (AOT publish, since new source-generated JSON is added).             |
|                                                                         |
| RESULT: PASS.                                                           |
|                                                                         |
| --- FILES -----------------------------------------------------------   |
|                                                                         |
| CREATE (ForgeMission.ClientRuntime)                                     |
|   Services/ProjectManifest.cs                                           |
|       versioned record + ProjectManifestJsonContext (source-gen,        |
|       camelCase, matching the transport convention).                    |
|   Services/ProjectStore.cs                                              |
|       the only filesystem owner: Create(goal, title?, location?),       |
|       Open(homePath), slug derivation, collision loop,                  |
|       read/validate/write, named failures.                              |
|                                                                         |
| MODIFY                                                                  |
|   ClientRuntime.Transport/ClientRuntimeContracts.cs                     |
|       delete DefaultWorkspaceSessionRequest/Response; add               |
|       ProjectCreateRequest, ProjectOpenRequest,                         |
|       ProjectSessionResponse, ProjectSummary.                           |
|   ClientRuntime.Transport/ClientRuntimeJsonContext.cs                   |
|   ClientRuntime.Transport/HttpClientRuntimeChannel.cs                   |
|       swap serializable types and route entries to                      |
|       transport/project/create and transport/project/open.              |
|   ClientRuntime/Transport/ClientRuntimeEndpoints.cs                     |
|       replace /transport/session/default with the two project           |
|       endpoints (store first, then sessions.CreateAsync(home, ...)).    |
|   ClientRuntime/Transport/ClientRuntimeSessionStore.cs                  |
|       delete _defaultWorkspaceRoot and CreateDefaultAsync.              |
|   ClientRuntime/Program.cs                                              |
|       register ProjectStore; delete the Workspace:InitialRoot read      |
|       and AddScoped(_ => new WorkspaceState(initialRoot)) - verified    |
|       dead, nothing injects WorkspaceState, the session store           |
|       constructs it directly. Also drop WorkspaceState's now-unused     |
|       initialRoot ctor parameter and its one test, so no boot-time      |
|       root path remains anywhere.                                       |
|   ClientRuntime.Presentation/Pages/Home.razor                           |
|       first-use flow (below).                                           |
|                                                                         |
| DELETE                                                                  |
|   ClientRuntime/Services/DefaultWorkspace.cs                            |
|   Tests/ClientRuntime/DefaultWorkspaceTests.cs                          |
|                                                                         |
| TESTS                                                                   |
|   new Tests/ClientRuntime/ProjectStoreTests.cs                          |
|   rewrite Tests/Presentation/HomeSessionOperationTests.cs helpers       |
|       (AddFolderAsync -> project create/open; RenderHome no longer      |
|       waits for a boot subscription) + add a boot-creates-nothing       |
|       test.                                                             |
|   extend Tests/Architecture/ClientRuntimePresentationBoundaryTests.cs   |
|       with a filesystem-API rule. Presentation is currently clean:      |
|       grep for System.IO|Directory.|File.|Path. returns nothing.        |
|                                                                         |
| --- MANIFEST AND BEHAVIOUR ------------------------------------------   |
|                                                                         |
| v1 SHAPE                                                                |
|   schemaVersion, projectId, title, goal, createdUtc,                    |
|   selectedMission, missionControlConversationId (null until Task 2).    |
|   No credentials, no transcript, no remote connection string.           |
|   Deliberately NOT adding assets / attachedContext / runs yet: the      |
|   spoke names them but defines no type for any of them, and Tasks       |
|   3/4 own their shape. schemaVersion is the sanctioned way to add       |
|   them then.                                                            |
|                                                                         |
| HOME DERIVATION                                                         |
|   goal -> first line, trimmed, truncated at a word boundary (60         |
|   chars) -> title. title -> lowercase, non-alphanumeric collapsed to    |
|   '-', trimmed, 40 chars, empty -> "project" -> slug.                   |
|   home = <user-profile>/Forge/Projects/<slug>.                          |
|   Purely deterministic, no model call. Title and location are shown     |
|   pre-filled and editable in the create form; only the goal is          |
|   required.                                                             |
|                                                                         |
| COLLISION                                                               |
|   If <slug> exists at all, take <slug>-2, <slug>-3, ...                 |
|   The manifest is written with FileMode.CreateNew, so a lost race       |
|   against a second Forge instance fails at the OS and advances to       |
|   the next suffix rather than overwriting. Bounded retries, then a      |
|   named error.                                                          |
|                                                                         |
| OPEN EXISTING FOLDER                                                    |
|   The directory must already exist; it is never created blind.          |
|   Manifest present -> discover it, preserving its projectId.            |
|   Absent -> create one there, title from the directory name, empty      |
|   goal.                                                                 |
|                                                                         |
| INVALID / MIGRATION                                                     |
|   schemaVersion greater than current -> refuse: "created by a newer     |
|   version of Forge". Malformed JSON or a missing required field ->      |
|   refuse, naming the offending path. Never silently overwrite or        |
|   repair a broken manifest. No older version exists, so there is no     |
|   migration path to write yet - and Task 1 writes a manifest only at    |
|   creation, so there is no lossy-rewrite risk. Task 2's                 |
|   conversation-ID write-back is where that starts to matter.            |
|                                                                         |
| --- SEQUENCING ------------------------------------------------------   |
|                                                                         |
|   1. Branch codex/phase-43.20-project-manifest.                         |
|   2. ProjectManifest + ProjectStore + unit tests, green before          |
|      anything else moves.                                               |
|   3. Transport DTOs, JSON context, route table.                         |
|   4. Endpoints, session-store and Program.cs cleanup; delete            |
|      DefaultWorkspace + its tests.                                      |
|   5. Home.razor first-use flow.                                         |
|   6. Rewrite/extend Presentation + boundary tests.                      |
|   7. dotnet build -> dotnet test -> make install (AOT).                 |
|   8. Update the spoke's Task 1 status with evidence, move the build     |
|      narrative to phase-43.20-project-workbench-mvp_completed.md,       |
|      open the PR to main.                                               |
|                                                                         |
| --- OPEN QUESTION (ONE) ---------------------------------------------   |
|                                                                         |
| "Most recent Project" auto-resume.                                      |
|   The spoke's Outcome says a person "opens Forge into their most        |
|   recent local Project", but Task 1's Done-when only requires that      |
|   reopening THAT home is the sole execution root, and forbids any       |
|   boot-time session. There is no recents record in the design, and I    |
|   will not invent a projects catalog (explicitly excluded).             |
|   My plan: Task 1 boots to the goal prompt with an "open an existing    |
|   project folder" alternative, and NO auto-resume.                      |
|   If you want resumption inside Task 1, the non-catalog option is a     |
|   lastOpenedUtc field in each manifest plus a scan of                   |
|   <profile>/Forge/Projects/* - say so and I will fold it in;            |
|   otherwise it belongs with Task 3's rail.                              |
|                                                                         |
| --- ASSUMPTIONS (proceeding unless corrected) -----------------------   |
|                                                                         |
|   1. Project create and open each return the project record AND the     |
|      Client Runtime session in one round-trip. A project cannot be      |
|      opened without its execution root, so two calls would only add     |
|      a broken intermediate state.                                       |
|   2. Mission switching keeps using the existing SessionSetupRequest     |
|      with the project home as its root.                                 |
|   3. selectedMission defaults to Janus, per the spoke's "initially      |
|      Janus".                                                            |
|                                                                         |
| Awaiting explicit approval before creating the branch or writing        |
| code.                                                                   |
+-------------------------------------------------------------------------+
````

## 3. Fault found

The plan concentrated on contracts and file operations but did not make the visual mock binding,

## Requested

Produce a build-ready plan for Phase 43.20 Task 1: create/open a project through the shared Client

## Better prompt

“Before planning, inventory the shared action contracts, binding visual references, theme tokens,

## Prevention

The task template should require a visual-reference inventory, the supported viewport range, a
