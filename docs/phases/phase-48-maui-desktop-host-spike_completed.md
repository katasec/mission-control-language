# Phase 48 — MAUI Desktop Host spike — completed

> **Completed 2026-09-19.** This Windows and macOS host replacement preserved the Application Host,
> Presentation UI, Supervisor, and pipe protocol. The compact historical handoff is the
> [Phase 48 hub](phase-48-maui-desktop-host-spike.md).

## Why this phase exists

Forge Desktop needs a dependable Windows-native shell. The existing Photino shell has not produced
an accepted Windows default-path result, while the local `m2` reference proves a minimal MAUI
Windows wrapper can own a normal WebView. This spike tests MAUI only as that thin host; it is not a
Blazor UI migration.

## Locked design

| Area | Decision |
|---|---|
| Native executable | `ForgeMission.Desktop.Host` is the ordinary Windows MAUI executable and the executable inside the Mac Catalyst `Forge.app` bundle. Its name stays unchanged, so the Desktop Supervisor and its child-process wiring stay unchanged. |
| Rendered content | MAUI `WebView` shows Host-owned Booting/Failed HTML first and navigates to the existing dynamic loopback URL only when the Supervisor sends `Navigate`. `ForgeMission.Application.Host` remains the sole embedded web server and continues serving `ForgeMission.Presentation` unchanged. |
| Host seam | `MauiDesktopHost` implements the existing `IDesktopHost` content/navigation/retry seam. MAUI owns its own application loop, so `IDesktopHost.Run()` is removed rather than simulated. |
| Retry | The Failed page navigates to the reserved `forge-retry://request` URL. The MAUI WebView cancels that navigation and emits the existing `RetryRequested` pipe event. No additional IPC or WebView message bridge is introduced. |
| Scope | Windows (`net10.0-windows10.0.19041.0`, `win-arm64`) and macOS (`net10.0-maccatalyst`, `maccatalyst-arm64`). The MAUI host is managed/self-contained, including Windows App SDK app-local deployment; it is not Native AOT. macOS publishes `Forge.app` beside the Supervisor. |
| Installer trial | A WiX MSI packages the already-published `dist/forge-desktop` folder without changing its files. It is an installation/distribution experiment only. |
| In-process trial | At the time of this spike, `ForgeMission.Desktop.Maui` was a separate Windows-only executable that owned the MAUI window and started the loopback Application Host in-process. It replaced neither the accepted legacy process topology nor the pipe Host during the spike. It was deleted on 2026-09-21; see [Phase 46.2 Task E](phase-46.2-task-e-retired-desktop-maui-trial_completed.md). |
| Standard Desktop build | GitHub Actions **Desktop build** is the source of testable Desktop bundles. It is manually runnable on any branch or commit and reusable by Release; it publishes the complete sibling bundle for Windows and macOS, retains each as a workflow artifact, and records its SHA-256. `make desktop-publish` is the implementation Actions calls, not a separate release source. |
| Windows AOT identity | Native AOT embeds a linker timestamp in the Supervisor PE header and debug-directory entries. The Windows publish step canonicalizes only those parsed fields, rejects an unexpected PE layout or byte change, and checks a clean repeat publish is byte-identical. |
| Local acceptance artifact | For a candidate commit, acceptance uses the unmodified `forge-desktop-win-arm64` or `forge-desktop-osx-arm64` artifact from **Desktop build**. For a release, it uses the same workflow's ZIP and checksum attached to the draft release. A local rebuild, copied executable, or overridden runtime route is not acceptance. |

## Architecture and gates

| Gate | Disposition |
|---|---|
| Component fit | **PASS.** `ForgeMission.Desktop.Host` owns the disposable native window and fixed command application. `ForgeMission.Desktop.Contracts` retains its dependency-free local-window seam. The Supervisor, Application Host, and Presentation retain their existing ownership. |
| Security Architecture | **PASS.** Type-2 local native-adapter replacement. The new host receives only inherited pipe handles and the supervisor-owned loopback URL; it holds no identity, credential, datastore, or new public entry point. Reversal is restoring the Photino project reference and host composition. |
| Engineering Philosophy | **PASS.** One MAUI host owns UI-thread dispatch and WebView behavior. The fixed two-command/one-event protocol is unchanged; no generic IPC, configuration switch, embedded server, or lifecycle abstraction is added. |
| Desktop interaction principles / UI system | **PASS.** No ForgeUI markup, interaction, design token, or reference surface changes. The existing application URL is rendered through the native WebView; packaged default-path inspection remains required. |
| Native AOT | **Exception, bounded.** MAUI Windows runs managed CoreCLR/ReadyToRun rather than Native AOT. The AOT rule remains unchanged for the Forge CLI, Supervisor, and Application Host. This spike adds no reflection-based application code. |
| Windows Supervisor identity | On Windows only, `make desktop-publish` normalizes the Desktop Supervisor's COFF build timestamp and every matching `IMAGE_DEBUG_DIRECTORY` timestamp after all three publish operations. The PowerShell normalizer validates the PE header, debug data directory, RVA-to-file mapping, and timestamp equality; it writes a fixed timestamp to a copy and replaces the image only after byte-diff validation. A second isolated Supervisor publish is normalized and must hash-identically match the shipped Supervisor. This preserves the Supervisor/Host/Application Host topology. |
| Windows delivery | **Desktop build** owns the only Windows packaging recipe. Release calls it, then attaches that exact ZIP and checksum to its draft release. The actionable trigger/download procedure is below. |
| Default path | **PASS.** The canonical Windows and macOS artifacts launch `ForgeMission.Desktop` with zero arguments and no `FORGE_*` overrides. Windows evidence shows boot content, dynamic-loopback navigation to the existing UI, and child cleanup; macOS evidence shows the downloaded Supervisor launching the staged `Forge.app` Host. |

## Tasks

| Task | Status | Done when |
|---|---|---|
| Replace the Photino implementation with the MAUI host | Verified | The existing host executable builds and runs the unchanged inherited-pipe protocol with an ordinary MAUI WebView. |
| Stabilize the Windows Supervisor image identity | Verified | `make desktop-publish` on Windows fail-closes unless the Supervisor is a valid PE whose COFF and debug-directory timestamps match, normalizes only those timestamp fields, and matches a separately published-and-normalized Supervisor byte-for-byte. Non-Windows publishes remain unchanged. |
| Standardize the Windows Desktop bundle | Verified | The reusable Desktop build workflow builds the complete Windows bundle, runs the Windows AOT identity guard, and uploads a named artifact and SHA-256 sidecar. Release attaches that exact artifact without rebuilding it; the procedure below is the sole future-agent instruction. |
| Publish the macOS MAUI Desktop bundle | Verified | The same MAUI Host publishes as a `maccatalyst-arm64` artifact from GitHub Actions and the downloaded Supervisor launches it on a Mac. |
| Default-path acceptance | Verified | The published zero-argument Windows artifact visibly starts the MAUI Host, uses the Supervisor-owned local Conversation tunnel, reaches the existing Forge UI, and exits through a normal MAUI window-close message with no remaining Host, Application Host, or port-forward child. |

## Standard bundle steps

1. For any candidate branch or commit, start **Desktop build** in GitHub Actions. Its Windows and
   macOS jobs build the same MAUI Desktop topology; a failed Windows AOT identity check publishes
   no usable Windows artifact. Future agents use this workflow rather than building a test candidate
   locally.
2. Download the candidate artifact with `gh run download <run-id> -n forge-desktop-win-arm64` or
   `gh run download <run-id> -n forge-desktop-osx-arm64`. For a release, download the matching
   archive and sidecar made by the same workflow. Both bundles are ZIPs. Verify the archive hash
   against its `.sha256` sidecar before extracting without changing files.
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
- Repository-wide tests are not accepted evidence: `dotnet test src/ForgeMission.slnx --no-build --no-restore` was blocked on 2026-09-18 because Docker/Testcontainers could not reach `npipe://./pipe/docker_engine`; it also exposed unrelated existing Windows test failures. The Windows publish guard above passed independently.
- The first GitHub Actions run (`35388053583`) reached the MAUI Host publish but failed with `NETSDK1147` because the hosted Windows runner lacked the MAUI workload. **Desktop build** now runs `dotnet workload restore` for that Host before publish; the next run is the first artifact candidate.
- The first downloaded ARM64 candidate (`35388528854`) passed the Supervisor pre-launch security check and started the Host, but the Host immediately fail-fast crashed in `CoreMessagingXP.dll` (`0xc0000602`). The bundle had no installed Windows App Runtime on the test laptop while the Host enabled the installed-runtime bootstrap. The next candidate changes the Host to Windows App SDK self-contained/app-local deployment and disables that bootstrap; it remains unaccepted until its downloaded artifact visibly renders.
- A controlled republish of only the Host into an isolated copy of that bundle, with Windows App SDK self-contained deployment and bootstrap disabled, kept the Supervisor and Host alive for 25 seconds with no new `CoreMessagingXP.dll` crash. This isolates the configuration fault, but it is not default-path acceptance: the next check must download and run a new GitHub Actions bundle unchanged.
- GitHub Actions **Desktop build** run [`35397280638`](https://github.com/katasec/mission-control-language/actions/runs/35397280638) passed on `main` commit `f44c5e4`. Its browser-downloaded ARM64 artifact matched GitHub's SHA-256 `758a8c06b6a9b373e9a3c0cecd5cfddddf329df21cde90d7fba6e7f256a53722`; its inner bundle SHA-256 matched its sidecar (`ba11a4562dc930515cc9988c7a74ef21f71860e48cc3c074c250c60dcc40ea61`). The zero-argument Supervisor and inherited-pipe MAUI Host were both alive after 45 seconds, with zero new Code Integrity events and zero new Host `CoreMessagingXP.dll` crash events. The visible Host showed the expected retry page because `127.0.0.1:18080/health` was unavailable.
- The local Kind cluster was recreated after Docker Desktop restarted. The old Windows bootstrap path could not hand Git Bash's `/dev/stdin` pseudo-file to native `kubectl.exe`; the follow-up `forge-infra` fix below replaces that delivery mechanism without changing the Desktop bundle.
- Local runtime bootstrap was fixed in `forge-infra` `main` commits `934f198`, `7966e06`, and `948f2d5`: native Windows `kubectl` now receives complete base64 Secret YAML on stdin rather than Git Bash's unsupported `/dev/stdin` pseudo-file, and the verifier probe ID has a portable fallback. On 2026-09-19 the canonical target passed both verifier roles and their cleanup barrier, rolled out `conversation-host` and `mission-worker`, and the Host's `/health` probe returned 200.
- The unchanged downloaded Desktop bundle was then launched with zero arguments. Its live tree was Supervisor → MAUI Host, Application Host, and Supervisor-owned `kubectl port-forward --address 127.0.0.1 --namespace forge-durable service/conversation-host 18080:8080`; `http://127.0.0.1:18080/health` returned 200, with zero new Code Integrity or Application crash events. The operator visually confirmed the Forge UI rendered.
- Final cleanup evidence: the same downloaded bundle was launched again with the Conversation health endpoint returning 200. Sending `CloseMainWindow()` to the MAUI Host returned `true`; within 20 seconds the Supervisor and all four children (MAUI Host, Application Host, `kubectl` port-forward, and `conhost`) were gone, and no listener remained on `127.0.0.1:18080`.
- GitHub Actions **Desktop build** run [`35463430366`](https://github.com/katasec/mission-control-language/actions/runs/35463430366) passed its Windows ARM64 and macOS ARM64 bundle jobs. The downloaded macOS artifact was launched through `ForgeMission.Desktop` with zero arguments on a Mac; the operator confirmed it worked.

## Investigation record

The controlled Windows Application Control and MAUI/WebView test matrix is retained in
[the completed investigation note](phase-48-maui-desktop-host-spike-investigation_completed.md).
