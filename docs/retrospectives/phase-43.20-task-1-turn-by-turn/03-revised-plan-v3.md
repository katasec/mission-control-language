# Turn 03 — Revised plan v3

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
| and SourceRoot/File carry a local path while
````

## 3. Fault found

The response described parity mostly as a test/proof. It did not yet make the shared Client Runtime

## Requested

Ensure Desktop and a future TUI expose the same underlying capability.

## Better prompt

“For every owned action, name the existing Client Runtime request, outcome, authorization, and

## Prevention

The handoff template should ask: “Which shared action contract performs this?” and reject a plan
