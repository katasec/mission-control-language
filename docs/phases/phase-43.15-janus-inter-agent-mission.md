# Phase 43.15 — Janus: minimal inter-agent mission (Claude architect + OpenAI implementer)

**Status: Done (2026-08-10).** `ForgeMission.ChatClients` extracted, Anthropic's Native AOT crash
fixed (swapped to `tryAGI.Anthropic`), full negotiation loop verified live on the AOT-published
`forge` binary — both the converge and loop-exhaustion paths. Full build narrative, root-cause
investigation, design rationale, and verification evidence:
[phase-43.15-janus-inter-agent-mission_completed.md](phase-43.15-janus-inter-agent-mission_completed.md).
Part of [Phase 43 — Forge Desktop](phase-43-forge-desktop.md).

**NEXT STEP: merge `codex/janus-mini-mission`, then decide what's next for Phase 43** — 43.4 (IDE
trace surface) and 43.5 (human-in-the-loop) are the natural follow-ons per "Relationship to other
phases" below; neither has started.

## What Janus proves

1. **`using <profile>` routes different steps in one mission to different LLM providers** (Phase 25,
   already shipped) — verified this was never actually exercised by any shipped mission before now;
   every mission in `missions/` uses exactly one `[providers.default]` profile.
2. **A propose → question/approve → revise negotiation loop** (`Proposer` + `Approver` as
   `role: judge`, `loop(3)`) mirrors the real manual Claude/Codex workflow described by the user:
   don't proceed until explicitly approved; questions get answered on retry via `{{feedback}}` —
   the same mechanism `role: judge` failures already use for critique-driven convergence
   ([`sdlc-agent`'s `DesignMode`](../../missions/sdlc-agent/mission.mcl) proved this pattern first).
3. **`role: agent` gates real tool execution** (Read/Edit/Write/Bash, [43.1](phase-43.1-tool-execution-engine.md)'s
   `AgenticSession`) to only after that approval — structurally, not by convention. A failing judge
   step breaks the pipeline's step loop immediately, before any later step (including `Implementer`)
   is ever reached, so there is no way to execute an unapproved plan.
4. Once wired into Forge Desktop's UI ([43.4](phase-43.4-ide-trace-surface.md)) with visible per-step
   streaming (already proven via CLI `--steps`, matching what the user recalled seeing) and an
   explicit break-glass escalation option ([43.5](phase-43.5-human-in-the-loop.md)'s `kind: human`),
   this is the target UX: the user briefs Claude, Claude negotiates with the implementer
   autonomously, the user watches the whole exchange, and can intervene — but doesn't have to.

## Open questions / not yet decided

- **Graceful "not approved" outcome** — a `Blocked` branch instead of a hard `MissionStatus.Fail` on
  loop exhaustion. Real, deferred enhancement; needs either a `decision` field added to the judge
  structured-output schema (an actual engine change to `DirectExpertRunner`'s closed schema) or a
  different mechanism entirely.
- **Whether `role: agent` (tools) Anthropic calls are AOT-safe** — untested. Plain `forge run` can't
  exercise this path today: tool execution requires `AgenticSession`, which only the not-yet-shipped
  Desktop Client Runtime drives (per [43.1](phase-43.1-tool-execution-engine.md)'s own "reusable
  later by a CLI-driven agentic mode" note) — untestable until Forge Desktop or a CLI-driven agentic
  mode exists. This blocks fully trusting `missions/claude`/`forge claude`'s AOT-published behavior,
  not just Janus — the fix this spoke shipped (`ForgeMission.ChatClients` + `tryAGI.Anthropic`)
  likely resolves it too, since `tryAGI.Anthropic`'s tool-call mapping is part of the same
  reflection-free `AsIChatClient()` implementation, but that's an inference, not yet verified live.

## Relationship to other phases

- Motivating aspiration already logged in [43 hub's open questions](phase-43-forge-desktop.md#open-questions--not-yet-decided)
  (2026-07-26): eliminate manual Claude/Codex copy-paste. Janus is the first concrete build step
  toward it.
- Feeds [43.4 — IDE trace surface](phase-43.4-ide-trace-surface.md): once Janus's negotiation loop
  runs end-to-end, rendering it live in the desktop UI (not just CLI `--steps`) is 43.4's job. Janus
  is the first real content 43.4 has to render, not a mockup.
- Feeds [43.5 — Human-in-the-loop](phase-43.5-human-in-the-loop.md): the "break glass" escalation the
  user wants is exactly 43.5's `kind: human`/`Suspended` primitive — not yet wired into Janus. Today
  Janus's only "escalation" is the implicit `MissionStatus.Fail` on loop exhaustion.
- Deliberately NOT using [`missions/sdlc-agent/`](../../missions/sdlc-agent/) as the proving ground —
  see the completed doc's "Why this exists" for the reasoning. `sdlc-agent`'s OCI-publish blocker
  ([43.3](phase-43.3-mission-attach-point.md)'s stated NEXT STEP) is independent of this work and
  unaffected by it.

## Done when

- ✅ `Approver` (Anthropic) runs successfully under the AOT-published `forge` binary.
- ✅ Full negotiation loop (`Proposer` proposes/asks, `Approver` approves/rejects/answers, converges
  within 3 rounds, or exhausts to a legitimate `MissionStatus.Fail`) verified live with real API
  calls end-to-end — both the converge and exhaust paths.

Both verified live 2026-08-10 — see the completed doc for the exact evidence.

`Implementer` actually executing real tool calls is explicitly **not** part of this spoke's "done" —
it depends on a CLI-driven agentic mode or Forge Desktop, both owned elsewhere ([43.1](phase-43.1-tool-execution-engine.md),
[43.11](phase-43.11-wasm-photino-shell.md)). 43.15 proves the negotiation-and-gating mechanism; it
doesn't need real execution to be considered done.
