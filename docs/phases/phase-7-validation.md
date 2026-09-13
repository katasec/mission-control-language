# Phase 7 — Validation (retired result)

> **Historical correction (2026-09-13):** This phase's reported result depended on a one-run
> comparison whose retained generated outputs did not support the documented conclusion. The
> report and generated outputs were retired. The hypothesis remains unverified.

## Goal

Run the `build-operator` example end-to-end and evaluate whether expert composition produces meaningfully better output than a single general-purpose prompt, retaining sufficient evidence to verify any conclusion.

## Completion condition

A versioned report, inputs, outputs (or immutable hashes), model configuration, and rubric support a reproducible conclusion.

## Testable hypothesis

> Expert composition improves reasoning quality, consistency, and outcomes compared to a single general-purpose prompt.

## Tasks

| # | Task | Status |
|---|------|--------|
| 1 | Run `fml run` on `build-operator` example end-to-end | Reported complete; retained evidence is insufficient |
| 2 | Run the same input against a single general-purpose prompt (no expert composition) | Reported complete; retained evidence is insufficient |
| 3 | Compare outputs using evaluation rubric (see below) | Reported complete; retained evidence is insufficient |
| 4 | Document findings | Retired; the report was not supported by the retained outputs |

## Result

No conclusion is accepted. The earlier claim that a pipeline avoided a Role-versus-ClusterRole
RBAC error was contradicted by the retained output, which used a namespace-scoped `Role`.

## Evaluation rubric

| Criterion | Question |
|-----------|----------|
| Reasoning quality | Does expert composition produce more focused, constrained reasoning per step? |
| Consistency | Is the output structure consistent and predictable across runs? |
| Reviewability | Can a human or oversight agent read the pipeline and understand the reasoning approach? |
| Grounding | Are findings tied to the specific input rather than generic advice? |
| Handoff quality | Does each step output make a useful input for the next step? |
| Overall usefulness | Is the final output more actionable than the single-prompt equivalent? |
