# Phase 43.15 — Janus: minimal inter-agent mission (Claude architect + OpenAI implementer)

**Status: Done (2026-08-11).** Both halves of "Done when" are verified: the AOT fix
(`ForgeMission.ChatClients` + `tryAGI.Anthropic`) and, since this spoke's 2026-08-10 reopening
found the negotiation mechanism itself didn't work, the fix for that too — a new
`SpeakerTranscript` type giving `loop(N)` full conversation-history replay between LLM
participants, plus splitting Janus into `Negotiate`/`Implement` so the one-shot `Implementer`
step no longer shares the negotiating parties' loop. Full investigation, design resolution
([Decision #9](phase-25-preflight-design-decisions.md#9-loop-context--deterministic-convergence-vs-random-retry)),
Codex task assignment, and independent re-verification are in the
[completed doc](phase-43.15-janus-inter-agent-mission_completed.md) — this spoke keeps only what
later phases still need to reference.

**Related finding:** `sdlc-agent/DesignMode`, `loop-demo/QualityAnswer`, and `self-refine/SelfRefine`
all share Janus's pure-LLM `loop(N)` + `role: judge` shape, so they now get full
conversation-history replay for free from the same engine change — no separate task needed.

## What Janus proves

1. **`using <profile>` routes different steps in one mission to different LLM providers** (Phase 25,
   already shipped) — verified this was never actually exercised by any shipped mission before now;
   every mission in `missions/` uses exactly one `[providers.default]` profile.
2. **A propose → question/approve → revise negotiation loop** (`Proposer` + `Approver` as
   `role: judge`, `loop(3)`, split into a `Negotiate` sub-mission) mirrors the real manual
   Claude/Codex workflow described by the user: don't proceed until explicitly approved; questions
   get answered on retry via full conversation-history replay (`SpeakerTranscript` — see
   [completed doc](phase-43.15-janus-inter-agent-mission_completed.md)), not a single-string
   mechanism — Proposer sees Approver's actual prior turn directly. ~~the same mechanism
   `role: judge` failures already use for critique-driven convergence ([`sdlc-agent`'s
   `DesignMode`](../../missions/sdlc-agent/mission.mcl) proved this pattern first)~~ **correction
   (2026-08-10, left visible rather than silently fixed): false — `DesignMode` never actually
   exercised this; it has the identical shape and gets the same fix for free (see "Related
   finding" above), but nothing "proved" the pattern until Janus was live-verified 2026-08-11.**
3. **`role: agent` gates real tool execution** (Read/Edit/Write/Bash, [43.1](phase-43.1-tool-execution-engine.md)'s
   `AgenticSession`) to only after that approval — structurally, not by convention. A failing judge
   step breaks the pipeline's step loop immediately, before any later step (including `Implementer`)
   is ever reached, so there is no way to execute an unapproved plan.
4. Once wired into Forge Desktop's UI ([43.4](phase-43.4-ide-trace-surface.md)) with visible per-step
   streaming (already proven via CLI `--steps`, matching what the user recalled seeing) and an
   explicit break-glass escalation option ([43.5](phase-43.5-human-in-the-loop.md)'s `kind: human`),
   this is the target UX: the user briefs Claude, Claude negotiates with the implementer
   autonomously, the user watches the whole exchange, and can intervene — but doesn't have to.

## "Not approved" outcome — resolved as presentation-only (2026-08-11)

**Decision: no engine or language change for this.** `Janus`'s current design already proves the
intended workflow correctly — Proposer/Approver negotiate with full transcript replay, and
`Implementer` runs only after `Approver` genuinely passes. A final rejection on loop exhaustion
already safely stops implementation and surfaces the Architect's actual reason, via the existing
`MissionStatus.Fail` + `FailReason` path — nothing is silently lost today.

Considered and explicitly rejected for this: a `MissionStatus.Blocked` outcome, `outputKeys`
runtime support in `DirectExpertRunner`, generated per-expert response schemas, and a
`kind: json_extract` workaround (the last of these doesn't even work mechanically — a step after a
failing judge never runs, so an extractor step placed after `Approver` could only ever fire on the
*passing* case, never the rejection case it would need to capture).

**For [43.4](phase-43.4-ide-trace-surface.md)/Forge Desktop, when it renders an exhausted Janus
negotiation:** present the existing failure result as

```
Not approved
<final Approver reason>
```

That's a presentation choice over data that already exists (`MissionResult.FailReason`) — not a new
machine-readable status. Revisit a third machine-readable status only if a future concrete use case
actually requires branching mission logic on it, not preemptively.

## Open questions / not yet decided

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
  see the completed doc's "Why this exists" for the reasoning. `sdlc-agent`'s OCI-publish work
  ([43.3](phase-43.3-mission-attach-point.md), formerly the stated next step) is independent
  follow-up and unaffected by the Janus Desktop PoC.

## Done when

- ✅ `Approver` (Anthropic) runs successfully under the AOT-published `forge` binary — the AOT
  crash fix.
- ✅ **Verified 2026-08-11**: Approver's actual pass/fail turns reach Proposer through
  `SpeakerTranscript` replay (not a single-string mechanism), and `Implementer` runs outside the
  negotiation loop via the `Negotiate`/`Implement` split. Live AOT run + full `dotnet test` (489
  passed, 11 skipped for unrelated live-provider reasons) — evidence in the
  [completed doc](phase-43.15-janus-inter-agent-mission_completed.md).

`Implementer` actually executing real tool calls is explicitly **not** part of this spoke's "done" —
it depends on a CLI-driven agentic mode or Forge Desktop, both owned elsewhere ([43.1](phase-43.1-tool-execution-engine.md),
[43.11](phase-43.11-wasm-photino-shell.md)). 43.15 proves the negotiation-and-gating mechanism; it
doesn't need real execution to be considered done.
