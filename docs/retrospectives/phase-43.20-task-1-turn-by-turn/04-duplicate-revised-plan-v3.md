# Turn 04 — Duplicate revised plan v3

## 1. Codex prompt

````text
+------------------------------------------------------------------+
| PLAN ADDENDUM REQUEST                                            |
+------------------------------------------------------------------+

Read the new “Presentation-surface parity” section in
docs/design/forge-architecture.md and the updated Task 1 Done-when
condition in docs/phases/phase-43.20-project-workbench-mvp.md.

Revise the plan without writing code:

- Add a Client Runtime transport/endpoint contract test for project create,
  open, and GoalRequired outcomes. It must exercise the shared transport
  DTOs/contracts, not Home.razor or any Desktop/Photino API.
- Keep bunit tests for Desktop interaction, but make them prove only that
  Home.razor renders state and invokes the same contract. It must own no
  Project business rule or filesystem behavior.
- Do not add a TUI in Task 1. The proof is that a future TUI can call the
  identical IClientRuntimeChannel contracts and receive the same result,
  authorization boundary, and failure semantics.

Reply with the revised implementation plan only, then wait for explicit
approval.
````

**Relay status:** No new instruction was sent for this duplicate response; the prior prompt is reproduced here because it remained the prompt in force.

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| REVISED IMPLEMENTATION PLAN v3 - Phase 43.20 Task 1 (Claude -> Codex)   |
| Adds Presentation-surface parity. No code written; no branch yet.       |
|                                                                         |
| --- WHAT CHANGED SINCE v2 -------------------------------------------   |
|                                                                         |
| A. New surface-free contract test drives create / open / GoalRequired   |
|    through the shared transport DTOs against the real Client Runtime    |
|    process. It is the parity proof; no TUI is added.                    |
| B. bunit tests are demoted to "renders state, invokes the contract".    |
|    Every Project rule (derivation, collision, validation, filesystem)   |
|    is asserted only in Client Runtime tests.                            |
| C. CONSEQUENCE OF PARITY - the create form no longer pre-fills a        |
|    derived title/home. Deriving them in Home.razor would be a           |
|    Desktop-only business path a TUI could not reuse, and boot may       |
|    make no call to fetch them. Title and location become OPTIONAL       |
|    OVERRIDE fields (placeholder: derived from your goal); when blank,   |
|    ProjectStore derives them and the response returns the values        |
|    actually used, which the surface then displays. The                  |
|    existing-directory GoalRequired path already carries its proposed    |
|    home/title in the response, so it stays pre-filled.                  |
|    This replaces v2's "shown pre-filled and editable" line.             |
|                                                                         |
| --- DESKTOP QUALITY GATE (re-affirmed, Q5 extended) ------------------  |
|                                                                         |
| Q1 Behaviour: boot creates no directory, session, subscription or       |
|    tool authority. One goal produces a named Project with a             |
|    deterministic home + complete v1 manifest; that home is the sole     |
|    local execution root.                                                |
| Q2 Owner: Client Runtime owns forge.project.json, every filesystem      |
|    touch, and every Project rule. Presentation renders state and        |
|    invokes named contracts. Supervisor and Host own none of this.       |
| Q3 Adapter: verified by reading ClientRuntimeEndpoints.cs,              |
|    HttpClientRuntimeChannel.cs, Home.razor -                            |
|    /transport/session/default is referenced only by Home.razor, the     |
|    channel route table, the JSON context and one test fake. No          |
|    IDesktopHost/Photino/Supervisor/native callback involved.            |
| Q4 Replacement boundary: no Host API, callback, process-lifetime or     |
|    credential change. Two new routes on the runtime that already owns   |
|    local execution.                                                     |
| Q5 Proof: ProjectStore unit tests; full-shape manifest round-trip; a    |
|    SURFACE-FREE transport contract test that exercises create, open     |
|    and GoalRequired with the same authorization/outcome/failure         |
|    semantics a TUI would see; bunit tests for zero-call boot, render    |
|    state and contract invocation; Presentation System.IO boundary       |
|    test; dotnet test + make install (AOT).                              |
| RESULT: PASS.                                                           |
|                                                                         |
| --- PARITY: WHAT PROVES A TUI COULD DO THIS -------------------------   |
|                                                                         |
| No TUI project is added in Task 1.                                      |
| New Tests/ClientRuntime/ProjectTransportContractTests.cs starts the     |
| real ForgeMission.ClientRuntime process and drives it through the       |
| production HttpClientRuntimeChannel / IClientRuntimeChannel using the   |
| shared transport DTOs. The test IS a second, non-Desktop surface: it    |
| references no Blazor, bunit, Photino, Desktop or Host type. Anything    |
| it can do, a TUI can do with the same code path.                        |
|                                                                         |
| Process isolation: it reuses the existing ClientRuntimeHostProcess      |
| helper from ClientRuntimeTransportOutOfProcessTests. I will extract     |
| that helper into one shared internal test file rather than copying      |
| it, so process start-up has a single owner.                             |
|                                                                         |
| Profile isolation without a production knob: the child process gets     |
| HOME (and USERPROFILE on Windows) pointed at a temp directory, so       |
| <user-profile>/Forge/Projects resolves inside the test's own sandbox    |
| and the developer's real home is never written to. This adds no         |
| config option to shipping code. .NET resolves SpecialFolder.            |
| UserProfile from those variables; I will confirm that empirically in    |
| step 2 by asserting the temp profile received the directory, and        |
| report it rather than assume it. If it does not hold on this            |
| platform, I will fall back to constructing the store with an explicit   |
| root behind the same contract and say so - not to a shipped knob.       |
|                                                                         |
| Contract cases covered surface-free:                                    |
|   create from goal only        -> deterministic home, complete v1       |
|                                   manifest, session + capabilities      |
|   create with explicit title/home override                              |
|   create colliding title       -> -2 suffix, both manifests intact      |
|   open a created Project       -> Opened, same projectId, root is the   |
|                                   Project home                          |
|   open a directory with no manifest -> GoalRequired + proposal,         |
|                                   nothing created on disk               |
|   open a nonexistent directory -> named failure, nothing created        |
|   invalid/newer manifest       -> named failure surfaced identically    |
|   a created Project's session can dispatch a capability (proving the    |
|   authorization boundary is the same one a TUI would get)               |
|                                                                         |
| --- bunit SCOPE (Desktop interaction only) --------------------------   |
|                                                                         |
| HomeSessionOperationTests keeps its fake IClientRuntimeChannel and      |
| asserts ONLY:                                                           |
|   - boot issues zero requests and zero subscriptions                    |
|   - the create form submits ProjectCreateRequest with exactly the       |
|     typed goal and any typed overrides, and sends nothing when the      |
|     goal is blank                                                       |
|   - a returned ProjectSession renders title/home and starts exactly     |
|     one subscription                                                    |
|   - a GoalRequired response renders the proposal's home/title and       |
|     issues no create until confirmed                                    |
|   - a named failure response renders as an error and leaves no          |
|     session or subscription                                             |
|   - mission switch sends SessionSetupRequest only with the open         |
|     Project home, only while a Project is open                          |
|   - existing replacement / cancellation / gap-notice coverage           |
| It asserts NO derived title, NO slug, NO collision suffix, NO           |
| manifest content, NO path - those exist only in Client Runtime tests.   |
| Structurally this holds because Home.razor computes none of them: it    |
| sends what the user typed and renders what the contract returned.       |
|                                                                         |
| --- 1. COMPLETE v1 MANIFEST SHAPE (unchanged from v2) ---------------   |
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
| Store-owned validation on read and before write: schemaVersion          |
| (newer -> refuse), non-empty projectId/title/goal, selectedMission      |
| present, asset relativePath normalized and contained within the home,   |
| and SourceRoot/File carry a local path while Artifact carries an        |
| opaque ID. Every failure names the manifest path.                       |
|                                                                         |
| --- 2. SELECTED MISSION ---------------------------------------------   |
|                                                                         |
| Initial { origin: BuiltIn, reference: "Janus", digest: null }.          |
| No content hash on the selection; localMissionContentHash lives only    |
| in ProjectLaunchSnapshot, written once by Task 4.                       |
|                                                                         |
| --- 3. GOAL IS NEVER EMPTY ------------------------------------------   |
|                                                                         |
| Open has two outcomes in one round-trip:                                |
|   manifest found  -> Opened: discovered, projectId preserved, session   |
|                      created rooted there.                              |
|   manifest absent -> GoalRequired: response carries that directory as   |
|                      the proposed home plus a title derived from the    |
|                      directory name. Nothing is created - no file, no   |
|                      session, no authority. The surface then runs the   |
|                      ordinary create call with that home.               |
| A nonexistent directory is a named error, never created blind. No       |
| code path persists an empty goal; the goal is validated in the store,   |
| not only in the UI, so every surface gets the same rule.                |
|                                                                         |
| --- 4. NO RECENTS, NO AUTO-RESUME -----------------------------------   |
|                                                                         |
| Boot makes zero channel calls: no open, no session, no subscription,    |
| no directory, no capability authority, and no profile-level index is    |
| written or scanned anywhere.                                            |
|                                                                         |
| --- 5. SESSIONSETUPREQUEST IS NOT A ROOT ESTABLISHER -----------------  |
|                                                                         |
| The free-path "+ Add folder" menu is removed. Project create and        |
| project open are the only calls that establish an initial session and   |
| each returns its Project + session data. SessionSetupRequest survives   |
| only to REPLACE a session for an already-open Project (mission          |
| switch): WorkspaceRoot is always the open Project's home and            |
| ReplacesSessionId is always set.                                        |
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
| Title/HomePath are optional overrides, not values a surface derives.    |
| ProjectSummary returns the values actually used, so every surface can   |
| display them without reimplementing derivation.                         |
| Failures are named errors carried on the response, so a TUI renders     |
| the same message text a Desktop banner shows.                           |
|                                                                         |
| --- FILES -----------------------------------------------------------   |
|                                                                         |
| CREATE (ForgeMission.ClientRuntime)                                     |
|   Services/ProjectManifest.cs           full v1 record graph + enums    |
|   Services/ProjectManifestJsonContext.cs                                |
|   Services/ProjectStore.cs              sole filesystem owner and       |
|       sole owner of derivation, collision, validation, named failures   |
|                                                                         |
| MODIFY                                                                  |
|   ClientRuntime.Transport/ClientRuntimeContracts.cs    swap DTOs        |
|   ClientRuntime.Transport/ClientRuntimeJsonContext.cs                   |
|   ClientRuntime.Transport/HttpClientRuntimeChannel.cs  route table      |
|   ClientRuntime/Transport/ClientRuntimeEndpoints.cs    two project      |
|       endpoints replace /transport/session/default                      |
|   ClientRuntime/Transport/ClientRuntimeSessionStore.cs drop             |
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
| --- DERIVATION AND COLLISION (Client Runtime only) ------------------   |
|                                                                         |
| goal -> first line, trimmed, word-boundary truncated at 60 -> title;    |
| title -> lowercase, non-alphanumeric collapsed to '-', trimmed, 40      |
| chars, empty -> "project" -> slug;                                      |
| home = <user-profile>/Forge/Projects/<slug>. Deterministic, no model.   |
| Collision: if <slug> exists at all, take <slug>-2, <slug>-3, ...        |
| The manifest is written with FileMode.CreateNew, so a race against a    |
| second Forge instance fails at the OS and advances to the next suffix   |
| rather than overwriting. Bounded retries, then a named error.           |
| ProjectStore takes its projects root as a constructor argument with a   |
| profile-resolving default - the same shape DefaultWorkspace.CreateNext  |
| uses today for its tests. No configuration knob is added.               |
|                                                                         |
| --- MIGRATION / INVALID MANIFEST ------------------------------------   |
|                                                                         |
| schemaVersion greater than 1 -> refuse ("created by a newer version     |
| of Forge"). Malformed JSON, a missing required field, or any            |
| validation failure -> refuse, naming the path. Never silently           |
| overwrite or repair a broken manifest. No older version exists, so no   |
| migration step is written yet; Task 1 writes a manifest only at         |
| creation. Task 2's conversation-ID write-back is where round-trip       |
| fidelity starts to matter, which is why the full v1 graph is typed      |
| now.                                                                    |
|                                                                         |
| --- TESTS -----------------------------------------------------------   |
|                                                                         |
| new Tests/ClientRuntime/ProjectStoreTests.cs (rules, in-process)        |
|   - create writes the complete v1 shape: typed empty assets /           |
|     attachedContext / runs, selectedMission BuiltIn+Janus+null          |
|     digest, null conversation ID, non-empty goal                        |
|   - derivation table: goal -> title -> slug, including the empty ->     |
|     "project" fallback and truncation                                   |
|   - deterministic home; collision -> -2, -3                             |
|   - full-shape round-trip: a hand-authored fixture populated with       |
|     runs, launchSnapshot, resolved experts, context and artifacts       |
|     parses and re-serializes losslessly (proves v1 completeness now,    |
|     not in Tasks 3/4)                                                   |
|   - open with manifest -> projectId preserved                           |
|   - open without manifest -> GoalRequired, directory unchanged          |
|   - open nonexistent -> named error, nothing created                    |
|   - newer schemaVersion / malformed JSON / empty goal / asset path      |
|     escaping the home -> named refusal, file untouched                  |
|                                                                         |
| new Tests/ClientRuntime/ProjectTransportContractTests.cs                |
|   (surface-free parity proof - the cases listed above, over the real    |
|   process and the shared DTOs; no Blazor/bunit/Desktop reference)       |
|                                                                         |
| refactor Tests/ClientRuntime/ClientRuntimeTransportOutOfProcessTests    |
|   extract ClientRuntimeHostProcess into one shared internal helper      |
|                                                                         |
| rewrite Tests/Presentation/HomeSessionOperationTests.cs                 |
|   (render/invoke assertions only, as scoped above)                      |
|                                                                         |
| extend Tests/Architecture/ClientRuntimePresentationBoundaryTests.cs     |
|   - marked Presentation projects may not use System.IO APIs             |
|     (Presentation is currently clean: grep for                          |
|     System.IO|Directory.|File.|Path. returns nothing)                   |
|                                                                         |
| delete Tests/ClientRuntime/DefaultWorkspaceTests.cs                     |
|                                                                         |
| I am not adding a source-scan test that tries to prove "Home.razor      |
| contains no Project rule" in general - it would be a fragile regex      |
| over prose-like markup. The enforceable parts are covered: no           |
| filesystem API in Presentation, and the surface-free contract test      |
| which fails the moment a rule leaks upward and stops working outside    |
| Home.razor.                                                             |
|                                                                         |
| --- SEQUENCING ------------------------------------------------------   |
|                                                                         |
|   1. Branch codex/phase-43.20-project-manifest.                         |
|   2. Manifest graph + JSON context + ProjectStore + ProjectStoreTests,  |
|      green before anything else moves. Confirm the HOME-redirect        |
|      isolation here and report the result.                              |
|   3. Transport DTOs, JSON context, route table.                         |
|   4. Endpoints, session-store and Program.cs cleanup; delete            |
|      DefaultWorkspace + its tests.                                      |
|   5. Host-process helper extraction + ProjectTransportContractTests     |
|      green BEFORE any Presentation work, so the contract is proven      |
|      without a UI in the loop.                                          |
|   6. Home.razor first-use flow (create / open / GoalRequired).          |
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
|   2. Transport DTO enums keep ClientRuntimeJsonContext's existing       |
|      numeric default; only the on-disk manifest uses string enums.      |
|      Both transport sides share one context, so nothing on the          |
|      existing wire format changes.                                      |
|                                                                         |
| No open questions remain. Awaiting explicit approval before creating    |
| the branch or writing code.                                             |
+-------------------------------------------------------------------------+
````

## 3. Fault found

This was a literal duplicate of turn 03. It consumed a relay/review turn without incorporating a

## Requested

Provide the updated Task 1 plan.

## Better prompt

“Before sending, compare the proposed reply with the previous relay. If unchanged, report that no

## Prevention

Require each revision to lead with a short changed-since-last-version list and refuse to resend an
