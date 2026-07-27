# Phase 43.2 — Avalonia vanilla shell (completed/shelved narrative)

> **This is the full build narrative for the shelved Avalonia spike.** The active pointer is
> [phase-43.2-avalonia-vanilla-shell.md](phase-43.2-avalonia-vanilla-shell.md); the new active spoke
> is [phase-43.2-electron-forge-desktop-shell.md](phase-43.2-electron-forge-desktop-shell.md). Kept
> in full here because Tasks 1–3's verified evidence (real agentic streaming, real tool execution,
> tool-call indicators) is real, reusable proof that the underlying loop works — only the UI
> framework it was built in is being dropped.

Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Depended on
[43.1](phase-43.1-tool-execution-engine.md) and [43.7](phase-43.7-workspace-provider.md) (the
workspace-root shape this spoke was built against).

## Design

The "meet people where they are" surface: a coding-agent chat UI that feels like Claude Code /
Codex, built once in Avalonia, running natively on macOS and Windows from one codebase. This spoke
deliberately did **not** attempt the debugger-style workbench — that stayed
[43.4](phase-43.4-ide-trace-surface.md), an iteration on top of this shell once it was proven.

Reference UX (what "vanilla" meant here): a single chat pane, a compose box, streaming assistant
output, tool-call indicators (e.g. "Reading `Foo.cs`...", "Editing `Bar.cs`..." — the same
minimal-chrome signal every existing coding-agent TUI already gives), and a picker in the same
visual slot as a model dropdown — see [screenshot in the design conversation, 2026-07-25] — except
listing missions instead of models (wired for real in [43.3](phase-43.3-mission-attach-point.md);
this spoke shipped with a single hardcoded mission to prove the shell itself).

## Locked decisions carried from the hub (at the time)

- New Avalonia project, `ForgeMission.Desktop` (or similar), referencing `ForgeMission.Core`
  directly (in-process — no `forge serve` hop for v1; revisit only if a browser/hosted surface
  needs to share the same backend later). **Superseded** — the Electron/Blazor-Server pivot
  reopened this exact question; see
  [forge-desktop-client-runtime.md](../design/forge-desktop-client-runtime.md)'s open architecture
  question.
- Workspace root = whatever directory the user opens the app against, constructed as a
  [43.7](phase-43.7-workspace-provider.md) local-disk provider (not a bare string) and passed into
  [43.1](phase-43.1-tool-execution-engine.md)'s `AgenticSession`. Multi-root ("Add folder") was a
  43.7 open question, not decided here — this spoke shipped single-root as long as it was built
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
  - No runtime-loaded/dynamic XAML, static resources, assets as embedded resources.
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
   unavailable Docker/Testcontainers Rooms-test environment. **No real live provider call was exercised**
   — the automated test uses a scripted `IChatClient` (same discipline as 43.1's `AgenticSessionTests`);
   an actual OpenAI round-trip requires a real key in `~/.forge/credentials.json`, not present on this
   machine.

   **Implemented by Codex, independently re-verified by Claude on this same machine/working tree**
   (same discipline as Task 1 and [43.7](phase-43.7-workspace-provider.md)):
   - Reviewed `DirectExpertRunner.StreamAsync` directly — confirms the accumulate-then-
     `ChatResponseExtensions.ToChatResponse(updates)`-once approach was used exactly as corrected during
     review (the initially-proposed `ProcessUpdate` method was verified via .NET reflection against the
     actual installed `Microsoft.Extensions.AI.Abstractions` 10.7.0 DLL to be `internal`, not callable
     across the assembly boundary — this build uses the public alternative instead).
   - `dotnet build src/ForgeMission.slnx` — reproduced clean (0 errors; same 1 unrelated pre-existing
     `PostgresFixture.cs` warning as Task 1).
   - `dotnet test ... --filter "VanillaMissionSessionFactoryTests|AgenticSessionTests"` — reproduced
     independently: 4 passed / 0 failed (the new streamed tool-call test plus 43.1's original 3).
   - `dotnet test src/ForgeMission.Tests/ForgeMission.Tests.csproj` — reproduced independently: 8 failed /
     316 passed / 11 skipped, 335 total — exactly the established baseline (334 total in Task 1's
     independent run) plus one net-new passing test, zero regressions.
   - Native AOT `win-arm64` publish: same re-verification-environment gap as Task 1 (`vswhere.exe`'s
     directory still not on `PATH` in this session) — not reproduced directly, but the publish output
     directory contained a genuine 40 MB `ForgeMission.Desktop.exe` timestamped minutes before the
     commit, independently launched (confirmed running, ~91 MB working set) and cleanly terminated.
3. ✅ **Done 2026-07-26.** Tool-call indicators — minimal inline rendering when a tool executes
   mid-turn (no full diff view — that was to be 43.4's code pane). Built to the design below; see
   [Task 3 design](#task-3-design-tool-call-indicators) for the mockups/spec and verification
   evidence.
4. **Added 2026-07-27, abandoned mid-flight 2026-07-27 (pivot to Electron).** Apply the visual
   identity system — skin only, not a restructure. Draft the Avalonia token catalogue per
   [Visual identity direction](../design/desktop-interaction-principles.md#visual-identity-direction-decided-2026-07-27)
   (mirrors `forge.css`'s groups: surfaces, lines, text, accent, radii, spacing, elevation) as
   `DynamicResource` brushes, then reskin the existing Tasks 1–3 controls (composer, `+` flyout,
   message list, tool-call indicator rows, workspace label) to use them in place of stock
   `FluentTheme` defaults. Explicitly **out of scope**: changing the chat-bubbles-and-compose-box
   layout shape itself — that restructure stayed [43.4](phase-43.4-ide-trace-surface.md)'s job. Run
   the [Cooper/Rams/Norman gate](../design/desktop-interaction-principles.md#the-assessment-gate--what-would-cooper-do-what-would-rams-do-what-would-norman-do)
   before implementation (mock the reskinned state, note the answers here), and use `avalonia_devtools`
   (DevTools MCP) to screenshot/verify the live result against the mock per the design-first
   process's step 6. **This is the task that surfaced the structural DevTools-MCP problem that
   ended up motivating the whole Electron pivot** (see
   [forge-desktop-client-runtime.md](../design/forge-desktop-client-runtime.md)) — it was never
   completed, and never will be under Avalonia. The in-flight implementation lives, unmerged, on
   branch `codex/phase-43.2-task-4-visual-identity`. Left alone deliberately: not deleted, not
   merged.

### Task 4 design: visual identity skin

| Before | After |
|---|---|
| ![Before: stock Fluent visual treatment](../images/phase-43.2/visual-identity-before.svg) | ![After: Forge ember visual identity](../images/phase-43.2/visual-identity-after.svg) |

**Component spec:**

- Keep the existing grid, message-list, flyout, and composer hierarchy unchanged. This task applies
  visual resources only; it does not create bubbles, cards, controls, or interactions.
- The application background uses the Forge background surface. The existing message-list border
  uses the standard line token and its interior uses the standard surface token.
- The workspace label and tool-call rows use the muted text token; all message and heading content
  uses the primary text token. Tool-call rows remain non-interactive and retain their current copy
  and ordering.
- The composer textbox and `+` trigger use the standard surface and strong-line tokens. The `+`
  glyph and selected flyout item use the ember accent and its soft accent surface. The existing
  Send button uses the ember accent treatment, including its contrast text and disabled state.
- Radius, spacing, and elevation resources match their `forge.css` counterparts. The flyout is the
  only listed control needing elevation; no new shadow-bearing card or layout fixture is introduced.
- Light and dark variants reproduce the corresponding `forge.css` values. The app continues to
  follow the system theme through `RequestedThemeVariant="Default"`.

**Gate check against the after mock:**

- *Cooper — persona/goal:* the quieter, higher-contrast workspace and tool metadata keep a developer
  oriented while a mission works, without adding chrome or interrupting the chat flow.
- *Cooper — implementation model:* the skin distinguishes primary message content from operational
  tool metadata but leaves the current chat-shaped runtime model intact; surfacing pipeline structure
  is deliberately deferred to 43.4.
- *Cooper — perpetual intermediate:* the familiar `+`, input, menu, and Send affordances remain
  immediately discoverable; expertise is not required to benefit from the skin.
- *Rams:* one coherent token system replaces default gray, preserves existing controls and states,
  gives only real controls interactive treatment, and remains usable by later 43.3/43.4 work.
- *Norman:* ember marks the actionable add/flyout state, muted rows remain visibly informational,
  the Send and disabled-input states continue to give direct feedback and constraints, and the
  compose-bar mapping remains unchanged.

**Pending approval detail (at time of abandonment):** the visual-direction decision specifies token
groups and `forge.css` source values but does not prescribe exact Avalonia resource keys. Those key
names were never approved — the task was abandoned before implementation reached that point.

5. Package for macOS (dev-signed is fine locally; defer notarization) and Windows (win-arm64,
   matching the existing release RID). **Not started — moot under the Avalonia shelving.**
6. Dogfood checkpoint: run a real multi-tool coding task end-to-end in the shell on Mac. **Not
   started — moot under the Avalonia shelving.**

### Design note: folder-open affordance fix

**Identified during review, 2026-07-26 — designed, then implemented.** Task 2 shipped the
open-folder flow working, but it violated two of the
[Desktop Interaction Principles](../design/desktop-interaction-principles.md) this doc prompted:
a top-right "Open Folder" button that never receded once a folder was open, and a placeholder
("Open a folder to begin") styled like a clickable link that did nothing when clicked — the real
trigger was the separate button. Compared directly against Claude Desktop's `+`-menu pattern for the
same action.

| Before | After |
|---|---|
| ![Before: persistent chrome, dead placeholder text](../images/phase-43.2/folder-open-before.svg) | ![After: progressive disclosure via the composer's + menu](../images/phase-43.2/folder-open-after.svg) |

**Component spec (after):**
- Composer gains a leading `+` icon button (36×36, outline style) opening a flyout menu anchored to
  itself, with two items: **Add folder** (calls the existing Task-2 `OpenFolderPickerAsync` path,
  unchanged) and **Attach files** (no feature behind it yet — omit the item entirely rather than
  ship a dead menu entry; add it back when file-attach exists).
- Compose textbox placeholder reads "Add a folder to start" while no workspace is open, reverting
  to "Describe what you want to build" once one is. Send button stays visible but disabled (as
  today) rather than removed, so the layout doesn't jump when a folder opens.
- The top-right "Open Folder" button and the dead placeholder text are removed outright, no
  replacement fixture. After a folder opens, the `+` menu remains in the composer for adding more
  later — nothing new appears in the header.
- **Addendum (gap found during handoff review, 2026-07-26):** the existing `WorkspaceLabel` row
  (`MainWindow.axaml:24`, today shows "No folder open" / the open path) was missed in the after
  mockup, which only depicts the pre-folder-open state. Resolution: while no workspace is open,
  this row is empty/collapsed — "No folder open" is redundant with the chat pane's own "No folder
  open yet" text and shouldn't say the same thing twice. Once a folder *is* open, the row shows the
  path in small muted text (no border, no background) beneath the header — this is real information
  the user needs on an ongoing basis (which folder they're in), not chrome, so it earns its place
  under the Norman feedback check even though nothing else in the header persists.

**Gate check:**
- *Rams — as little design as possible:* one entry point (the `+` menu) instead of two. Pass.
- *Rams — long-lasting:* the `+` menu is the same slot [43.3](phase-43.3-mission-attach-point.md)
  and later attach-style features can extend, rather than a bespoke button needing its own future
  redesign.
- *Norman — signifier check:* "No folder open yet" is plain muted text, not link-styled — nothing
  implies it's clickable. Pass.
- *Norman — mapping check:* `+` menu in the composer matches the pattern from Claude Desktop /
  Slack / iMessage users already know. Pass.

**Status:** ✅ **Done 2026-07-26** — implemented by Codex bundled with Task 3 (both touch the
composer), design-reviewed and independently re-verified by Claude on this same machine/working
tree (same discipline as Tasks 1–2 and [43.7](phase-43.7-workspace-provider.md)). `MainWindow.axaml`
now has a leading 36x36 `+` button with a `MenuFlyout` (`Add folder` only — `Attach files` correctly
omitted), the top-right button and dead placeholder text are gone, and the composer placeholder
switches on `IsWorkspaceOpen`. The `WorkspaceLabel` gap found during handoff review is closed:
`IsWorkspaceLabelVisible` requires both an open workspace and a non-empty label, so the row is
collapsed pre-open and shows the muted path post-open — verified by reading the actual XAML/VM diff
against this spec line by line, not just the summary. See the [Task 3](#task-3-design-tool-call-indicators)
verification evidence below for the shared build/test/publish run (same commands cover both).

### Task 3 design: tool-call indicators

**Designed 2026-07-26, before implementation**, per the
[design-first process](../design/desktop-interaction-principles.md#design-first-process-for-ui-facing-tasks).
Before this task (post Task 2) a tool call executed silently — the assistant bubble showed only the
final answer, with no trace that a `Read`/`Edit`/`Bash` call happened in between. For a coding-agent
surface that's a trust gap, not just a cosmetic one: the whole point of this app is that it edits
real files, and the user had no way to see that happening turn-by-turn.

| Before | After |
|---|---|
| ![Before: no visibility into tool calls](../images/phase-43.2/tool-call-indicators-before.svg) | ![After: quiet inline indicator rows, done vs. running](../images/phase-43.2/tool-call-indicators-after.svg) |

**Component spec:**
- One indicator row per tool call, rendered inline inside the assistant message, in the exact order
  the calls happened — not batched at the end. No card, no border, no background of its own; it's a
  quiet text row that inherits the assistant bubble's surface (matches the minimal-chrome gate).
- Two states only, no third "queued" state needed for v1:
  - **Running** — a small pending glyph (dashed/spinner-style circle) + present-participle verb +
    target, muted color: `Editing Bar.cs…`.
  - **Done** — a small check glyph + past-tense verb + target, same muted color: `Read Foo.cs`.
- Copy pattern by tool, sentence case, no trailing punctuation except the running state's ellipsis:
  `Read`/`Reading`, `Edit`/`Editing`, `Write`/`Writing`, `Run`/`Running` (for `Bash`, target = the
  command, truncated if long).
- No click target, no expandable output in this task — that was explicitly deferred to
  [43.4](phase-43.4-ide-trace-surface.md)'s code pane. These rows are informational only.
- Font size one step below the message body text (secondary/caption scale) so they read as
  metadata, not content.

**Gate check:**
- *Rams — thorough:* both running and done states are designed up front, not just the happy-path
  "done" state.
- *Rams — as little design as possible:* no card/border/diff view — a single muted text row per
  call, deferring anything richer to 43.4.
- *Norman — feedback check:* the user sees, in real time, that a specific file is being read or
  edited — directly closes the "no visibility" gap in the before state.
- *Norman — signifier check:* rows are static text with no hover/click affordance, since they aren't
  interactive yet — no false promise of a click target.

**Status:** ✅ **Done 2026-07-26.**

**Implemented by Codex, independently re-verified by Claude on this same machine/working tree**
(same discipline as Tasks 1–2 and [43.7](phase-43.7-workspace-provider.md)):
- Reviewed the actual diff, not just the summary: `AgenticSession.cs` gained a
  `Func<ToolCallNotification, CancellationToken, Task>` callback firing `Running` (post-approval,
  pre-execute) then `Done` (post-execute, carrying the `ToolExecutionResult`) — mirrors the existing
  `_approveToolCall` seam exactly, no unrelated surgery. `ToolCallIndicatorViewModel`'s copy mapping
  matches the spec exactly (`Reading …`/`Read`, `Editing …`/`Edited`, `Writing …`/`Wrote`,
  `Running …`/`Ran`), with an `Using`/`Used` fallback for unlisted tools (reasonable, not spec'd
  either way) and an 80-char truncation on the target, matching the "truncated if long" requirement
  for `Bash`.
- `dotnet build src/ForgeMission.slnx` — reproduced clean: 0 warnings, 0 errors.
- `dotnet test src/ForgeMission.Tests/ForgeMission.Tests.csproj --filter "VanillaMissionSessionFactoryTests|AgenticSessionTests"` —
  reproduced independently: 4 passed / 0 failed. The new
  `VanillaMissionSessionFactoryTests` assertion isn't a smoke test — it asserts notification order
  (`Running` before `Done`), tool name, and that `Result` is `null` on `Running` and populated
  (non-error, correct content) on `Done`, around the existing real-`Read`-execution scripted path.
- `dotnet test src/ForgeMission.Tests/ForgeMission.Tests.csproj` — reproduced independently:
  8 failed / 316 passed / 11 skipped, 335 total. The 8 failures are the identical established
  `ExecExpertRunnerTests` Windows baseline (confirmed by running that filter alone: same 8 test
  names, same code-9009 cause) — zero regressions. Passed/skipped counts differ from Codex's report
  (320/7 there vs. 316/11 here); the delta is fully inside live-integration tests gated on
  credentials/Docker/the real `claude` CLI not present in this session (`ForgeClaude_OneShotPrompt…`,
  `DirectExpertRunnerIntegrationTests…RealLlm…`, `GrokWebSearchIntegrationTests…RealGrok…`, etc.) —
  an environment difference between sessions, not a functional discrepancy; none of it touches the
  new code, which the focused filter above already covers cleanly.
- Native AOT `win-arm64` publish: **reproduced directly in this session** (unlike Tasks 1–2, where
  a `vswhere.exe` PATH gap blocked the publish step itself here) — `dotnet publish
  src/ForgeMission.Desktop/ForgeMission.Desktop.csproj -c Release -r win-arm64` completed with only
  a non-fatal `vswhere.exe not recognized` warning, producing a genuine 40 MB
  `ForgeMission.Desktop.exe` timestamped at build time. Independently launched (confirmed running
  via `tasklist`, pid 33092, ~82 MB working set — matches Codex's reported ~82.5 MB) and cleanly
  terminated.
- **Not independently verified:** the actual rendered UI (the `+` flyout opening, the indicator rows
  appearing live, the workspace label showing/hiding) — there's no tool available here to screenshot
  a native Avalonia window (only browser tabs are screenshot-capable). Verification of the visual
  behavior rests on reading the XAML/ViewModel diff against the component spec line by line, not a
  rendered screenshot. Same gap Codex itself flagged ("did not manually interact with the folder
  picker/flyout in the GUI"). Worth a manual look next time the app is opened interactively, but not
  blocking sign-off given the logic-level diff review.

## Done when (as originally written)

The Avalonia app opens a folder, accepts a prompt, streams a response, executes at least one real
tool call (file read/edit) visibly, and produces a working result — on macOS first, with a Windows
build (Surface ARM64 / Parallels) confirmed working within the same iteration (43.6 checkpoint).

Tasks 1–3 met this bar for the vanilla-chat slice; Tasks 4–6 (visual identity, packaging, dogfood)
never completed before the phase was shelved in favor of Electron.

## Open questions (as originally written)

- In-process `ForgeMission.Core` vs. `forge serve` loopback — locked toward in-process above, but
  worth a final check once streaming/cancellation semantics are prototyped (in-process gives free
  cancellation via `CancellationToken`; a server hop would need its own cancel-request wire-up).
  **Reopened by the Electron pivot** — see
  [forge-desktop-client-runtime.md](../design/forge-desktop-client-runtime.md)'s open architecture
  question, now scoped to [43.2 (Electron)](phase-43.2-electron-forge-desktop-shell.md) Task 2.
