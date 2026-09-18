# Phase 48 — MAUI Desktop Host spike

> **Completed 2026-09-19.** Windows Forge Desktop now uses the MAUI Host without changing the
> Supervisor, Application Host, Presentation UI, or inherited-pipe protocol. The full record is
> in [the completed spoke](phase-48-maui-desktop-host-spike_completed.md).

## Outcome

- The MAUI Host is an unpackaged, Windows App SDK self-contained `win-arm64` executable.
- GitHub Actions **Desktop build** is the sole Windows bundle recipe. It builds the sibling bundle,
  validates deterministic Supervisor PE identity, and uploads the checksum-bearing artifact.
- The downloaded zero-argument bundle reached the Forge UI through the Supervisor-owned local
  Conversation tunnel and cleaned up every child when the MAUI window closed.

## Standard bundle steps

1. Start **Desktop build** in GitHub Actions for the candidate commit.
2. Download its `forge-desktop-win-arm64` artifact unchanged with
   `gh run download <run-id> -n forge-desktop-win-arm64`; verify the outer SHA-256 against GitHub,
   then the inner ZIP against its `.sha256` sidecar.
3. Launch `ForgeMission.Desktop.exe` with zero arguments and no `FORGE_*` overrides. The normal
   local dependency is `make -C ~/progs/forge-infra 350-conversation-kind-up`; the Supervisor owns
   its `kubectl port-forward` to `127.0.0.1:18080`.

## Routing

The Windows spike is complete. macOS native Host composition is deferred in
[the backlog](../backlog.md); do not package the Windows-only MAUI Host as a macOS Desktop.
