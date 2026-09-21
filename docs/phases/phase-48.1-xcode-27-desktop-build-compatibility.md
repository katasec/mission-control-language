# Phase 48.1 — Xcode 27 Desktop build compatibility

> **Status: active 2026-09-21.** The previous macOS Desktop target selected the .NET 10
> Mac Catalyst 26.5 profile, which deliberately rejects the operator's Xcode 27 toolchain.
> This repair moves only the macOS native-host build profile and canonical GitHub Actions runner
> to the .NET-supported Xcode 27 profile. It changes neither Desktop product behavior nor its
> process topology.

## Scope and design

| Area | Decision |
|---|---|
| Product outcome | `make desktop` and the canonical macOS Desktop build produce the existing complete sibling bundle on Xcode 27. The user still launches the zero-argument `ForgeMission.Desktop` Supervisor. |
| Owner and boundary | `ForgeMission.Desktop.Host` remains the disposable MAUI native-window owner; `ForgeMission.Desktop` remains the supervisor. This task changes only their build-time target/path agreement, not their pipe contract, lifecycle, credentials, or runtime ownership. |
| macOS target | Use `net10.0-maccatalyst27.0` with the narrowly scoped `XCODE_27_0_PREVIEW` suppression required by the installed .NET 10 Apple workload. Its enforced Mac Catalyst deployment minimum is 17.0; the previous 15.0 profile cannot build against the Xcode 27 target. The Windows target and its 10.0.19041.0 minimum stay unchanged. |
| Bundle convention | The Makefile and the Supervisor development-path fallback name the same `net10.0-maccatalyst27.0` output folder. The published bundle remains `dist/forge-desktop`, containing `ForgeMission.Desktop`, `ForgeMission.Application.Host`, and `Forge.app`. |
| Canonical build | The macOS Desktop GitHub Actions job uses the dedicated `xcode-27` ARM64 runner. The unchanged Windows ARM64 job remains its independent release guard. |
| Non-goals | No MAUI UI, Application Host, pipe protocol, launch arguments, endpoints, credential route, runtime route, data contract, installer layout, or Windows behavior changes. |

Microsoft's .NET 10 Apple workload release documents the Xcode 27 profile as a target-framework
suffix plus the scoped preview warning suppression. The Xcode 27 profile also enforces Mac
Catalyst 17.0 as its deployment minimum; the operator approved that supported-platform change on
2026-09-21. This is a Type-2 build-toolchain/profile choice with a product compatibility effect:
a later stable workload may replace the preview suffix/suppression in this same bounded surface,
but it must retain the stated supported-platform fact or revise it deliberately. It introduces no
new configuration knob.

## Gates

| Gate | Review |
|---|---|
| Component fit | **PASS.** The Host project owns the MAUI target; the Supervisor owns its development bundle lookup; the Makefile and Desktop workflow own delivery. |
| Security Architecture | **PASS, N/A to tiering.** No bounded context, datastore, ingress, identity, credential, or cross-context contract changes. The existing local inherited-pipe and loopback boundary is unchanged. |
| Engineering Philosophy | **PASS.** One fixed supported macOS profile replaces an incompatible fixed profile. The build fails at the owning toolchain boundary rather than adding a runtime fallback or user-controlled switch. |
| Native AOT | **PASS.** The Application Host and Supervisor remain the same `osx-arm64` Native AOT publishes; the MAUI Host remains managed/self-contained. No reflection or trim-sensitive application code changes. |
| Desktop quality | **PASS.** Product requirement is unchanged: the Supervisor starts the disposable Host, which displays its existing local content then navigates to the owned loopback URL. The adapter, process/lifecycle owner, and replacement boundary are unchanged. |
| UI / visual reference | **N/A.** No rendered content, interaction, layout, token, or theme change is proposed. |

## Failure and default path

| Fact | Decision / proof required |
|---|---|
| Expected failure | The previous 26.5 Mac Catalyst profile rejects Xcode 27 before the native Host is produced; the Xcode 27 profile rejects the former 15.0 deployment minimum because its platform minimum is 17.0. |
| Owner and containment | The Host project/MSBuild target validation owns this build-time failure; no partial desktop bundle is an accepted artifact. |
| Recovery | The source-controlled 17.0 target, matching bundle lookup, and canonical runner select the compatible profile. A later stable workload may replace the preview suffix/suppression in this same bounded surface without silently lowering the stated baseline. |
| Default artifact | The canonical GitHub Actions **Desktop build** macOS ZIP and SHA-256 sidecar, downloaded unchanged. |
| Defaults | Launch `ForgeMission.Desktop` with zero arguments and no `FORGE_*` overrides. |
| Required observation | The downloaded macOS artifact’s Supervisor reaches the existing Host boot state and loopback UI, and Host close cleans up its supervised children. Local `make desktop` proves developer-toolchain compatibility only; it is not release acceptance. |

## Task 1 — align the Xcode 27 profile

Change only the macOS Host target/suppression, its two path consumers, and the macOS GitHub
Actions runner.

**Done when:**

1. Local Xcode 27 `make desktop` emits the complete macOS sibling bundle, including `Forge.app`.
2. The development Supervisor lookup names the same platform-specific output location.
3. `dotnet build src/ForgeMission.slnx` and `dotnet test src/ForgeMission.slnx` pass.
4. Desktop build passes for unchanged Windows and Xcode 27 macOS jobs.
5. The downloaded macOS artifact hash validates and its zero-argument default path is observed.

On completion, move the detailed evidence to
`phase-48.1-xcode-27-desktop-build-compatibility_completed.md`, leave a one-line pointer here,
and restore the Phase 45.5 next-step pointer in the plan hub.

## Task 2 — adopt the required UIKit scene lifecycle

The first successful local Xcode 27 bundle launch reached the MAUI Host, then macOS terminated it
before the existing boot content could appear. The named Console observation was:

```text
Application failed to launch: UIScene life cycle is required for apps built with this SDK.
```

| Area | Decision |
|---|---|
| Product requirement | The existing single native Host window opens and receives the same inherited pipe commands. No multi-window product capability is introduced. |
| Owner | `ForgeMission.Desktop.Host` owns the Apple lifecycle declaration. The Supervisor, Application Host, pipe protocol, and MAUI page remain unchanged. |
| Implementation | Add a Mac Catalyst `SceneDelegate` deriving from MAUI's `MauiUISceneDelegate`; add the matching `UIApplicationSceneManifest` and default scene configuration to the Host’s Mac Catalyst `Info.plist`. Declare `UIApplicationSupportsMultipleScenes` true so UIKit can create the required scene; the unchanged MAUI composition still opens one Host window. |
| Failure boundary | UIKit validates the bundle lifecycle before MAUI creates the page. A missing manifest/delegate terminates the disposable Host; the Supervisor observes its exit and cleans its children. The repair is confined to the Host-owned bundle configuration. |
| Reversal | Remove the delegate and manifest only if a later MAUI/Xcode profile again supports the application-delegate lifecycle. It cannot be removed while Xcode 27 requires scenes. |
| Non-goals | No lifecycle policy, native-window count, WebView content, UI token/layout, process ownership, runtime URL, credentials, or cross-context contract change. |

Microsoft's MAUI Mac Catalyst guidance uses this delegate/manifest pairing for scene-backed native
windows. The prior false support flag is superseded: the canonical artifact created then immediately
exited its sole scene without a native window. The required true flag permits that scene lifecycle;
the unchanged MAUI composition retains the product’s one Host window.

**Task 2 done when:** a fresh Xcode 27 `make desktop` bundle starts its zero-argument Supervisor
without the UIKit scene-lifecycle runtime issue; the existing Host boot content and loopback UI
are visibly observed, then closing the Host leaves no Supervisor-owned children. The canonical
Xcode 27 GitHub artifact must repeat that acceptance.

## Task 3 — retain inherited startup arguments across UIKit initialization

After Task 2’s scene configuration reached the packaged bundle, a fresh zero-argument Supervisor
launch opened the Host window but showed the Host-owned failure page:

```text
This process is started by ForgeMission.Desktop and requires inherited pipe handles.
```

The Host receives those arguments in Mac Catalyst `Program.Main(string[] args)`. The existing
controller instead reads `Environment.GetCommandLineArgs()` after UIKit has initialized the scene;
under the Xcode 27 scene profile that later API no longer retains the Supervisor arguments.

| Area | Decision |
|---|---|
| Product requirement | The same Supervisor-owned inherited command/event pipes reach the Host controller, so the first visible state is the existing Booting content rather than a missing-pipe failure. |
| Owner | The Mac Catalyst Host startup boundary owns capturing its own entrypoint arguments before calling `UIApplication.Main`. The Supervisor remains the only creator of pipes and the controller remains the only parser/consumer. |
| Implementation | Preserve an immutable copy of `Program.Main` arguments **after its executable-name element** in a narrowly named Host-startup value, register that value in MAUI composition, and make `DesktopHostController` parse it instead of the post-UIKit environment command line. This preserves the existing parser’s option/value pair alignment. |
| Security / contracts | The exact existing `--command-pipe <handle> --event-pipe <handle>` contract remains unchanged. No argument is logged, persisted, exposed to WebView content, or made configurable. No identity, secret, endpoint, datastore, or public entry point changes. |
| Failure boundary | A missing/malformed fixed pair still yields the existing Host-owned failure page. A valid pair must start the existing background pipe reader; a closed Supervisor pipe still exits the Host. |
| Non-goals | No argument syntax, retry semantics, process/lifecycle policy, pipe ownership, UI, runtime, WebView, or multi-window behavior change. |

This is a Type-2 adapter-startup repair. Its reversal is removal only if a future Mac Catalyst
scene profile again preserves `Environment.GetCommandLineArgs()` after UIKit initialization; the
direct `Program.Main` input remains the authoritative process boundary either way.

**Task 3 done when:** a local Xcode 27 packaged zero-argument launch visibly shows Booting and
then the existing loopback UI; a controlled direct Host launch without the pair still shows the
existing missing-pipe page; closing the Host removes the Supervisor, Application Host, and local
port-forward children. The same behavior is then observed from the validated canonical artifact.
