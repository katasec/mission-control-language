# Repository extraction — Conversations: completed evidence

## Destination bootstrap and owner transfer

The initial owner transfer is accepted on `forge-conversations` branch
`codex/extract-conversations`.

| Fact | Observation |
|---|---|
| Transfer | History-preserving filtered transfer commit `e5e5426` retained 67 relevant commits from MCL without modifying the source checkout. |
| Owner boundary | Contracts, Host, Worker, ConversationPresentation, their focused tests, Dockerfiles, build assets, and new isolated solution are in the destination. Host tests no longer reference ClientRuntime/Bob; boundary tests enforce destination-only project references. |
| Build/AOT | Destination restore and Release build passed with zero warnings/errors; Contracts AOT analysis passed. |
| Tests | Supervisor observation: `dotnet test ForgeMission.Conversations.slnx -c Release --no-build --no-restore` passed 175 Host and 18 Worker tests (193 total). |
| Images | Docker server `29.1.2` built/loaded `forge-conversation-host:local` (`c7eb0bc10b8fa14f0e083983a2f46420164b1ecc2e557834f6e75bd32cc6506d`) and `forge-conversation-worker:local` (`1b09d9a5c11925c1ccd90b96277087c1a15127a0834bf8b6adc87a3e98a810cc`). Supervisor inspection found entrypoints `dotnet ForgeMission.ConversationHost.dll` and `dotnet ForgeMission.ConversationWorker.dll`. |
| Package proof | Local `0.1.0` packages and symbol packages passed the audit: package identity/version, README/license, assembly/symbols, repository commit, and dependency shape. No package or image was published. |
| Default path | Pending by design: packages are unpublished, MCL consumers and `forge-infra` remain unchanged. The zero-argument Desktop/Kind acceptance belongs to the later cutover. |

`git diff --check` passed for the destination transfer and subsequent destination work.

## Destination package release

| Fact | Observation |
|---|---|
| Immutable release | Workflow-dispatch run [`36012050691`](https://github.com/katasec/forge-conversations/actions/runs/36012050691) built, tested, packed, audited, and pushed `Katasec.Forge.Conversations.Contracts` and `Katasec.Forge.ConversationPresentation`, each at `0.1.0`. |
| Registry outcome | Live GitHub Packages API observation after propagation confirmed both packages are private, associated with `katasec/forge-conversations`, and expose exactly one visible `0.1.0` version (Contracts ID `1290113494`; Presentation ID `1290113574`). |
| Release evidence repair | The original workflow's final 60-second propagation poll timed out despite the subsequent live PASS. [PR #2](https://github.com/katasec/forge-conversations/pull/2), merged as `a8949d5`, extends it to five minutes and emits final predicate diagnostics. Its CI verify job passed; it did not republish immutable versions. |

## MCL consumer package cutover

| Fact | Observation |
|---|---|
| Package seam | Application, Application.Transport, Presentation, ForgeUI, and `ForgeMission.Tests` now use the two released `0.1.0` Conversation packages. The atlas names `forge-conversations` as the external bounded owner. |
| Source removal | The six former Conversation source/test projects, both Conversation Dockerfiles, and their `ForgeMission.slnx` entries are absent. Supervisor structural test observation: 4 package-consumption tests passed. |
| Consumer proof | Normal MCL restore passed through its authorized private feed. Focused package/application/presentation tests passed 94/94; the remaining full MCL suite passed 450/450 with one existing Docker-runner test skipped. Application Host Native AOT publish produced `ForgeMission.Application.Host` for `osx-arm64` (29,022,432 bytes). |
| Negative proof | An isolated-cache, nuget.org-only restore failed closed with `NU1101` for `Katasec.Forge.Conversations.Contracts`; this controlled test is non-acceptance evidence and proves no local-project fallback. |
| Known environment limit | A full MCL Release build reaches all package consumers with zero warnings, but the untouched `ForgeMission.Desktop.Host` then fails local Mac Catalyst setup (`NETSDK1047` missing `maccatalyst-x64` assets; one retry reached an empty generated-AOT-object clang failure). Fresh restore returned to `NETSDK1047`; no source/config workaround was made. The full test suite and Application Host AOT publish above pass. |
