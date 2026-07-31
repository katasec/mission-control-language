# Phase 43.11 — Blazor WASM UI shell + Photino native packaging

**Status: Design.** Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Depends on
[43.10](phase-43.10-transport-contract.md) (the UI needs a real channel to the Client Runtime before
it can do anything). Last in the prerequisite chain: 43.8 → 43.9 → 43.10 → **43.11**. Replaces the
now-superseded Electron + Blazor Server shell
([phase-43.2-electron-forge-desktop-shell.md](phase-43.2-electron-forge-desktop-shell.md)) as the
active desktop-shell spoke. [43.3 — Mission-as-attach-point](phase-43.3-mission-attach-point.md)
resumes on top of this, not the old shell.

## Design

Per [forge-architecture.md](../design/forge-architecture.md#native-host-ui-framework-and-the-verification-constraint):
the UI is Blazor WebAssembly, developed and verified primarily against a plain browser tab (CDP,
DevTools, Playwright, screenshots, hot reload — the same zero-setup loop already proven for
`ForgeUI`/Rooms). Photino wraps that same application for native packaging only — it is not the UI
framework and holds no business logic.

**What carries forward from the Electron/Blazor Server shell, unchanged:** the actual UI component
design work already done — the `forge.css` token application, the tool-call indicator states
(running/done glyphs, exact per-tool verb copy), the folder-open `+` composer menu — is UX/visual
design knowledge, not framework-specific code. [Task 3's target mockup](phase-43.2-electron-forge-desktop-shell.md)
and its component spec remain the correct visual target; only the rendering technology underneath
changes.

**What does not carry forward:** the Electron `main.cjs` process-spawning code
(`startHost`/`waitForHostAddress`/`confirmReady`/`hostProcess.kill()`), Blazor Server's SignalR
circuit, and the combined UI+tool-execution process shape. Those are specific to the superseded
architecture and are not migrated — this spoke builds their Photino/WASM equivalents fresh, per the
new layer split.

## Locked decisions

- **Development happens against a plain browser tab, always.** Any visual-verification task in this
  spoke (or downstream, e.g. 43.3/43.4) is done against the WASM app running as a normal localhost
  page — the same browser tooling already proven throughout this project. Photino is checked
  periodically for "does it still look right when packaged," not used as the primary dev-loop
  target — that would reintroduce the exact WKWebView/CDP verification gap this whole decision
  exists to avoid.
- **Photino's responsibilities are strictly limited to:** native window, native WebView, desktop
  lifecycle, packaging, and OS integration (menus, updater). Business logic, capability execution,
  and authorization all live in the Client Runtime ([43.8](phase-43.8-capability-provider-pattern.md)/
  [43.9](phase-43.9-client-runtime-authorization.md)), not in the Photino host.
- **Native dialogs (folder picker, etc.) are the Native Desktop Host's job**, not the WASM UI's and
  not the Client Runtime's — the WASM UI requests one through the Client Runtime API
  ([43.10](phase-43.10-transport-contract.md)'s channel), which is the same relationship the old
  Electron `contextBridge`/`forgeDesktop.pickFolder` pattern already established; only the
  implementation underneath changes.
- **`forge webui` and the packaged Photino app share the identical WASM UI and Client Runtime** —
  per [forge-desktop-client-runtime.md](../design/forge-desktop-client-runtime.md#forge-webui-redefinition),
  the only difference is chrome (native window vs. browser tab).
- **Photino maturity is a due-diligence gate before this spoke starts, not after.** Check current
  maintenance activity, Linux WebKitGTK support quality, and issue backlog health before committing
  implementation time — same bar already applied to Avalonia's Pro-tier control risk. If Photino
  turns out to be a poor bet, that's a design-stage finding, not something to discover mid-build.

## Tasks

1. **Due diligence: verify Photino's maturity** before anything else in this spoke starts. Check
   recent commit activity, open issue backlog, and specifically Linux (WebKitGTK) support quality —
   the platform most likely to be the weakest link for a smaller native-webview wrapper. Record the
   finding here before proceeding.
2. Scaffold the Blazor WebAssembly project — new UI components, not a port of the Electron shell's
   Razor markup (the underlying rendering model differs enough that a port risks carrying over
   Server-specific assumptions). Reuse `forge.css` directly, same as the superseded shell did.
3. Implement the WASM side of [43.10](phase-43.10-transport-contract.md)'s `IClientRuntimeChannel`
   consumption — the UI's only path to the Client Runtime.
4. Rebuild the composer, tool-call indicator, and response-card treatment against Task 3's existing
   target mockup and component spec (visual design carries forward; only the implementation
   technology changes).
5. Scaffold the Photino native host: window lifecycle, loading the WASM app's local origin, native
   folder-picker dialog exposed through the Client Runtime API contract.
6. **Verify against a plain browser first** (screenshot/inspect the WASM app running as a normal
   localhost page — full loop: open folder, send a prompt, see a real tool call, see the styled
   response). Only after that passes, verify the identical app once more through the packaged
   Photino build on macOS, to confirm the WKWebView residual-risk note in
   [forge-architecture.md](../design/forge-architecture.md) doesn't manifest in practice.

## Done when

The Blazor WASM UI, reached through `IClientRuntimeChannel`, opens a folder, accepts a prompt,
streams a response from the configured Mission Runtime, executes at least one real tool call
visibly (with correct per-tool glyphs/copy, matching Task 3's already-proven design), and produces
a working result — verified against a plain browser tab first, then confirmed once more through the
actual packaged Photino app on macOS. The Photino package has no business logic in it — everything
it does is window/lifecycle/packaging, confirmed by code review, not just by the app happening to
work.
