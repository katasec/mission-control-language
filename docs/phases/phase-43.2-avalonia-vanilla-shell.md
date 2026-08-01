# Phase 43.2 — Avalonia vanilla shell (SHELVED)

**Status: Shelved 2026-07-27 — not a failure, a spike.** Avalonia was dropped in favor of an
Electron/Blazor-Server client — see
[docs/design/forge-desktop-client-runtime.md](../design/forge-desktop-client-runtime.md) for the
real reason (Avalonia's visual-verification tooling needs a paid DevTools tier + per-machine
license setup and hit a multi-day environment saga; the web-rendered alternative gets the same
verification for free via existing browser tooling, plus zero-cost `forge.css` token reuse instead
of an XAML translation tax). The active spoke replacing this one is
[phase-43.2-electron-forge-desktop-shell.md](phase-43.2-electron-forge-desktop-shell.md).

**What's preserved:** Tasks 1–3 (scaffold + AOT, real agentic streaming with a real tool-call loop,
tool-call indicators, folder-open affordance fix) were genuinely built and independently verified —
real filesystem/tool round-trips, no mocks. That evidence, and the full task-by-task narrative, is
retained in
[phase-43.2-avalonia-vanilla-shell_completed.md](phase-43.2-avalonia-vanilla-shell_completed.md).

**What's abandoned:** Task 4 (apply the visual identity/design-system skin) was in progress when the
pivot decision was made and was never finished. The in-flight implementation was on branch
`codex/phase-43.2-task-4-visual-identity`, merged into `main` via PR 2026-07-27 alongside this
doc-update pass — kept for historical reference only at the time, not because the Avalonia UI code
was active. **Removed 2026-08-01**: with the WASM/Photino replacement (43.11 Batch A) actually in
place and proven, the `ForgeMission.Desktop` project (and its `VanillaMissionSessionFactoryTests`)
were deleted entirely rather than kept as inert history — git history is sufficient provenance for
dead code once its replacement exists, no reason to carry the project forward in the tree.

**Not shelved:** [43.1 — Tool-execution engine](phase-43.1-tool-execution-engine.md) and
[43.7 — Workspace provider abstraction](phase-43.7-workspace-provider.md) are framework-agnostic
(they live in `ForgeMission.Core`), done, and reused as-is by the new Electron plan.
