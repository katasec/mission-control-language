# Phase 37.1 — Code Review Reasoning-Budget Pilot

> **Status: Design — resolved, ready for task assignment**
> **Depends on:** [Phase 37](phase-37-eval-harness.md) — reuses its `EvalRunner`
> aggregation core, `report()` output, and (once amended) its step-observation
> contract. This phase owns only the code-review-specific scenario: corpus,
> missions, scoring, cost fuse.
> **Purpose:** Reproduce, on our own architecture, the token/cost reduction
> [Adam Jacob's "swamp workflow" post](https://www.adamhjk.com/blog/a-practical-guide-to-reducing-token-spend/)
> reports for code review — an uncapped LLM-coordinator loop vs a fixed
> deterministic pipeline — as a controlled replication, not an independently
> designed benchmark. Matching his methodology (not inventing a new one) is
> the point: it isolates "does MCL's architecture help" as the only changed
> variable.

---

## Reference result being replicated

Adam's `garfield-extension` benchmark (Apache 2.0,
[github.com/adamhjk/garfield-extension](https://github.com/adamhjk/garfield-extension))
compares an LLM-coordinator-loop skill (`garfield`) against a deterministic
workflow (`workflow-garfield`) on a fixed test codebase (`ledgerlite`, vendored
under `benchmarking/ledgerlite/`). Published numbers for the case this phase
reproduces:

| Treatment | Tokens | Agent calls | Wall clock |
|---|---|---|---|
| `garfield` (coordinator loop) | 4,639,565 | 23 | 12.8 min |
| `workflow-garfield` (deterministic) | 506,484 | 3 | 6.6 min |

Ratio: **~9.2x fewer tokens, ~7.7x fewer agent calls, ~1.9x faster** for the
deterministic treatment. This phase does not assume we will reproduce these
exact numbers — the model differs (see Decision 7) — only that the same
qualitative result should hold.

## Locked design decisions

| # | Decision | Resolution | Why |
|---|---|---|---|
| 1 | MCL mission shape | 8 stages: `GitDiff → RepositoryInventory → FileFiltering → LanguageDetection → ProjectDetection → ContextConstruction → LLMReview → Summary`. Stages 1–6 are `kind:exec`, zero LLM tokens. | Mirrors the swamp post's principle: deterministic code for everything except judgment calls. |
| 2 | Quality metric | Deterministic pass/fail against hidden tests (matches `garfield_bench`'s `grading.py`), not a severity-weighted rubric or human review. | Matches his own methodology; avoids inventing an unproven scoring system for v1. |
| 3 | Corpus | Fork `benchmarking/cases/` + `benchmarking/ledgerlite/` from `garfield-extension` as-is. Adapt a case's shape only if it genuinely doesn't fit our mission's I/O — do not force a fit. | Apache 2.0, zero new authoring, direct reproduction. |
| 4 | Pilot scope | **One case only: `contained-dry-run`** (not `payment-idempotency`). | Cheaper (4.6M vs 12.3M tokens at his scale), cleaner binary grading criteria, doesn't touch generated client code. Best first-run fit. |
| 5 | "Before" mission | `GarfieldSkill` — a `kind:exec` step wrapping a coordinator script (bash/python) that dispatches review sub-agent calls in a loop, adjudicating between cycles itself. Loop logic lives entirely inside the script; no new MCL grammar/loop construct needed. | His original `garfield` skill has no documented cycle cap — it is genuinely unbounded by design. Capping it artificially would change the variable being measured. |
| 6 | "After" mission | `CodeReview` — the 8-stage mission in Decision 1. | — |
| 7 | Model | GPT-5.6 Luna, uniform across every stage of both missions. API model ID: `gpt-5.6-luna`. Pricing: $1.00 / 1M input tokens, $6.00 / 1M output tokens, $0.10 / 1M cached-input tokens (>272K-context requests price at 2x input / 1.5x output — not applicable here, `ledgerlite` diffs are far under that). | Keeps model capability constant so the only changed variable is architecture (coordinator-loop vs fixed-pipeline), not model strength. `ProviderClientBuilder` already supports `provider: openai` — no new plumbing. |
| 8 | `FileFiltering` logic | Pass-through: emit the full changed-files list, no type/size/vendored/generated exclusion. | Verified against his `workspace.py`: `changed_files()` runs `git status --porcelain`, returns everything unfiltered. Mirrors his actual behavior exactly. |
| 9 | Deterministic stage contracts | Each of stages 1–6 is a `kind:exec` Expert with a plain-English `input`/`output` one-liner (per real `expert.md` front matter — see [BusinessAnalyst example](../../missions/parallel-synthesis/experts/BusinessAnalyst/expert.md)) plus declared `Inputs`/`OutputKey` matching the script's actual stdin/stdout JSON. No invented typed-schema layer — MCL has none today. |
| 10 | `GarfieldSkill` cost fuse | **Hard $3 stop** for the pilot run, computed as `inputTokens/1e6 * 1.00 + outputTokens/1e6 * 6.00` (cached-input tokens, if any, at $0.10/1M) from exec-reported usage (Decision 12), checked after every coordinator cycle. Not a cycle cap — the loop stays logically uncapped; this is a safety fuse, not a design bound. | At Luna pricing ($1/$6 per 1M in/out) this buys roughly ~1.5M tokens assuming a ~80/20 input/output blend — likely *less* than the ~4.6M-token scale of his original run on this case, so the pilot may report "fuse tripped, incomplete" rather than a natural stop. That is an acceptable, honest first result — it already shows the coordinator loop costs materially more than the deterministic pipeline, which finishes for cents. |
| 11 | Per-step measurement fields | Adopt Adam's `AgentUsage` schema directly (from `garfield_bench/models.py`), renamed to MCL conventions: `stepName` (his `agent_id`), `parentStep`, `role`, `stage`, `cycle`, `inputTokens`, `cachedInputTokens`, `outputTokens`, `reasoningOutputTokens`, `totalTokens`, `durationMs`, `toolDurationMs`. | Proven schema for exactly this measurement problem — no reason to invent a new one. |
| 12 | Exec usage reporting | `kind:exec` scripts emit a `usage` object in their stdout JSON (alongside the required `outputKey`); the runner merges this into the same per-step measurement record real `IChatClient`-backed steps produce. | Required so `GarfieldSkill`'s coordinator (which calls the OpenAI API directly from inside the script, outside Forge's `UsageTrackingChatClient`) is visible to the same metrics/report pipeline as `CodeReview`'s native LLM steps. |
| 13 | Sample size | N=1 case for this pilot phase (Decision 4). No statistical claims — this is an explicit small-N replication, reported as raw numbers only, same as Adam's own post. | — |

## Dependency on Phase 37

Phase 37 must land the following before this phase's `LLMReview`/`Summary`
step metrics and `GarfieldSkill` exec-usage metrics can be captured — see
Phase 37's step-observation amendment (tracked there, not here):

- `OnStepStart`/`OnStepComplete` currently fire only in `PipelineRunner`'s
  serial path ([PipelineRunner.cs:260,310](../../src/ForgeMission.Core/Runtime/PipelineRunner.cs));
  `ExecuteParallelStepAsync` does not invoke them. Not required for this
  phase's linear 8-stage mission, but the contract Phase 37 lands should not
  regress parallel steps either.
- Callback signature needs `kind`, duration, model, and a `usage` object
  (Decision 11) — today it carries only `(expertName, StepEnvelope)`.
- `UsageAccumulator` is a single mission-wide counter today
  ([UsageTrackingChatClient.cs:9](../../src/ForgeMission.Core/Adapters/UsageTrackingChatClient.cs));
  per-step attribution needs per-step accumulation, not snapshot-diffing a
  shared counter (ambiguous under concurrency).

## Repository layout

```
evals/
  code-review/
    cases/
      contained-dry-run/          # forked as-is from garfield-extension/benchmarking/cases/
        prompt.md
        fixture.patch
        solution.patch
        oracle.json
        hidden-tests/
    ledgerlite/                   # forked as-is, vendored fixture codebase
    missions/
      garfield-skill.mcl          # "before" — kind:exec coordinator loop
      code-review.mcl             # "after" — 8-stage deterministic+LLM pipeline
    results/
      <run-id>/                   # immutable per-run output, not committed fixtures
```

## Done when

- `forge run` both missions once against `contained-dry-run`.
- `CodeReview` completes, produces a pass/fail grade against `oracle.json` /
  hidden tests, and reports total tokens/cost/latency.
- `GarfieldSkill` either completes naturally or trips the $3 fuse — either
  outcome is a valid, reportable result (Decision 10).
- A comparison report (tokens, cost, latency, pass/fail for both) is written
  to `evals/code-review/results/<run-id>/report.md`.
- Result is written into `docs/findings.md` alongside Phase 37's own
  reference evals.

## What is NOT in scope (this pilot)

- `payment-idempotency` (Decision 4) — second case, later phase if the pilot
  is worth extending.
- Any quality metric beyond pass/fail (Decision 2) — no severity-weighted
  rubric, no human review pass.
- Reproducing Adam's exact token/agent-count scale (Decision 10) — the fuse
  makes this explicitly out of scope for the pilot.
- `forge eval code-review` CLI surface — this pilot is run directly via
  `forge run`, not through Phase 37's bootstrap CLI runner. Binding this
  scenario into that CLI is follow-on work once Phase 37's Spoke 6 lands.
