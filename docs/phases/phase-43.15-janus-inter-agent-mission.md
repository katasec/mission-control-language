# Phase 43.15 — Janus: minimal inter-agent mission (Claude architect + OpenAI implementer)

**Status: Reopened (2026-08-10).** The AOT fix (`ForgeMission.ChatClients` +
`tryAGI.Anthropic`) is genuinely done and stays done — that part of "Done when" is real,
verified, and not in question. What's reopened: the *mission itself* has two real gaps found
in the same session, after the AOT work landed —
[full design writeup and resolution](phase-25-preflight-design-decisions.md#9-loop-context--deterministic-convergence-vs-random-retry)
lives in the Phase 25 preflight doc (Decision #9's second superseded note), not duplicated here.
Summary:

1. `context["feedback"]` was never written for a failing `role: judge` step (only `kind:
   rule`/`kind: exec` do it) — `PipelineRunner.ExecuteStepAsync` needs the fix.
2. Deeper: even with (1) fixed, Janus's mission shape is wrong. `Implementer` was inside the
   same `loop(3)` as the two negotiating parties (`Proposer`/`Approver`), which isn't a
   participant in the negotiation and shouldn't share its loop. Corrected shape: split into
   `Negotiate(task) loop(3)` (Proposer + Approver only) and `Implement(plan)` (composed
   afterward), with `loop(N)` defaulting to full conversation-history replay among LLM-only
   participants (no flag — mission composition already provides the scoping).

Neither is implemented yet. The "Implementation verified" evidence in the
[completed doc](phase-43.15-janus-inter-agent-mission_completed.md) is accurate for what it
tested (no AOT crash, structured output round-trips, loop mechanically converges/exhausts) — it
did **not** verify that Approver's feedback was actually reaching Proposer, and it turns out it
wasn't. Part of "Done when" below is corrected to reflect that.

**NEXT STEP: implement the corrected design** — task assignment(s) to Codex, sequenced: (1) the
small `{{feedback}}` wiring fix (general-purpose, benefits any `role: judge` + `loop(N)` mission,
not Janus-specific), (2) the full-conversation-replay capability for LLM-only loops, (3) the
Janus mission split (`Negotiate` + `Implement`), in that dependency order. Re-verify live
afterward — same bar as the AOT fix: a real run, not just "tests pass."

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

- ✅ `Approver` (Anthropic) runs successfully under the AOT-published `forge` binary — genuinely
  done, this is purely about the AOT crash, unaffected by the mission-design gaps below.
- ⏳ **Corrected, 2026-08-10**: the loop *mechanically* converges/exhausts correctly (verified
  live), but "negotiation" requires Approver's feedback to actually reach Proposer and the
  mission to only loop its actual negotiating parties — neither was true. Reopened; see status
  note above. Re-mark done only after the corrected design is implemented and re-verified live.

`Implementer` actually executing real tool calls is explicitly **not** part of this spoke's "done" —
it depends on a CLI-driven agentic mode or Forge Desktop, both owned elsewhere ([43.1](phase-43.1-tool-execution-engine.md),
[43.11](phase-43.11-wasm-photino-shell.md)). 43.15 proves the negotiation-and-gating mechanism; it
doesn't need real execution to be considered done.
