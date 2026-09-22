# Phase 49.4 — Local MCL seam cleanup

> **Status:** 49.4a is accepted; its evidence is in the
> [completion record](phase-49.4-mcl-seam-cleanup_completed.md). No package or repository
> extraction is authorized by this spoke. The Phase 49.3 compatibility fence applies in full.

## Purpose and component fit

Remove only source-graph edges that contradict a component's documented ownership without changing
runtime behavior. This advances MCL's extraction readiness and reduces unnecessary build closure;
it is not a shared-library design or a repository move.

## Accepted card

49.4a removed ForgeUI's unused direct CLI project edge without a behavior or contract change; see
the [completion record](phase-49.4-mcl-seam-cleanup_completed.md). Its rollback is to restore that
single project reference (or revert the accepted merge commit).

## Later cards

49.4b isolates neutral retrieval vocabulary from concrete Scout/Grok transport. 49.4c removes
Runner/Orchestration CLI reuse through a proven owner. Each requires a separate approved design;
neither is implied by 49.4a.
