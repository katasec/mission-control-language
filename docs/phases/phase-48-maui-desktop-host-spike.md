# Phase 48 — MAUI Desktop Host spike

> **Windows accepted 2026-09-19; macOS artifact publication in progress.** The same MAUI Host
> serves both targets without changing the Supervisor, Application Host, Presentation UI, or
> inherited-pipe protocol. The Windows completion record is in
> [the completed spoke](phase-48-maui-desktop-host-spike_completed.md).

## Outcome

- The MAUI Host is an unpackaged Windows App SDK self-contained `win-arm64` executable and a
  Mac Catalyst `maccatalyst-arm64` application bundle.
- GitHub Actions **Desktop build** is the sole bundle recipe. It builds the same sibling process
  topology on Windows and macOS, validates deterministic Supervisor PE identity on Windows, and
  uploads a checksum-bearing artifact for each platform.
- The downloaded zero-argument bundle reached the Forge UI through the Supervisor-owned local
  Conversation tunnel and cleaned up every child when the MAUI window closed.

## Standard bundle steps

1. Start **Desktop build** in GitHub Actions for the candidate commit. It produces
   `forge-desktop-win-arm64` and `forge-desktop-osx-arm64`.
2. Download either artifact unchanged with `gh run download <run-id> -n <artifact-name>`. Both
   bundles are ZIPs. Verify the outer SHA-256 against GitHub, then the archive checksum against
   its `.sha256` sidecar.
3. Launch the platform's `ForgeMission.Desktop` with zero arguments and no `FORGE_*` overrides.
   The normal local dependency is `make -C ~/progs/forge-infra 350-conversation-kind-up`; the
   Supervisor owns its `kubectl port-forward` to `127.0.0.1:18080`.

## Routing

Windows default-path acceptance is complete. macOS uses the same MAUI Host and is published by the
same GitHub Actions workflow. Its bundle-and-Supervisor launch smoke is complete; full macOS
default-path acceptance remains deferred for normal sign-in and Conversation Runtime readiness.

## macOS bundle-layout repair

Mac Catalyst defaults to creating an installer package during `dotnet publish`; its direct
application output is `Forge.app`. The Host publish sets `CreatePackage=false` so the published
Desktop folder contains that app bundle beside the Supervisor. `make desktop` remains the sole
local entry point, and the Supervisor starts
`Forge.app/Contents/MacOS/ForgeMission.Desktop.Host` through the existing inherited-pipe protocol.

This is a Type-2 local packaging repair. It adds no public entry point, identity, runtime route,
service, or UI change; visual-reference and Presentation-parity gates are N/A. A failed Host
publish fails the build rather than emitting a bundle that claims to be runnable. Done when a fresh
`make desktop` leaves the expected app bundle beside `ForgeMission.Desktop`, and launching
that Supervisor starts the Host. Full macOS default-path acceptance remains the existing later
check, including normal sign-in and Conversation Runtime readiness.
