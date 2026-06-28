# Phase 34 — Forge UI

**Status:** Design

---

## Why a UI is necessary

This decision was reached through direct experimentation, not assumption. We tested
every available AI interaction surface — Claude Desktop, Claude CLI (`--print`), and
Open WebUI — before concluding that a dedicated UI is required.

### What the trials revealed

| Surface | Result | Root cause |
|---------|--------|------------|
| Claude Desktop | Output mutated to prose | Claude synthesises all tool results by design |
| Claude CLI (`--print`) | Same mutation | Same reason |
| Open WebUI | Renders markdown, works | Talks directly to `forge serve` OAI endpoint |
| Terminal (`forge run`) | Works perfectly | No intermediary |

The pattern: **any surface where an LLM is the intermediary will reformat and
collapse the output**. This is not a bug in those surfaces — it is their core UX
philosophy. The tension is structural and not fixable.

### The fundamental conflict

MCL's core value is making the reasoning process a first-class artifact — not just
the answer, but how you got there, what was checked, what failed, what retried.

Every existing AI surface is designed to do the opposite: collapse that process into
a clean, confident answer. Serving MCL through those surfaces produces the answer
but loses the proof. **The proof is the product.**

### The average user argument

The need for a UI is not just a developer or academic concern. Consider this: a
non-technical user (a sales person, a student, a professional) asks an AI "which
month of the year has the letter X in it?" ChatGPT answers confidently with the
wrong answer and a plausible-sounding French etymology rationalisation. The user has
no way to detect the hallucination.

MCL can defend against this — a `FactChecker` expert catches the error, a `Verifier`
judge forces a retry, the correct answer emerges. But if the output surface is
Claude Desktop or ChatGPT, the pipeline ran invisibly and the user just sees a
better answer with no indication of why they should trust it more.

The differentiator is not accuracy — it is **visible trust**. A non-technical user
does not need to see the pipeline. They need to see one signal:

> ✓ **No English month name contains the letter X** — *verified*

vs a confident unverified guess. That signal is what a dedicated UI can surface.
Without it, MCL is "AI with better accuracy behind the scenes" — not a
differentiated story. With it, MCL is "AI where you can see and trust the
reasoning" — a fundamentally different proposition, especially in high-stakes
domains (compliance, medical, financial, legal) where "trust me" is not enough.

---

## What the UI needs to do

### For non-technical users (primary)

- Show the answer prominently
- Show a **verified / unverified** trust signal
- Optionally disclose "how do I know?" — a collapsible trace for users who want
  to understand why the answer is trustworthy
- Feel familiar — chat-like input, clean output, no pipeline jargon visible by
  default

### For developers and mission authors (secondary)

- Show the full pipeline trace as it executes: which expert is running, streaming
  output per step, pass/fail status at each step
- Show loop convergence — how many retries, what feedback triggered them
- Show the StepEnvelope data (status, reason, meta) per expert
- Allow side-by-side: raw LLM answer vs MCL-verified answer

### The seatbelt analogy

The user does not need to understand the tensioner mechanism to benefit from a
seatbelt. They just need the warning light. The pipeline trace is the engine; the
UI is the warning light. Both audiences are served by the same underlying data
(StepEnvelope) rendered at different levels of detail.

---

## Design decisions

### Why not extend Open WebUI?

Open WebUI works (it talks directly to `forge serve`) but it is a generic chat UI.
It has no concept of missions, pipelines, expert chains, or trust signals. Users
see a chatbox — the MCL structure is completely invisible. Extending it to show
pipeline traces would require deep forking of an upstream project, creating a
maintenance burden with no upstream alignment.

### Why not Claude Desktop / Copilot / Codex CLI?

These surfaces put an LLM between the user and the mission output. That LLM will
always reformat, summarise, and synthesise — dissolving exactly the structured
reasoning MCL produces. This is by design in those products and cannot be worked
around.

### Custom UI vs fork of Open WebUI

| Option | Pros | Cons |
|--------|------|------|
| Custom UI (React/Next.js) | Full control, MCL-native concepts, no upstream debt | Build cost |
| Fork Open WebUI | Familiar base, auth/sessions already built | Maintenance burden, fighting the upstream model |
| Extend Open WebUI via plugin | Low effort | Plugin API too limited for pipeline trace |

Recommendation: **custom UI, minimal scope**. Start with two panels — pipeline
trace (left) and verified answer (right). The `forge serve` OAI endpoint plus the
StepEnvelope data model already provide everything the UI needs. No new backend
work required.

### Transport

`forge serve` already exposes an OAI-compatible SSE streaming endpoint. The UI
consumes this directly — same as Open WebUI does today. The pipeline trace data
is already in the stream (StepEnvelope per step); the UI just needs to render it.

The `output:` field planned for `agent.yaml` (render hint: `markdown | text | json`)
gives the UI a signal for how to render the final answer.

---

## Spokes

| Spoke | Description | Status |
|-------|-------------|--------|
| 1 | UI scaffold — Next.js app, two-panel layout (trace + answer), connects to `forge serve` | Todo |
| 2 | Pipeline trace renderer — streams StepEnvelope events, shows expert name + status in real time | Todo |
| 3 | Trust signal — verified/unverified badge on the final answer, driven by mission pass/fail | Todo |
| 4 | "How do I know?" disclosure — collapsible per-step detail for users who want the full trace | Todo |
| 5 | Loop convergence visualisation — show retry count, feedback message, convergence point | Todo |
| 6 | Developer mode toggle — switch between end-user view (trust signal only) and full trace view | Todo |
| 7 | `agent.yaml` render hint (`output:` field) — forge serve uses this to shape the response for the UI | Todo |

---

## Origin

Reached after direct experimentation with Claude Desktop, Claude CLI, and Open
WebUI (2026-06-28). The conclusion was not assumed — each surface was tested and
the failure mode documented. The sales-person hallucination example (ChatGPT
confidently answering "June" for "which month has X in it?") crystallised the
non-technical user case and confirmed that visible trust, not just accuracy, is
the product.
