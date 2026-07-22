# Brainstorm: human-in-the-loop steps in MCL missions

**Status: conceptual only — not decided, not scoped as a phase.** Captured from a design
conversation on 2026-07-20. If this gets picked up, split it into a proper phase hub/spoke
per `AGENTS.md`; don't build directly from this doc.

## The idea

Introduce a human as a first-class step in an MCL pipeline — same composition contract as any
other expert (`kind: llm`, `kind: exec`, `kind: onnx`, ...), reads/writes the context bag the
same way, composes with `when()` and `loop(N)` the same way. The one real difference: the wait
for a human response can be indefinite, which no existing `kind` has to handle.

## Why existing roles don't need to change

`role: judge` already has fail/pass semantics and writes `{{feedback}}` on failure to drive
`loop(N)` retries. A human step just needs to be able to play the same roles a machine expert
already plays — decision/routing signal (à la `Classifier`, feeds `when()`), judge (drives
retry), or a normal prose-output expert (consult, feeds `{{output}}`) — no new `role:` required.
Retrospect this only if a real mission surfaces a shape the existing roles can't express.

## Use cases this unlocks (all the same primitive, different composition)

- **Approval as routing** — human step writes `decision: approve|reject|revise` to the context
  bag; `when()` branches off it exactly like the SDLC mission routes on `mode`
  ([interaction-modes.md](../design/interaction-modes.md)).
- **Escalation as fallback branch** — pair with `role: judge` or an `onnx` confidence score:
  AI handles it when confidence is high, `HumanReview when(else)` catches the rest. Human is the
  default branch, not a mandatory step — most runs never pause.
- **Correction as loop feedback** — human edits feed `{{feedback}}` on `loop(N)` retry, same slot
  `role: judge` failures already use.
- **Consult as context** — free-form human input lands in `{{output}}` like any expert.
- **Mandatory gate before irreversible steps** — required human step before a `kind: exec` that
  deploys/sends/pays. Structural version of the "explicit permission" boundary already followed
  in this working style.

## The mechanical gap: suspend/resume

`PipelineRunner.RunAsync` ([PipelineRunner.cs](../../src/ForgeMission.Core/Runtime/PipelineRunner.cs))
runs a mission start-to-finish today; there's no "pause mid-run, resume later" concept.
Phase 42.3 hit an adjacent problem (tool-call continuations) and deliberately chose *not* to
pause the process — see
[phase-42.3 §Design](../phases/phase-42.3-tool-capable-enriching-responder.md) ("the pipeline
runs start→finish... does not require pausing the C# pipeline mid-run; instead it re-derives
state per request"). That works there because the pauser is another AI turn that immediately
re-invokes with full history. A human doesn't behave that way, so the same trick doesn't
transfer as-is — but the shape of the fix does: persist state externally, skip already-completed
steps, re-derive forward.

**What's actually missing:** `StepEnvelope` only has a "completed" outcome; the `foreach` over
`mission.Pipeline.Elements` in `PipelineRunner.RunAsync` has no way to stop early and hand
control back. Needed:
- A `Suspended` outcome on `StepEnvelope`.
- A resume option on `PipelineRunOptions` that generalizes the existing `StartAtAgent` skip
  mechanism (`PipelineRunner.cs:86`) from "skip to the agent step" to "skip to step N with a
  seeded context bag."

**Split by where the mission runs, not by `kind:`:**
- **Local CLI** — `HumanExpertRunner.RunAsync` just blocks on stdin. An indefinite wait in a
  foreground terminal is idle time, not a resource problem. No persistence needed — the existing
  `Task<StepEnvelope>` contract on `IExpertRunner` is unchanged.
- **Hosted** — `RunAsync` must never block a worker for hours. It returns `Suspended`
  immediately; a layer above `PipelineRunner` (shaped like the `MissionExecutionService` already
  backing `RunContracts.cs`) persists `{run_id, step_index, context bag}` and later resumes by
  calling `RunAsync` again with the persisted context + "start at step N."

## Delivering the pending step: pluggable channels

Once state is persisted, *how* the human is notified and *how* their answer comes back is
orthogonal to the suspend/resume mechanism above — it's just an adapter layer between "persist
state" and "resume call."

Proposed shape: a `channel:` field on the human step, resolved by a switch exactly like
[`ProviderClientBuilder.BuildChatClient`](../../src/ForgeMission.Cli/ProviderClientBuilder.cs)
resolves `provider:` for LLM experts — one case per channel (`rooms`, `email`, `teams`, `slack`),
each providing:
1. **Notify** — render the pending question in that channel's native shape (Rooms chat message,
   email body with a link, Teams adaptive card with buttons, Slack block with a slash-reply).
2. **Resume callback** — the channel's click/reply must call back into one universal resume
   webhook that exchanges a token for `{run_id, decision}`.

Rooms ([`MemberKind.Human`](../../src/ForgeMission.Rooms/MemberKind.cs)) is the *easy* channel —
it already tracks state server-side. Email/Teams/Slack don't hold conversation state between
messages, so the token itself has to be self-describing (signed, embeds `run_id` and
`step_index`) — the same trick calendar RSVP and PR-approval-by-email links use.

## Open questions / not yet decided

- Exact shape of the resume token (signing scheme, TTL, single-use).
- Where `MissionExecutionService`-equivalent persistence actually lives for a suspended run —
  needs checking against what `RunContracts.cs` / the hosted API already assumes.
- Whether `kind: human` needs any config beyond `channel:` (e.g. a rendered prompt template per
  channel) or whether the expert's existing markdown body is enough.
- Not scoped against any phase or timeline yet.
