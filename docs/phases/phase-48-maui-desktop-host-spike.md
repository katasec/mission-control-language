# Phase 48 — MAUI Desktop Host spike

> **Active compatibility repair, 2026-09-21.** The accepted spike remains recorded in
> [the completed spoke](phase-48-maui-desktop-host-spike_completed.md). The current Xcode 27
> build-profile repair is scoped in
> [the compatibility spoke](phase-48.1-xcode-27-desktop-build-compatibility.md).

## Standard bundle steps

1. Start **Desktop build** in GitHub Actions for the candidate commit. It produces
   `forge-desktop-win-arm64` and `forge-desktop-osx-arm64`.
2. Download either artifact unchanged with `gh run download <run-id> -n <artifact-name>`. Both
   bundles are ZIPs. Verify the archive checksum against its `.sha256` sidecar.
3. Launch the platform's `ForgeMission.Desktop` with zero arguments and no `FORGE_*` overrides.
