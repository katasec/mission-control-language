# Turn 08 — Implementation summary v2

## 1. Codex prompt

````text
+------------------------------------------------------------------+
| REVIEW CORRECTION — TASK 1 NOT YET APPROVED                      |
+------------------------------------------------------------------+

Do not merge PR #78.

Fix the P1 validation bug in ProjectStore.DeriveTitle:
a non-empty title override currently returns before validating goal, so
Create("", "Title", ...) and Draft("", "Title", ...) accept an empty goal.
The locked contract requires a non-empty goal regardless of overrides.

Required changes:
- Validate goal before any title-override return.
- Add direct ProjectStore tests for empty/whitespace goal with a non-empty
  title override, for both Draft and Create.
- Add the corresponding surface-free transport contract coverage.
- Re-run the focused tests plus the full build/test/AOT verification.

Documentation correction:
- Task 1 must not be marked “Done and verified” in the active spoke, and
  its completed narrative must not claim verification, until Codex has
  approved the corrected implementation summary.
- Keep Phase 43’s overall status “in progress.”

Reply with an updated IMPLEMENTATION SUMMARY, including the exact verification
results. Wait for approval before merging.
````

## 2. Claude response

````text
+-------------------------------------------------------------------------+
| IMPLEMENTATION SUMMARY v2 - Phase 43.20 Task 1 (Claude -> Codex)        |
| P1 goal-validation bug fixed. PR #78, 2 commits, pushed, tree clean.    |
| NOT merged. Task 1 is recorded as implemented and awaiting review, not  |
| done, in both the active spoke and its completed narrative.             |
|                                                                         |
| --- THE P1 FIX ------------------------------------------------------   |
|                                                                         |
| You were right, and it was reachable in production, not just in tests.  |
| ProjectStore.DeriveTitle returned a non-empty title override BEFORE the |
| goal gate, so Draft("", "Title", ...) and Create("", "Title", ...) were |
| both accepted - and create would have written a manifest whose goal was |
| empty, breaking the locked "goal is never empty in a persisted          |
| manifest" rule.                                                         |
|                                                                         |
| The gate is now a separate RequiredGoal guard that both entry points    |
| run first, before any override is consulted:                            |
|                                                                         |
|   public ProjectHomeProposal Draft(goal, titleOverride, homeOverride)   |
|       var title = DeriveTitle(RequiredGoal(goal), titleOverride);       |
|   public ProjectRecord Create(goal, titleOverride, homeOverride)        |
|       var required = RequiredGoal(goal);                                |
|       var title = DeriveTitle(required, titleOverride);                 |
|                                                                         |
| DeriveTitle is now purely a title choice; it can no longer decide       |
| whether a Project may exist. RequiredGoal returns the trimmed goal too, |
| so every create path persists that one validated value - the explicit-  |
| home branch no longer re-trims its own copy.                            |
|                                                                         |
| --- RED-THEN-GREEN PROOF --------------------------------------------   |
|                                                                         |
| I did not want to claim coverage I had not watched fail, so I stashed   |
| the fix and ran the new tests against the committed buggy store:        |
|                                                                         |
|   git stash push -- .../ClientRuntime/Services/ProjectStore.cs          |
|   dotnet test --filter FullyQualifiedName~ProjectStoreTests             |
|     Failed! - Failed: 8, Passed: 28, Total: 36                          |
|     Draft_EmptyGoal_IsRejectedEvenWithANonEmptyTitleOverride("")   FAIL |
|     Draft_EmptyGoal_IsRejectedEvenWithANonEmptyTitleOverride("   ")FAIL |
|     Draft_EmptyGoal_IsRejectedEvenWithANonEmptyTitleOverride(nl)  FAIL  |
|     Draft_EmptyGoal_IsRejectedWithBothATitleAndAHomeOverride       FAIL |
|     Create_EmptyGoal_IsRejectedEvenWithANonEmptyTitleOverride           |
|         _AndCreatesNothing("", "   ", "\n\t")                  3x FAIL  |
|     Create_EmptyGoal_IsRejectedWithATitleAndHomeOverride                |
|         _AndWritesNoManifest                                      FAIL  |
|                                                                         |
|   git stash pop                                                         |
|   dotnet test --filter ProjectStoreTests|ProjectTransportContractTests  |
|     Passed! - Failed: 0, Passed: 53, Total: 53                          |
|                                                                         |
| --- NEW COVERAGE ----------------------------------------------------   |
|                                                                         |
| ProjectStoreTests (+8)                                                  |
|   Draft_EmptyGoal_IsRejectedEvenWithANonEmptyTitleOverride              |
|       [Theory] "", "   ", "\n\t"                                        |
|   Draft_EmptyGoal_IsRejectedWithBothATitleAndAHomeOverride              |
|   Create_EmptyGoal_IsRejectedEvenWithANonEmptyTitleOverride             |
|       _AndCreatesNothing [Theory] "", "   ", "\n\t"                     |
|   Create_EmptyGoal_IsRejectedWithATitleAndHomeOverride                  |
|       _AndWritesNoManifest                                              |
|   The last one exists because the explicit-home branch skips slug       |
|   derivation entirely, so it needs its own proof that the gate fires.   |
|                                                                         |
| ProjectTransportContractTests (+3, surface-free, real process)          |
|   Draft_AnEmptyGoalWithATitleOverride_IsStillATypedFailure              |
|   Create_AnEmptyGoal_IsATypedFailure_AndWritesNoProject                 |
|   Create_AnEmptyGoalWithTitleAndHomeOverrides_IsStillATypedFailure      |
|   Each asserts the typed InvalidGoal code AND that nothing was written. |
|                                                                         |
| --- EXACT VERIFICATION RESULTS --------------------------------------   |
|                                                                         |
|   dotnet build src/ForgeMission.slnx                                    |
|       Build succeeded. 0 Warning(s), 0 Error(s).                        |
|   dotnet test src/ForgeMission.slnx                                     |
|       845 passed, 11 skipped, 0 failed                                  |
|       Tests 562, ConversationHost 139, Rooms 97, Worker 42, Runner 5    |
|       (was 834 passed; +11 from the new regression coverage)            |
|   Focused: ProjectStoreTests + ProjectTransportContractTests            |
|       53 passed, 0 failed                                               |
|   make desktop-publish (Native AOT, osx-arm64)                          |
|       exit 0; grep -icE "IL[0-9]{4}" over the log = 0                   |
|   make install (CLI AOT publish)                                        |
|       exit 0; ILC warning count = 0                                     |
|   Live, against the freshly published binary on an empty profile:       |
|       POST /transport/project/create {"goal":"   ","title":"Sneaky"}    |
|         -> {"outcome":3,"session":null,"proposal":null,                 |
|             "error":{"code":0,                                          |
|                 "message":"A goal is required to create a Project."}}   |
|       POST /transport/project/draft {"goal":"","titleOverride":"X"}     |
|         -> {"draft":null,"error":{"code":0,                             |
|              "message":"A goal is required to create a Project."}}      |
|       Projects on disk afterwards: todos-api only. No "sneaky" home, no |
|       manifest, no session.                                             |
|   ~/Forge still does not exist on this machine.                         |
|                                                                         |
| One inconclusive attempt, reported rather than dropped: I first tried   |
| to reproduce the bug through the launcher by drafting and then clearing |
| the goal field. The clear did not register in the Blazor binding, so    |
| create carried the original goal and simply made a normal Project - it  |
| proved nothing either way. The wire check above is the observation.     |
|                                                                         |
| --- DOCUMENTATION CORRECTION ----------------------------------------   |
|                                                                         |
|   docs/phases/phase-43.20-project-workbench-mvp.md                      |
|       Task 1 heading is now "implemented, awaiting review", with an     |
|       explicit "Not yet approved - mark done only after Codex approves  |
|       the corrected summary and its PR merges."                         |
|   docs/phases/phase-43.20-project-workbench-mvp_completed.md            |
|       Section retitled "(implemented, awaiting review)" with a          |
|       not-approved-yet callout; "Verification" is now "Local            |
|       verification (pre-approval)"; the P1 bug and its fix are recorded |
|       under Decisions made while building.                              |
|   docs/phases/phase-43-forge-desktop.md                                 |
|       43.20 row: "In progress. The local Project record is implemented  |
|       and awaiting review"; phase status stays In progress.             |
|   docs/plan.md                                                          |
|       NEXT STEP: the local Project record is awaiting review; durable   |
|       Project Mission Control is next. Phase 43 status: "Project        |
|       Workbench MVP in progress" (one row, no sub-phase numbers).       |
|                                                                         |
| --- GATES (unchanged, re-affirmed after the fix) ---------------------  |
|                                                                         |
|   PRESENTATION-SURFACE PARITY GATE: PASS                                |
|       draft, create, open, and session replacement remain named Client  |
|       Runtime contracts with typed outcomes; the goal rule now holds on |
|       every one of them regardless of overrides, and the surface-free   |
|       contract tests prove it over the real process with no Desktop     |
|       type involved.                                                    |
|   DESKTOP DESIGN AND IMPLEMENTATION QUALITY GATE: PASS                  |
|       No Host, Supervisor, adapter, process-lifetime, or credential     |
|       surface changed. The fix is four lines inside the class that      |
|       already owned every Project rule.                                 |
|                                                                         |
| --- UNCHANGED FROM v1 -----------------------------------------------   |
|                                                                         |
| Files, contracts, deviations 1-5, assumptions, and open follow-ups are  |
| as reported in the previous summary; only ProjectStore's goal gate, its |
| tests, and the four status docs changed. The earlier deviation list     |
| still stands, including create owning the final home for a confirmed    |
| draft, and the 400-not-typed-outcome choice for a rejected              |
| SessionSetupRequest.                                                    |
|                                                                         |
| PR #78 remains open and mergeable, no required checks configured.       |
| Awaiting your approval before merging to main.                          |
+-------------------------------------------------------------------------+
````

## 3. Fault found

The original implementation allowed title input to mask an empty goal. The missing negative case

## Requested

Correct the discovered empty-goal validation flaw and report the updated implementation.

## Better prompt

“Map every named precondition to a positive and negative test before changing code; include empty,

## Prevention

Derive negative tests directly from each named precondition in the spoke before coding: a goal is
