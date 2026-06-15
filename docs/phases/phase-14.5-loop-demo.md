# Phase 14.5 — Loop Demo Mission

**Status:** Pending — depends on Phase 12 (StepEnvelope) and Phase 14 (loop N)

## Goal

A self-contained demo mission that makes the value of `loop N` immediately obvious without
requiring domain knowledge. Uses a deliberately unreliable expert and a sober judge to show
that looping always converges on quality — even when individual inference is non-deterministic.

## The Concept: Drunk Expert

`ContextOverloaded` is an expert that is overwhelmed and produces inconsistent work. It always
marks itself as pass (it does not know its output is poor). `QualityJudge` is always sober and
catches the bad work.

Running with `loop 3` shows the mission retrying until the judge approves. The audience sees
quality emerge from unreliable components — which is the real-world LLM story.

## Why Deterministic, Not Random

The demo uses `{{attempt}}` to make `ContextOverloaded` predictably bad on attempt 1 and good
on attempt 2+. This makes the demo **reproducible** — every run tells the same story, which is
what you need when showing it to someone.

## Mission File

```
// missions/loop-demo/mission.fms

use "./experts"

let topic = "why the sky is blue"

mission DemoReliability(topic) =
    ContextOverloaded
    |> QualityJudge
    loop 3

output(DemoReliability)
```

## ContextOverloaded Expert

```markdown
// experts/ContextOverloaded/expert.md
---
name: ContextOverloaded
input: topic
output: explanation
---

You are an overworked expert asked to explain: {{topic}}

This is attempt {{attempt}} of {{max_loops}}.

- On attempt 1: you are exhausted. Write a vague, hand-wavy explanation. Miss the key mechanism.
  Do not notice the quality is poor. Sound confident anyway.
- On attempt 2 or later: you are rested. Write a clear, specific, accurate explanation.
  Include the actual mechanism, a concrete example, and why it matters.
```

## QualityJudge Expert

```markdown
// experts/QualityJudge/expert.md
---
name: QualityJudge
input: explanation
output: verdict
---

You are a quality judge. Evaluate the explanation you received.

It passes if:
1. It names the specific mechanism (not just "light scatters")
2. It includes a concrete example or analogy
3. It is accurate — no hand-waving

If it fails any criterion, declare failure and state which criterion was missed.
If it passes all criteria, declare success and quote the strongest line.
```

## Expected Demo Flow

```
Running mission 'DemoReliability'... (attempt 1/3)
→ ContextOverloaded...   [vague output]
→ QualityJudge...        [fail — missing mechanism]

Running mission 'DemoReliability'... (attempt 2/3)
→ ContextOverloaded...   [clear, specific output]
→ QualityJudge...        [pass]

[final output to stdout]
```

## Contrast Mission (single expert, no loop)

```
// missions/loop-demo-naive/mission.fms

use "./experts"

let topic = "why the sky is blue"

mission NaiveAnswer(topic) =
    ContextOverloaded

output(NaiveAnswer)
```

Run both side by side. `NaiveAnswer` always returns the attempt-1 vague output with no retry.
`DemoReliability` always converges on quality by attempt 2.

## Demo Script

```bash
# Naive — one shot, whatever comes out
cd missions/loop-demo-naive && fms run

# Reliable — loops until quality passes
cd missions/loop-demo && fms run --steps
```

The `--steps` flag on the second run shows the retry happening in real time on stderr while
the clean final output lands on stdout.
