# Phase 49.4 — Local MCL seam cleanup: 49.4a evidence

> **Status:** Accepted 2026-09-22. Raw AOT/MAUI logs remain outside Git in
> `/private/tmp/phase49-4a-20260922-122500`.

## Scope and component fit

ForgeUI owns the browser/Rooms composition edge; CLI owns Native-AOT command composition. ForgeUI
has no CLI type, namespace, command, configuration, reflection, or runtime use, so its direct CLI
`ProjectReference` was deleted from `src/ForgeUI/ForgeUI.csproj`. The change advances ForgeUI's
documented ownership by removing an unrelated compile closure. No source moved and no runtime,
provider, package, route, UI, store, identity, Docker, workflow, or infrastructure behavior changed.

The task is security and default-path N/A: it adds no tier, entry point, credential, store, or
cross-context call, and it changes no user-visible/runtime/integration/deployment default.

## Acceptance observations

| Check | Observation |
|---|---|
| Scope | Diff contains the one `ForgeMission.Cli` project-reference deletion plus Phase 49 task/evidence documentation. `git diff --check` passed. |
| Source/evaluated graph | ForgeUI has no `ForgeMission.Cli` source use. Evaluated `ProjectReference` contains Core, Rooms, Rooms.Data, Runner.Contracts, Billing, and ConversationPresentation only. |
| ForgeUI build | `dotnet build src/ForgeUI/ForgeUI.csproj --no-restore` passed with 0 warnings and 0 errors. |
| Relevant contracts | `MessagesSerializationTests` passed 2/2; `RunContractsSerializationTests` passed 2/2. |
| Live provider recovery | Each `GrokWebSearchIntegrationTests` case passed individually: non-streaming 1/1 in 31 s; streaming 1/1 in 30 s. |
| Full build | `dotnet build src/ForgeMission.slnx --no-restore` passed with 0 warnings and 0 errors. |
| Full suite | `dotnet test src/ForgeMission.slnx --no-restore --no-build` passed 731, skipped 5, failed 0. |
| Independent review | PASS after the final build, full suite, and live Grok observations. |

## AOT and package comparison

The managed AOT roots remain CLI, Application Host, and Desktop Supervisor; ForgeUI is not in
their direct dependency closure. All observations below were compared with the accepted 49.2
baseline. No new warning class appeared.

| Root | First / immediate repeat | Output observation |
|---|---:|---|
| CLI AOT | 217.2 s / 3.282 s | 166,899,336 B both; executable SHA-256 identical across runs. |
| Application Host AOT | 43.508 s / 5.974 s | 87,085,760 B both; executable SHA-256 identical across runs. |
| Desktop Supervisor AOT | 8.006 s / 2.697 s | 44,530,904 B both; executable SHA-256 identical across runs and baseline size. |
| MAUI package | 30.003 s / 5.436 s | 18,710,601 B / 18,710,592 B; nine-byte package metadata variation only. |

## Rollback and program state

Rollback is a normal revert of the accepted merge commit, which restores only the removed
`ProjectReference`; no deployed artifact, package, identity, store, or migration exists to unwind.
The pre-program anchor remains `checkpoint-pre-repo-split-2026-09-22` at
`06e11af30b6eb17d2a3c32e1e5b883bbe42c0d51`. 49.4c remains unapproved; package and repository
extraction remain blocked by the Phase 49 decision ledger.
