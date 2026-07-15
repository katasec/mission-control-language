# Phase 37 — Evaluation Harness (`eval` / `dataset` / `report`)

> **Status: Design**
> **Depends on:** Phase 21 (parallel/named outputs), Phase 25 (mission composition —
> missions callable as steps), Phase 25a (`role: judge`), Phase 12 (`StepEnvelope`
> carries structured pass/fail + score)
> **Purpose:** Turn "expert composition elevates response quality" from a *vibe*
> into a *number*. Run one or more missions over a dataset of inputs, score each
> output with a judge, and aggregate the verdicts into a comparison report.

---

## Motivation

MCL's founding thesis (`docs/why.md`) is a **measurable claim**:

> Expert composition improves reasoning quality, consistency, and outcomes compared
> to a single general-purpose prompt.

Today that claim is demonstrated, not proven. You can run a mission once and read
the output with your eyes. That produces a **vibe** — "the refined pitch seems
better" — from one input, judged once, informally. What you cannot produce is the
line that actually settles the thesis:

> *refined beat naive on 17 of 20 inputs; average judge score 8.1 vs 5.4.*

An eval harness is the missing primitive that produces that line. It is a repeatable
loop that (1) takes a **set** of inputs, (2) runs one or more missions on each,
(3) **scores** each output, and (4) **aggregates** the scores into a verdict.

### Why this is the natural next phase, not a bolt-on

The pieces already exist in the repo — the harness is mostly *wiring what is already
there*:

- **The scorer already exists.** Six `role: judge` experts ship today (`PitchJudge`,
  `QualityJudge`, `ReferenceJudge`, `Verifier`, …). `llm-as-judge` is a full
  reference-guided judging mission (Zheng et al., MT-Bench). A judge is just a
  mission whose output is a score + verdict.
- **The A/B pairs already exist — as demos.** The mission library ships two
  comparison pairs *built specifically to show "the difference composition makes"*
  (their own header comments say so):
  - `elevator-pitch` (1 expert, one shot) ↔ `elevator-pitch-refined` (4-expert
    draft→critique→revise→judge)
  - `loop-demo-naive` (no judge) ↔ `loop-demo` (loop + `QualityJudge`)
- **Missions are already composable** (Phase 25). A mission can call another mission
  as a step. An eval is "call the baseline, call the candidate, call the judge" —
  three things the runtime already does.

What is missing is only the **outer loop + aggregation**: for each of N inputs, run
both missions, hand both outputs to a judge, record who won, tally. Right now that
loop lives in the author's head and runs once. Phase 37 moves it out of the author's
head into a reviewable, repeatable artifact.

### Why it matters beyond settling the thesis once

The durable value is **regression + design feedback**. When a new reasoning
structure is invented next week, the author can ask "is this actually better than
the simpler version, or does it just *feel* fancier?" and get a real answer instead
of a hunch. Named, swappable reasoning structures are only worth having if you can
rank them.

---

## The core design fork (decide first — blocks the grammar)

There are two places the eval loop can live. This is the one decision that shapes
every spoke.

### Option A — Out-of-language (`forge eval` command + external dataset)

A CLI subcommand reads a dataset file (CSV/TOML/JSONL), calls `forge run` N times
per mission, and tallies. Fast to build, no grammar change.

- **Pro:** minimal; ships in days; dataset lives in a familiar format.
- **Con:** the eval logic lives *outside* MCL. The comparison — arguably the most
  important reasoning artifact in the whole project — is a shell script, not a
  first-class, forkable, reviewable object. This directly undercuts the thesis
  that *reasoning structure is a first-class artifact*.

### Option B — In-language (`eval` / `dataset` / `report` constructs) — **recommended, on-thesis**

The benchmark is expressed in `.mcl` itself, so a comparison is as inspectable and
forkable as the missions it compares.

```
dataset PitchIdeas = [
    "an app that turns voice memos into structured tasks",
    "a marketplace for local farm surplus produce",
    "a browser extension that summarises long threads",
    // … 20 total
]

eval PitchComparison(product in PitchIdeas) = {
    baseline:  QuickPitch(product)      // missions/elevator-pitch
    candidate: RefinedPitch(product)    // missions/elevator-pitch-refined
    judge:     PitchPreferenceJudge     // role: judge — sees both, picks winner + score
}

report(PitchComparison)
```

- **Pro:** maximally on-thesis. A benchmark becomes a reviewable artifact. The
  judge is *already* a mission construct — reuse, not new machinery. Anyone reading
  the file sees exactly what is being compared, on what data, by what rubric.
- **Con:** grammar + parser + runtime work. Larger phase.

**Recommendation:** Option B is the destination. A pragmatic path is to **bootstrap
with a thin Option-A runner** to get the first real number this week, then lift the
same semantics into the language once the shape is proven. The two are not mutually
exclusive — the CLI runner and the `eval` construct can share the same aggregation
core. Spoke breakdown below assumes Option B as the target with the bootstrap called
out explicitly.

---

## Scoring modes

Two judging strategies, both backed by MT-Bench (Zheng et al., 2023):

| Mode | Judge sees | Judge outputs | Use when |
|---|---|---|---|
| **Pairwise** | Both outputs (A and B) | Winner (`baseline` / `candidate` / `tie`) + reason | Comparing two missions head-to-head (the A/B pairs) |
| **Pointwise** | One output + rubric (+ optional reference answer) | Numeric score (e.g. 1–10) + reason | Scoring a single mission against an absolute rubric; enables trend tracking over time |

**Position bias mitigation (pairwise):** LLM judges favour whichever answer is
presented first. The harness must run each pairwise comparison **twice with the
order swapped** and only count a win if it survives the swap (otherwise → tie).
This is a known MT-Bench finding and is non-negotiable for a credible number.

**Judge = mission (reuse Phase 25a).** A judge is any `role: judge` expert/mission
whose `StepEnvelope` carries the verdict. Pointwise judges already exist. Pairwise
judges are a small addition: an expert whose expert.md rubric compares two
`{{baseline.output}}` / `{{candidate.output}}` blocks. No new *kind* is needed.

---

## Cross-model eval mode (the transpose — proves the "standardized floor across models" claim)

The A/B modes above hold the *model* fixed and vary the *mission* (mission-A vs mission-B). The **transpose**
holds the *mission* fixed and varies the **subject model** — and it is what proves
[Phase 42](phase-42-forge-cloud.md)'s promise of *"a standardized quality floor across models, including
local."* It is a small extension of the same machinery, **not new plumbing**: same `EvalRunner` core, same
judge, same `report()` — you just vary the **provider profile** per run (`forge.toml`) instead of varying
the mission binding.

```
eval FloorAcrossModels(product in PitchIdeas) = {
    subject:  RefinedPitch(product)         // ONE mission, run per model
    models:   [gpt-4o, claude, llama-8b, local-mistral]   // subject-model sweep
    baseline: raw(product)                  // the naked model, same input (the floor reference)
    judge:    QualityJudge                   // pointwise rubric — scores each cell
}
report(FloorAcrossModels)
```

The report emits the two numbers that *are* the promise: **floor-lift** per model (wrapped − raw) and
**variance-collapse** across models (σ of raw scores vs σ of mission-wrapped scores):

```
## FloorAcrossModels — RefinedPitch, 20 inputs, 4 subject models
              raw    wrapped    Δ floor-lift
  gpt-4o      6.9     8.4       +1.5
  claude      7.1     8.5       +1.4
  llama-8b    3.8     7.9       +4.1     ← largest lift where the model is weakest
  local       4.2     8.0       +3.8
  variance across models:  raw σ 1.6  →  wrapped σ 0.3      ← the standardized floor
```

Load-bearing details: the **judge model must be pinned and held constant** across the sweep (else you're
measuring the judge, not the subjects — see design Q3); a model that **fails to execute the mission's steps**
(invalid classify JSON, incoherent synthesis) reports as a floor *miss*, not a crash (per-row failure
isolation, Spoke 3) — which is the useful signal "this model is below the minimum-viable bar for this
mission." **v1 can ship this as a `--models` flag on the bootstrap CLI runner (Spoke 6)** before the
in-language `models:` surface lands.

## Aggregation & report

The harness collects per-input verdicts and produces a summary. Minimum viable
`report()` output (markdown, matching how missions already emit):

```
## PitchComparison — 20 inputs

candidate (RefinedPitch) beat baseline (QuickPitch): 17 / 20   (3 ties, 0 losses)
mean score:  candidate 8.1   baseline 5.4   Δ +2.7
position-bias flips discarded: 2

| # | input                                   | winner    | cand | base |
|---|-----------------------------------------|-----------|------|------|
| 1 | voice memos into structured tasks       | candidate | 9    | 6    |
| 2 | marketplace for local farm surplus…     | tie       | 7    | 7    |
| … |                                         |           |      |      |
```

Machine-readable output (JSONL/`results.json`) written alongside for downstream
tooling and CI gating. STJ source-gen context for any new result types (AOT).

---

## Hub + Spokes

### Spoke 1 — `dataset` construct + parser
Grammar for `dataset Name = [ ... ]` (inline list first; file-backed source —
`dataset Name from "ideas.jsonl"` — as a follow-on). AST node, source positions,
`ForgeMission.Parser` support. Validation: dataset referenced by an `eval` must
exist and be non-empty.

### Spoke 2 — `eval` construct + parser
Grammar for `eval Name(param in Dataset) = { baseline:, candidate:, judge: }`.
`baseline`/`candidate` bind to mission invocations (reuse Phase 25 mission-as-step
dispatch); `judge` binds to a `role: judge` expert or mission. Single-mission
(pointwise-only) form allowed: an `eval` with just `candidate:` + `judge:`.

### Spoke 3 — `EvalRunner` (aggregation core)
Orchestrates: for each dataset row → run baseline + candidate (concurrently, reuse
`parallel` fan-out infra from Phase 21) → invoke judge → record verdict. Handles
per-row failure isolation (one bad row does not abort the eval). This core is shared
by both the in-language construct and the bootstrap CLI runner.

### Spoke 4 — Pairwise judge protocol + position-bias swap
Define the pairwise judge contract (judge sees `{{baseline.output}}` +
`{{candidate.output}}`, returns winner + score). Implement double-run order swap and
tie-on-disagreement. Ship one reference pairwise judge (`PitchPreferenceJudge`)
against the existing `elevator-pitch` A/B pair.

### Spoke 5 — `report()` + result serialisation
Markdown summary table + aggregate stats (win rate, mean score, Δ, discarded flips).
`results.json` / JSONL machine output. STJ source-gen for result types.

### Spoke 6 — Bootstrap CLI runner (Option-A path, ships first)
`forge eval <mission-a> <mission-b> --dataset file --judge <expert>` — thin wrapper
over the Spoke 3 core, no grammar dependency. Delivers the **first real number**
before Spokes 1–2 land. Deliberately sequenced first so the thesis gets a datapoint
early and the aggregation core gets exercised before the language surface is frozen.

### Spoke 7 — Reference eval + documented finding
Run `PitchComparison` (20 inputs) and `LoopVsNaive` over the two existing A/B pairs.
Write the result into `docs/findings.md` (currently a promissory note referenced by
`why.md`). This closes the loop on the founding hypothesis with actual data.

---

## Design questions (unresolved)

| # | Question | Notes |
|---|---|---|
| 1 | **In-language vs CLI as the primary surface** | Recommendation: in-language (`eval`) is the destination; CLI bootstrap ships first (Spoke 6) and shares the Spoke 3 core. Confirm before freezing grammar. |
| 2 | **Dataset source formats** | Inline list (v1). File-backed: JSONL (one JSON object per row, supports multi-field inputs) vs CSV (flat, familiar). Lean JSONL — missions increasingly take structured, multi-key inputs. |
| 3 | **Judge model vs subject model** | Should the judge run on a *different, stronger* model than the missions under test, to reduce self-preference bias? MT-Bench uses GPT-4 as judge over weaker subjects. Make judge model configurable per-eval (`forge.toml` provider profile). |
| 4 | **Pairwise position-bias handling** | Resolved in principle: double-run with swap, tie on disagreement. Open: is 2× cost acceptable, or offer a `--fast` single-order mode with a bias caveat in the report? |
| 5 | **Statistical honesty** | 20 inputs is enough to see a signal, not enough for significance. Should `report()` emit a confidence interval / note N is small? Avoid overclaiming from tiny datasets. |
| 6 | **Non-determinism / seeds** | LLM outputs vary run-to-run. Should an eval run each cell K times and average, or accept single-shot per cell for v1? Single-shot v1; K-repeat as a flag later. |
| 7 | **Cost visibility** | An eval is N × (baseline + candidate + judge×2) LLM calls — easily hundreds. `report()` should surface token/call counts so an author knows the price before running a 200-row dataset. |
| 8 | **CI gating** | Should `forge eval` return a non-zero exit / threshold assertion (`--min-win-rate 0.7`) so evals can gate a release, guarding against a mission-quality regression? Design after the construct stabilises. |
| 9 | **Judge rubric drift** | If the judge's expert.md rubric changes, historical scores are no longer comparable. Version the judge (reuse Phase 11 expert versioning) and stamp it into `results.json`. |
| 10 | **Cross-model eval surface** | The transpose (same mission, sweep subject models — see "Cross-model eval mode") that proves [Phase 42](phase-42-forge-cloud.md)'s floor-across-models claim. Ship as a `--models` flag on the bootstrap CLI (Spoke 6) first; lift to an in-language `models: [...]` clause on `eval` later. Judge model pinned + constant across the sweep (ties to Q3). Report emits floor-lift Δ per model + variance-collapse σ. |

---

## What is NOT in scope (v1)

- **Automatic dataset generation** — the author supplies inputs. LLM-synthesised
  eval sets are a later, separate concern (overlaps the program-synthesis spike).
- **Human-in-the-loop scoring UI** — judging is LLM-as-judge only in v1. A human
  review surface belongs to Forge UI (Phase 34/35).
- **Cross-run trend dashboards** — v1 emits a single report per run. Time-series
  tracking of a mission's score across commits is a follow-on.
- **Statistical significance testing** — v1 reports raw win rates and means with a
  small-N caveat, not p-values.
- **Multi-way tournaments** (>2 missions ranked against each other) — pairwise A/B
  first; N-way ranking later.

---

## Connection to the founding thesis

Phase 37 is the phase that lets MCL *close the loop it opened in `why.md`*. Every
prior phase built reasoning structures (loops, debate, judges, symbolic gates, exec).
This is the first phase that lets the project **measure whether those structures
actually deliver the quality they claim** — and, going forward, refuse to ship a
reasoning structure that can't beat the simpler baseline it replaces.
