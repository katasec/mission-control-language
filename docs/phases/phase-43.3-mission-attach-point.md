# Phase 43.3 — Mission-as-attach-point

**Status: Design.** Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md). Depends on
[43.2](phase-43.2-avalonia-vanilla-shell.md).

## Design

Replace the vanilla shell's hardcoded single mission (43.2 task 5) with a real picker in the same
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

1. Mission discovery — list attachable missions. Start with a local directory scan
   (`missions/*/mission.mcl` + `forge.toml`) mirroring what `forge run`/`forge claude` already
   resolve; OCI-published mission discovery (Phase 39.4's registry) is a later upgrade, not needed
   for v1.
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
