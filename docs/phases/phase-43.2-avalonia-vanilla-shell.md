# Phase 43.2 — Avalonia vanilla shell

**Status: In build — Tasks 1–2 done 2026-07-26** (scaffold, AOT, agentic streaming, and streamed
tool-call loop verified); Tasks 3–5 remaining.
Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Depends on
[43.1](phase-43.1-tool-execution-engine.md) and
[43.7](phase-43.7-workspace-provider.md) (the workspace-root shape this spoke builds against).

## Design

The "meet people where they are" surface: a coding-agent chat UI that feels like Claude Code /
Codex, built once in Avalonia, running natively on macOS and Windows from one codebase. This spoke
deliberately does **not** attempt the debugger-style workbench yet — that's
[43.4](phase-43.4-ide-trace-surface.md), an iteration on top of this shell once it's proven.

Reference UX (what "vanilla" means here): a single chat pane, a compose box, streaming assistant
output, tool-call indicators (e.g. "Reading `Foo.cs`...", "Editing `Bar.cs`..." — the same
minimal-chrome signal every existing coding-agent TUI already gives), and a picker in the same
visual slot as a model dropdown — see [screenshot in the design conversation, 2026-07-25] — except
listing missions instead of models (wired for real in [43.3](phase-43.3-mission-attach-point.md);
this spoke can ship with a single hardcoded mission to prove the shell itself).

## Locked decisions carried from the hub

- New Avalonia project, `ForgeMission.Desktop` (or similar), referencing `ForgeMission.Core`
  directly (in-process — no `forge serve` hop for v1; revisit only if a browser/hosted surface
  needs to share the same backend later).
- Workspace root = whatever directory the user opens the app against, constructed as a
  [43.7](phase-43.7-workspace-provider.md) local-disk provider (not a bare string) and passed into
  [43.1](phase-43.1-tool-execution-engine.md)'s `AgenticSession`. Multi-root ("Add folder") is a
  43.7 open question, not decided here — this spoke can ship single-root as long as it's built
  against 43.7's interface, not a hardcoded path.
- **Native AOT — locked 2026-07-25.** Confirmed against
  [Avalonia's own Native AOT deployment guide](https://docs.avaloniaui.net/docs/deployment/native-aot):
  does not reduce UI/UX design options (full styling/theming/animation/Skia rendering all work),
  only constrains *how* the app is wired — same AOT-first discipline [AGENTS.md](../../AGENTS.md#aot-first--standing-rules-for-all-new-code)
  already mandates elsewhere in this repo, not a new burden:
  - Compiled bindings (`x:CompileBindings="True"`), not reflection bindings.
  - **CommunityToolkit.Mvvm, not ReactiveUI** — ReactiveUI's expression-tree/reflection-heavy
    bindings have documented AOT friction; Avalonia's own official template ships a
    CommunityToolkit.Mvvm variant for this reason.
  - No runtime-loaded/dynamic XAML, static resources, assets as embedded resources. Worth
    remembering for [43.4](phase-43.4-ide-trace-surface.md)'s dockable-panel workbench later if it
    ever wants user-customizable layouts loaded from disk — not a v1 concern.
  - Compile-time DI (register view models at startup), not reflection-based service location.
  - Third-party Avalonia controls need AOT vetting case-by-case; first-party `Avalonia.Controls` is
    fine. Matches this repo's existing "right-size deps" bias (Phase 40.1 design-system doc).
  - Design-time XAML preview/hot-reload is limited under AOT — a dev-workflow cost only, mitigated
    by developing in JIT/Debug (fast inner loop, hot reload works) and only AOT-publishing the
    release binary, same split .NET already does generally.

## Tasks

1. ✅ **Done 2026-07-26.** Scaffolded `ForgeMission.Desktop` from the official CommunityToolkit.Mvvm
   Avalonia template (`Avalonia`/`Avalonia.Desktop`/`Avalonia.Themes.Fluent`/`Avalonia.Fonts.Inter`
   `12.1.0`, `CommunityToolkit.Mvvm` `8.4.2`, `Microsoft.Extensions.DependencyInjection` `10.0.9` —
   no ReactiveUI), added to `ForgeMission.slnx`, referencing `ForgeMission.Core` directly. Compiled
   bindings everywhere (`x:CompileBindings="True"` on `App.axaml`, `MainWindow.axaml`, and the
   message `DataTemplate`'s own `x:DataType`), compile-time DI (`ServiceCollection` in
   `App.axaml.cs`, `MainWindowViewModel` constructor-injected — the view itself never resolves a
   service). `IsAotCompatible=true` added to `Core`/`Parser`/`Scout` (none had it before this task);
   the `Cli`'s existing `YamlDotNet` AOT-suppression comment/setting duplicated onto
   `ForgeMission.Desktop.csproj` verbatim, since `Desktop → Core → YamlDotNet` hits the identical
   warning. Chat view is a static placeholder (message list + compose box + local `Send`) — no
   agentic wiring, streaming, tool indicators, or packaging; correctly out of scope for this task.

   **Implemented by Codex, independently re-verified by Claude on this same machine/working tree**
   (same discipline as [43.7](phase-43.7-workspace-provider.md)):
   - `dotnet build src/ForgeMission.slnx` — reproduced clean (0 errors; 1 unrelated pre-existing
     warning in `ForgeMission.Rooms.Tests/PostgresFixture.cs`, a file this task never touched).
   - `dotnet test src/ForgeMission.Tests/ForgeMission.Tests.csproj` — reproduced independently:
     8 failed / 315 passed / 11 skipped, all 8 failures `ExecExpertRunnerTests` (the known
     pre-existing Windows subprocess `code 9009` baseline from 43.1/43.7) — zero regressions from
     this task.
   - Native AOT `win-arm64` publish: **could not be reproduced directly in this session** — both a
     Bash and a PowerShell attempt failed with `vswhere.exe` not found (its directory,
     `Program Files (x86)\Microsoft Visual Studio\Installer`, isn't on `PATH` in either tool
     session), even though the file exists on disk. This is a re-verification-environment gap, not
     a defect in the implementation: the publish output directory already contained a genuine
     20.3 MB `ForgeMission.Desktop.exe`, timestamped minutes before the commit, which **was**
     independently launched (`tasklist` confirmed a real running `ForgeMission.Desktop.exe` process,
     ~80 MB working set, consistent with a live Avalonia GUI process) and cleanly terminated.
     Physical evidence corroborates the reported publish + launch even though the build step itself
     couldn't be re-run here.
   - `ForgeMission.Rooms.Tests` failures (Docker/Testcontainers unreachable + two Windows file-lock
     cleanup failures) are environment-only and out of scope — that project touches none of the
     files this task changed.
2. ✅ **Done 2026-07-26.** Compose now opens a user-selected local workspace through Avalonia's
   `TopLevel.StorageProvider.OpenFolderPickerAsync`, then runs the bundled `missions/vanilla` mission
   through a fresh `AgenticSession` per Send. A new Core `IChatClientFactory` is the sole provider-client
   seam; Desktop registers the OpenAI-only `LocalKeyChatClientFactory`, which reads
   `~/.forge/credentials.json`'s `providers.openai.apiKey` via the AOT-safe `CredentialStore` source-gen
   path and never reads `ProviderProfile.ApiKey` or a provider-key environment variable. The compose box
   remains disabled until a folder is open; each Send is deliberately one-shot (prior visible messages are
   not mission context).

   Streaming uses `ContentWriter`, not `StepWriter`: the existing `MissionChatClient` precedent proves
   it carries only raw response chunks. While wiring it, a latent Core bug was fixed surgically in
   `DirectExpertRunner.StreamAsync`: tool-mode streaming now passes `ChatOptions.Tools`, rebuilds native
   conversation turns, skips the JSON-envelope instruction, buffers SDK updates while yielding text live,
   and uses the public `ChatResponseExtensions.ToChatResponse` aggregator before writing the existing
   `context["tool_calls"]` key. No `PipelineRunner` change was needed because it already extracts that
   key generically. `VanillaMissionSessionFactoryTests.StreamingToolCall_ReadsPlantedFile_StreamsFinalAnswer`
   verifies a streamed tool call, real local Read execution, and streamed continuation: 1 passed / 0 failed.
   The previously stale vanilla `mcl.lock` hash was regenerated with `forge init` so the normal expert-lock
   check can resolve the bundled mission. Build and Native AOT `win-arm64` publish both passed; the full
   suite remains blocked only by the known 8 Windows `ExecExpertRunnerTests` failures and this machine's
   unavailable Docker/Testcontainers Rooms-test environment.
3. Tool-call indicators — minimal inline rendering when a tool executes mid-turn (no need for a
   full diff view yet — that's 43.4's code pane).
4. Package for macOS (dev-signed is fine locally; defer notarization) and Windows (win-arm64,
   matching the existing release RID).
5. Dogfood checkpoint: run a real multi-tool coding task end-to-end in the shell on Mac.

## Done when

The Avalonia app opens a folder, accepts a prompt, streams a response, executes at least one real
tool call (file read/edit) visibly, and produces a working result — on macOS first, with a Windows
build (Surface ARM64 / Parallels) confirmed working within the same iteration (43.6 checkpoint).

## Open questions

- In-process `ForgeMission.Core` vs. `forge serve` loopback — locked toward in-process above, but
  worth a final check once streaming/cancellation semantics are prototyped (in-process gives free
  cancellation via `CancellationToken`; a server hop would need its own cancel-request wire-up).
