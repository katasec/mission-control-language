# Phase 37.1 — Code Review Reasoning-Budget Pilot

> **Status: Rebased — no valid comparison result yet.**
> **Depends on:** [Phase 37](phase-37-eval-harness.md) for generic step observation.

## Purpose

Make one fair, reproducible comparison between the published upstream
`garfield` skill and `workflow-garfield` deterministic workflow on the public
`contained-dry-run` fixture. The question is whether deterministic orchestration
reaches a correct repair more cheaply and reliably than the agentic workflow
when both receive equivalent, non-answer-revealing task guidance.

## Reset status

The 2026-07-28 pilot implementations and their raw results were removed. They
are not comparison evidence: earlier prompts leaked defect-specific guidance,
early Garfield implementations were not the upstream skill, and the later
deterministic wrapper blocked on a response-schema incompatibility before a
repair could be applied. Generic MCL step measurement is retained separately.

## Fixed comparison boundary

- Case: public `contained-dry-run` fixture only.
- Model: `gpt-5.6-luna` with the same reasoning effort for both treatments.
- Public task contract: one shared behavioral-review contract requiring review
  of result cardinalities, state-changing side effects, compatibility behavior,
  and supporting validation. It must not name a source line, hidden assertion,
  expected output string, or solution patch.
- Treatments: pinned upstream `garfield` and `workflow-garfield` behavior; do
  not substitute a hand-written coordinator or a newly invented pipeline.
- Grading: private external hidden grader. Hidden tests, oracle metadata, and
  solution patches must never be tracked in this repository or made visible to
  either treatment.
- Evidence: raw run artifacts remain local/ignored; the committed report may
  contain only aggregate measurements and grade outcomes.

## Required sequence

1. Define and review the shared public behavioral contract.
2. Recreate each upstream invocation through a thin MCL wrapper, preserving
   its actual control flow and structured-response contract.
3. Run a small public-fixture calibration for each wrapper. In particular,
   verify that the deterministic reviewer response parses and can proceed to
   repair; do not silently relax the upstream schema.
4. Verify model identity, token accounting, workspace isolation, and that no
   private grader asset is reachable from either treatment.
5. Run one fresh paired trial and grade both working copies externally.
6. Record grade, input/cached/output tokens, cached-aware cost, latency, and
   terminal outcome. Do not infer a directional conclusion from an invalid run.

## Done when

- Both upstream treatments complete their normal control flow against the
  same public fixture and shared public contract.
- The private grader records a pass/fail result for each treatment.
- The report records exact model, tokens, cached-aware cost, latency, agent
  calls, and terminal outcome for each treatment.
- The report explicitly states whether the run is a valid comparison and any
  material deviation from upstream behavior.

## Out of scope

- Additional cases, retries to force a preferred outcome, or a generalized
  eval/dataset DSL.
- Prompting either treatment with hidden-test content or an encoded solution.
- Treating prior removed pilot runs as supporting or contradicting the upstream
  result.
