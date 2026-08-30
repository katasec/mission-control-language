# Phase 43.20 — Project Workbench MVP: completed work

> Build narrative and verification evidence for finished 43.20 tasks. The active spoke
> ([phase-43.20-project-workbench-mvp.md](phase-43.20-project-workbench-mvp.md)) keeps only the
> locked design later tasks still build against, plus a one-line status per completed task.

## Task 1 — Project home and local manifest (verified 2026-08-30)

### What shipped

`~/source/repos/0001` is gone. Desktop boots into a launcher that issues no request at all; a
Project — and only a Project — creates a local execution root.

| Piece | Where |
|---|---|
| Complete v1 manifest graph + source-generated JSON | `ForgeMission.ClientRuntime/Services/ProjectManifest.cs`, `ProjectManifestJsonContext.cs` |
| Sole owner of derivation, collision, validation, filesystem work | `ForgeMission.ClientRuntime/Services/ProjectStore.cs` |
| `ProjectDraftRequest` / `ProjectCreateRequest` / `ProjectOpenRequest`, `ProjectOperationResponse` | `ForgeMission.ClientRuntime.Transport/ClientRuntimeContracts.cs` |
| `transport/project/draft`, `.../create`, `.../open`; replacement-only `session/setup` | `ForgeMission.ClientRuntime/Transport/ClientRuntimeEndpoints.cs` |
| `CreateForProject` / `ReplaceAsync` split | `ForgeMission.ClientRuntime/Transport/ClientRuntimeSessionStore.cs` |
| Launcher (goal → draft → editable overrides → create; open; GoalRequired; Failed) | `ForgeMission.ClientRuntime.Presentation/Pages/Home.razor` |

Deleted: `Services/DefaultWorkspace.cs`, its tests, `/transport/session/default`,
`DefaultWorkspaceSessionRequest/Response`, the `Workspace:InitialRoot` configuration read, and the
dead scoped `WorkspaceState` registration with its unused `initialRoot` constructor parameter.

### Decisions made while building

**Create owns the final home, including a confirmed draft's.** Live browser verification caught
this: the launcher sends the drafted location back verbatim, so an ordinary create was taking the
"explicit home" branch and refusing a second `Todos API` instead of suffixing — the collision path
was unreachable from any surface that shows a draft before confirming it. The rule now lives in
`ProjectStore.Create`: a home directly inside `<user-profile>/Forge/Projects` is Forge-managed and
takes the next free `-2`/`-3`, while a home outside that root is a directory the person named
themselves and is used exactly (a collision there is `InvalidHome`, never a silent relocation).

**Expected failures are typed on the wire, exceptions only inside Client Runtime.**
`ProjectStore` throws `ProjectOperationException`; `ClientRuntimeEndpoints` maps it to
`ProjectOperationError { code, message }` in exactly one place. Unexpected faults (permissions, a
full disk) are deliberately not caught and fail the transport rather than being laundered into a
domain code.

**A rejected `SessionSetupRequest` is a 400, not a typed outcome.** It is a misuse/stale-race guard
no correct surface ever trips, so it fails loudly rather than becoming a renderable state.

**`GoalRequired` prefills from the response instead of issuing a second draft.** The proposal
already carries a runtime-derived home and title, so the extra round-trip would add nothing; the
plan's second draft call was dropped. No derivation moved into Presentation.

**Rootedness is checked before normalizing.** `Path.GetFullPath` silently resolves a relative path
against the Client Runtime's working directory, which is never a home a caller meant to name — the
first `Draft_RelativeHomeOverride_IsRejected` run failed on exactly that.

### Verification

| Observation | Result |
|---|---|
| `dotnet build src/ForgeMission.slnx` | Succeeded, 0 warnings, 0 errors |
| `dotnet test src/ForgeMission.slnx` | 834 passed, 11 skipped, 0 failed (551 + 139 + 97 + 42 + 5) |
| `ProjectStoreTests` (24 tests) | Draft purity, derivation table, collision, v1 completeness round-trip, every typed refusal |
| `ProjectTransportContractTests` (14 tests) | Surface-free: real Client Runtime process driven through `HttpClientRuntimeChannel`, no Blazor/bunit/Desktop/Host type |
| `HomeSessionOperationTests` (25 tests) | Zero-call boot, draft render/invoke, GoalRequired, Failed, replacement-only mission switch |
| `ClientRuntimePresentationBoundaryTests` (4 tests) | Presentation may use neither `HttpClient` nor any filesystem API |
| `make desktop-publish` (Native AOT, osx-arm64) | Published clean; `grep -icE "IL[0-9]{4}"` over the log = 0 |
| Empty profile + packaged Client Runtime + browser | Loading Desktop left `<profile>` completely empty — no directory, no session, no subscription |
| Live create of `Todos API` | One home at `<profile>/Forge/Projects/todos-api` with the v1 manifest above; the draft step alone created nothing |
| Live reopen of that home | Same title and home, used as the sole execution root; no second directory |
| Live second create of `Todos API` | `todos-api-2` with a new `projectId`; the first manifest's `projectId` unchanged |

The manifest written by the live run:

```json
{
  "schemaVersion": 1,
  "projectId": "f892ec18-a844-4ad4-b834-314c1b951e02",
  "title": "Todos API",
  "goal": "Todos API",
  "assets": [],
  "selectedMission": { "origin": "BuiltIn", "reference": "Janus", "digest": null },
  "attachedContext": [],
  "missionControlConversationId": null,
  "runs": []
}
```

### Gates

**Desktop Design and Implementation Quality Gate — PASS.** Behaviour: boot creates no directory,
session, subscription, or tool authority; one goal yields a named Project whose home is the sole
execution root. Owner: Client Runtime owns the manifest, every filesystem touch, every Project rule,
and the session-replacement rule; Presentation renders and invokes. Adapter: no `IDesktopHost`,
Photino, Supervisor, or native callback is involved — the three new routes sit on the runtime that
already owns local execution. Replacement boundary: no Host API, process-lifetime, or credential
change. Proof: the table above, including the packaged-app browser observation.

**Presentation-surface parity gate — PASS.** Draft, create, open, and session replacement are all
named Client Runtime contracts with typed outcomes and failures.
`ProjectTransportContractTests` is itself a second, non-Desktop surface exercising all four through
the production `IClientRuntimeChannel`; `ForgeMission.ClientRuntime.TransportProbe` was migrated to
the same project-create contract, so no out-of-process client can establish a root any other way.

### Notes for later tasks

- No recents index or auto-resume exists, by design. A later recent-project experience needs its own
  bounded design; nothing scans directories today.
- Task 2's conversation-ID write-back is the first *rewrite* of an existing manifest. Task 1 only
  ever writes at creation, which is why the full v1 graph is typed and round-trip-tested now.
- Test profile isolation is a property of the child process (`HOME`/`USERPROFILE` redirect in
  `ClientRuntimeHostProcess`), not a configuration knob in shipping code. Keep it that way.
