# Phase 43.3 — Mission-as-attach-point

**Status: In progress.** Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Depends on
[43.11 — Blazor WASM UI + Photino shell](phase-43.11-wasm-photino-shell.md), done 2026-08-08. (The
43.2 Electron/Avalonia shells this doc originally depended on were both superseded/shelved before
43.11 shipped — see [phase-43.2-electron-forge-desktop-shell.md](phase-43.2-electron-forge-desktop-shell.md).)

## Design

Replace the vanilla shell's hardcoded single mission (43.2's scaffold/Mission-Runtime-wiring tasks)
with a real picker in the same
UI slot a model dropdown occupies — attaching a **mission** instead of a model. This is the direct
generalization of [Phase 38.5](phase-38.5-registry-save-as-agent.md)'s raw-model passthrough
pattern (`@claude`/`@openai` as thin `vanilla`-shape missions,
[missions/claude/](../../missions/claude/)) from "one model wrapped as a mission" to "any real
multi-role mission is attachable the same way."

Flagship mission: [missions/sdlc-agent/](../../missions/sdlc-agent/) —
`Classifier -> (DesignMode | TaskMode)`, where `DesignMode` is
`Architect -> CriticalReviewer -> Synthesiser -> QualityJudge` with `loop(2)`. Picking "SDLC" in
the desktop app should feel exactly like picking a smarter model, but produce the
propose/critique/revise/gate-check shape no single model turn can.

## Tasks

1. ✅ **Done** — Mission discovery. `MissionDiscovery.Discover(missionsRoot)`
   ([src/ForgeMission.Core/Resolution/MissionDiscovery.cs](../../src/ForgeMission.Core/Resolution/MissionDiscovery.cs))
   scans immediate subdirectories of a missions root for `mission.mcl`, mirroring the
   `<missionsRoot>/<name>/mission.mcl` convention `forge run`/`forge claude` already resolve by
   name — but lists every mission found instead of one pinned handle. Returns a
   `MissionDescriptor(Name, MissionFilePath, HasManifest)` per mission, sorted by name; pure
   filesystem read, no OCI/registry calls (Phase 39.4's registry discovery is a later upgrade).
   Root path resolution is left to the caller — same pattern as `RunnerMissionSource`/`forge claude`,
   each of which computes its own root today. Not yet wired to any transport endpoint or UI; that's
   task 2/3's job. 5 unit tests in
   [src/ForgeMission.Tests/Resolution/MissionDiscoveryTests.cs](../../src/ForgeMission.Tests/Resolution/MissionDiscoveryTests.cs).
2. Picker UI — same visual treatment as a model dropdown (per the reference screenshot from the
   2026-07-25 design conversation), listing mission name + one-line description (reuse mission
   frontmatter/`forge.toml` metadata if present, else the mission name alone).
3. Attach/switch — selecting a mission rebinds the compose box's target; switching mid-conversation
   starts a fresh session (no cross-mission context carry-over for v1 — flag as an open question
   below if it turns out users expect otherwise).
4. **Decide and implement intermediate-role-switch visibility** (the open question raised in the
   hub): does the user see "Architect proposes... CriticalReviewer pushes back..." inline as the
   mission runs, or only the final synthesized output? Recommend starting **visible** — it's the
   entire value proposition of attaching a mission instead of a model, and hiding it makes the
   mission indistinguishable from a single smarter model in the UI, undermining the pitch this
   whole phase is built on.
5. Dogfood checkpoint: run a real design question through the SDLC mission in the shell, confirm
   the propose/critique/revise/gate loop is visible and legible.

## Done when

The mission picker lists real missions (at minimum: `sdlc-agent` + the existing `@claude`/`@openai`
passthrough missions), attaching one runs it end-to-end through
[43.1](phase-43.1-tool-execution-engine.md)'s agentic loop, and role-switches are visible in the
UI per task 4's decision.

## Open questions

- Cross-mission context carry-over on switch — deferred above, revisit if dogfooding surfaces real
  friction.
- Whether mission metadata needs a new field (short description for the picker) or existing
  frontmatter is enough — check what [missions/sdlc-agent/forge.toml](../../missions/sdlc-agent/forge.toml)
  already carries before adding anything new.
