# Phase 48 — MAUI Desktop Host spike

> **Completed 2026-09-19.** The canonical GitHub Actions **Desktop build** publishes the same
> Supervisor topology for Windows ARM64 and macOS ARM64. Both downloaded artifacts were accepted.
> The design, implementation, and acceptance evidence are in
> [the completed spoke](phase-48-maui-desktop-host-spike_completed.md).

## Standard bundle steps

1. Start **Desktop build** in GitHub Actions for the candidate commit. It produces
   `forge-desktop-win-arm64` and `forge-desktop-osx-arm64`.
2. Download either artifact unchanged with `gh run download <run-id> -n <artifact-name>`. Both
   bundles are ZIPs. Verify the archive checksum against its `.sha256` sidecar.
3. Launch the platform's `ForgeMission.Desktop` with zero arguments and no `FORGE_*` overrides.
