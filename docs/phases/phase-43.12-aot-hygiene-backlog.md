# Phase 43.12 — AOT hygiene backlog

**Status: Design — engineering backlog, not blocking.** Raised 2026-08-01 during
[43.11](phase-43.11-wasm-photino-shell.md) Batch A's Native AOT validation of `ForgeMission.ClientRuntime`/
`ForgeMission.Desktop` (the native desktop shell project, renamed 2026-08-01 from
`ForgeMission.ClientRuntime.Photino` — see [forge-architecture.md](../design/forge-architecture.md#naming-the-desktop-shell-project)).
Cross-cutting — not scoped to Phase 43 specifically
(`ForgeMission.Docker` is also compiled into `ForgeMission.Cli`, which predates this phase) — tracked
here because that's where the gaps were found. Same shape as [39.7](phase-39.7-exec-secret-isolation.md):
a real finding worth recording and resolving deliberately, not urgent enough to block the phase that
surfaced it.

## Why this exists

43.11 Batch A's AOT validation found two real, previously-undetected AOT bugs in code that had
already shipped and passed every existing test — both only surfaced by an actual `dotnet publish` +
runtime smoke test against the produced binary, not by `dotnet build` or the test suite (see
[43.11](phase-43.11-wasm-photino-shell.md)'s "AOT validated and enabled" section for the two that
were found and fixed). That track record is the reason the three items below are worth tracking
deliberately rather than assuming "it built, so it's fine."

## Backlog items

1. **`ForgeMission.Docker` isn't marked `IsAotCompatible`, despite already being compiled into two
   AOT executables** (`ForgeMission.Cli` and `ForgeMission.ClientRuntime`). Its actual JSON usage is
   already correct — every call site uses the existing `DockerJsonContext : JsonSerializerContext`
   source-gen context, confirmed by reading `DockerCli.cs` directly — so this isn't a live bug. It's
   a compiler-enforcement gap: every other library in the AOT closure (`Core`, `Parser`, `Scout`,
   `Serve`, `Billing`, `ClientRuntime.Transport`) has `<IsAotCompatible>true</IsAotCompatible>`, so a
   future reflection-based JSON call added to `Docker` would fail the build immediately (now that
   [`src/Directory.Build.props`](../../src/Directory.Build.props) treats warnings as errors); without
   the marker, the same mistake would build clean and only surface at publish+run time — exactly the
   two bugs this phase already found. **Fix:** add the marker, confirm a clean rebuild.

2. **The AOT smoke tests never exercised the default local-Docker startup
   path.** `ForgeMission.Desktop` resolves the Mission Runtime via
   `ForgeMission.Orchestration`'s `MissionRuntimeResolver`, defaulting to
   starting a real containerized Mission Runner via
   `LocalDockerMissionRuntimeLauncher` when `MissionRuntime:Mode` is unset
   or `"docker"` — but every AOT verification run in 43.11 isolated the
   transport plumbing from a real Docker startup instead. A read of
   `LocalDockerMissionRuntimeLauncher.cs` found nothing reflection-heavy
   (no YAML, no `Activator`, no unguarded JSON), but that's build-time
   inspection, not the runtime proof this phase has twice shown is
   necessary. **Fix:** run the actual AOT-published `ForgeMission.Desktop`
   binary with a real local Docker daemon, default mode, and confirm the
   full startup sequence (resolve and start the Mission Runner container,
   spawn `ForgeMission.ClientRuntime` pointed at its resolved URL, reach it
   over the wire) completes under AOT.

3. **(Awareness only, no action needed yet.) EF Core + Blazor Server are quarantined from the AOT
   path by convention, not enforcement.** `ForgeMission.Rooms`/`ForgeMission.Rooms.Data` (EF Core,
   dynamic proxies, expression trees — a materially harder AOT problem than a missing
   `JsonSerializerContext`, requiring compiled models) are reachable today only through `ForgeUI`
   (Blazor Server, deliberately JIT, confirmed via `grep` — nothing AOT references either project).
   Nothing structural stops a future change from wiring Rooms into something that ends up in the AOT
   closure. No fix needed now — just something to check before any future decision extends AOT
   further into ForgeUI/Rooms territory.

## Done when

Item 1 is fixed (marker added, clean rebuild confirmed) and item 2 has been run at least once against
a real Docker daemon with a result recorded here. Item 3 has no done-when — it stays a standing
awareness note until something changes the Rooms/ForgeUI boundary.
