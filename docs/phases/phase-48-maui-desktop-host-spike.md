# Phase 48 — MAUI Desktop Host spike

> **Status:** implementation in progress. This is a Windows-only host replacement spike; it does
> not change the Application Host, Presentation UI, supervisor, or pipe protocol.

## Why this phase exists

Forge Desktop needs a dependable Windows-native shell. The existing Photino shell has not produced
an accepted Windows default-path result, while the local `m2` reference proves a minimal MAUI
Windows wrapper can own a normal WebView. This spike tests MAUI only as that thin host; it is not a
Blazor UI migration.

## Locked design

| Area | Decision |
|---|---|
| Native executable | `ForgeMission.Desktop.Host` becomes the ordinary Windows MAUI executable. Its name stays unchanged, so the Desktop Supervisor and its child-process wiring stay unchanged. |
| Rendered content | MAUI `WebView` shows Host-owned Booting/Failed HTML first and navigates to the existing dynamic loopback URL only when the Supervisor sends `Navigate`. `ForgeMission.Application.Host` remains the sole embedded web server and continues serving `ForgeMission.Presentation` unchanged. |
| Host seam | `MauiDesktopHost` implements the existing `IDesktopHost` content/navigation/retry seam. MAUI owns its own application loop, so `IDesktopHost.Run()` is removed rather than simulated. |
| Retry | The Failed page navigates to the reserved `forge-retry://request` URL. The MAUI WebView cancels that navigation and emits the existing `RetryRequested` pipe event. No additional IPC or WebView message bridge is introduced. |
| Scope | Windows only (`net10.0-windows10.0.19041.0`, `win-arm64` on this machine). The MAUI host is managed/self-contained, not Native AOT. Existing non-Windows host support is outside this spike. |
| Installer trial | A WiX MSI packages the already-published `dist/forge-desktop` folder without changing its files. It is an installation/distribution experiment only: it neither signs the package nor establishes Smart App Control trust. |
| In-process trial | `ForgeMission.Desktop.Maui` is a separate Windows-only executable that owns the MAUI window and starts the loopback Application Host in-process. It replaces neither the accepted legacy process topology nor the pipe Host during the spike. Reversal is deleting this project and its installer selection. |
| Standard Desktop build | GitHub Actions **Desktop build** is the source of testable Desktop bundles. It is manually runnable on any branch or commit and reusable by Release; it publishes the complete Windows sibling bundle, retains it as a workflow artifact, and records its SHA-256. `make desktop-publish` is the implementation Actions calls, not a separate release source. A macOS bundle is blocked until a macOS native Host composition is explicitly designed. |
| Windows AOT identity | Native AOT embeds a linker timestamp in the Supervisor PE header and debug-directory entries. The Windows publish step canonicalizes only those parsed fields, rejects an unexpected PE layout or byte change, and checks a clean repeat publish is byte-identical. This is not signing and does not claim a changed program body has the same Windows policy verdict. |
| Local acceptance artifact | For a candidate commit, Windows acceptance uses the unmodified `forge-desktop-win-arm64` artifact from **Desktop build**. For a release, it uses the same workflow's ZIP and checksum attached to the draft release. A local rebuild, copied executable, or overridden runtime route is not acceptance. macOS acceptance has no artifact yet because the MAUI Host is Windows-only. |

## Architecture and gates

| Gate | Disposition |
|---|---|
| Component fit | **PASS.** `ForgeMission.Desktop.Host` owns the disposable native window and fixed command application. `ForgeMission.Desktop.Contracts` retains its dependency-free local-window seam. The Supervisor, Application Host, and Presentation retain their existing ownership. |
| Security Architecture | **PASS.** Type-2 local native-adapter replacement. The new host receives only inherited pipe handles and the supervisor-owned loopback URL; it holds no identity, credential, datastore, or new public entry point. Reversal is restoring the Photino project reference and host composition. |
| Engineering Philosophy | **PASS.** One MAUI host owns UI-thread dispatch and WebView behavior. The fixed two-command/one-event protocol is unchanged; no generic IPC, configuration switch, embedded server, or lifecycle abstraction is added. |
| Desktop interaction principles / UI system | **PASS.** No ForgeUI markup, interaction, design token, or reference surface changes. The existing application URL is rendered through the native WebView; packaged default-path inspection remains required. |
| Native AOT | **Exception, bounded.** MAUI Windows runs managed CoreCLR/ReadyToRun rather than Native AOT. The AOT rule remains unchanged for the Forge CLI, Supervisor, and Application Host. This spike adds no reflection-based application code. |
| Windows Supervisor identity | On Windows only, `make desktop-publish` normalizes the Desktop Supervisor's COFF build timestamp and every matching `IMAGE_DEBUG_DIRECTORY` timestamp after all three publish operations. The PowerShell normalizer validates the PE header, debug data directory, RVA-to-file mapping, and timestamp equality; it writes a fixed timestamp to a copy and replaces the image only after byte-diff validation. A second isolated Supervisor publish is normalized and must hash-identically match the shipped Supervisor. This does not sign an image or alter the Supervisor/Host/Application Host topology. |
| Windows delivery | **Desktop build** owns the only Windows packaging recipe. Release calls it, then attaches that exact ZIP and checksum to its draft release. The actionable trigger/download procedure is below. |
| Default path | **Applies.** Publish the three sibling executables for `win-arm64` into `dist/forge-desktop`; run `ForgeMission.Desktop.exe` with zero arguments and no `FORGE_*` overrides. Passing evidence must show boot content, dynamic-loopback navigation to the existing UI, and cleanup when the window closes. On 2026-09-17, Windows Application Control blocked the unsigned published Supervisor before process launch; this is an environment-policy observation, not MAUI acceptance. |

## Tasks

| Task | Status | Done when |
|---|---|---|
| Replace the Photino implementation with the MAUI host | In progress | The existing host executable builds and runs the unchanged inherited-pipe protocol with an ordinary MAUI WebView. |
| Stabilize the Windows Supervisor image identity | In progress | `make desktop-publish` on Windows fail-closes unless the Supervisor is a valid PE whose COFF and debug-directory timestamps match, normalizes only those timestamp fields, and matches a separately published-and-normalized Supervisor byte-for-byte. Non-Windows publishes remain unchanged. |
| Standardize the Windows Desktop bundle | In progress | The reusable Desktop build workflow builds the complete Windows bundle, runs the Windows AOT identity guard, and uploads a named artifact and SHA-256 sidecar. Release attaches that exact artifact without rebuilding it; the procedure below is the sole future-agent instruction. |
| Design the macOS Desktop bundle | Open design decision | Choose and document a macOS native Host composition before adding a macOS GitHub Actions artifact. Do not package the Windows-only MAUI Host or a bare Photino adapter as a purportedly runnable macOS Desktop. |
| Default-path acceptance | Blocked by WDAC | The published zero-argument Windows artifact visibly reaches the existing Forge UI and exits without orphaned children. |

## Standard bundle steps

1. For any candidate branch or commit, start **Desktop build** in GitHub Actions. Its Windows job
   builds the full bundle; a failed AOT identity check publishes no usable artifact. Future agents
   use this workflow rather than building a test candidate locally.
2. Download the candidate artifact with `gh run download <run-id> -n forge-desktop-win-arm64`.
   For a release, download the ZIP and sidecar made by the same workflow with
   `gh release download v<version> --pattern 'forge-desktop-win-arm64.zip*'`. Run
   `Get-FileHash .\forge-desktop-win-arm64.zip -Algorithm SHA256` and compare it with the
   accompanying `.sha256` file, then extract it without changing files. macOS has no equivalent
   command until its Host composition is designed and built.
3. Launch the downloaded `ForgeMission.Desktop` with zero arguments and no `FORGE_*` overrides.
   Record the default-path result: boot state, navigation to the loopback UI, and child cleanup.
4. Only after that observation may the same commit's Desktop bundle be considered for distribution.
   The workflow artifact is a testable bundle, not a claim that Windows will accept every new
   program-body hash.

## Implementation evidence

- `make OS=Windows_NT desktop-publish` on 2026-09-18 normalized the published Supervisor and an isolated repeat publish to SHA-256 `7df4e4ff164e30a89bc712e80186b1b29a9ecf7db63f70fe9c1a3450610c8682`; `cmp -s` passed.
- The validated locations were the COFF timestamp at byte 264 and the three PE debug-directory timestamps at bytes 5,228,148, 5,228,176, and 5,228,204. No other bytes changed.
- The PowerShell verifier rejected both a malformed file and a copied image with one mismatched debug-directory timestamp. A copied MRE image with one debug record also normalized and verified.
- Directly starting the normalized published Supervisor with `not-a-url` printed its expected usage and exited 1. This is only a process-start check; zero-argument Desktop default-path acceptance remains open.

## Investigation record

The controlled Windows Application Control and MAUI/WebView test matrix is retained in
[the active investigation note](phase-48-maui-desktop-host-spike-investigation.md). The active
work remains the default-path observation, a successful workflow artifact, and a visibly rendering
managed MAUI WebView.
