# Phase 49.4 — Local MCL seam cleanup

> **Status:** Accepted. Its evidence is in the
> [49.4a](phase-49.4-mcl-seam-cleanup_completed.md) and
> [49.4b](phase-49.4b-retrieval-contract-inversion_completed.md) completion records. No package
> or repository extraction is authorized by this spoke. The Phase 49.3 compatibility fence applies
> in full.

## Purpose and component fit

Remove only source-graph edges that contradict a component's documented ownership without changing
runtime behavior. This advances MCL's extraction readiness and reduces unnecessary build closure;
it is not a shared-library design or a repository move.

## Accepted card

49.4a removed ForgeUI's unused direct CLI project edge without a behavior or contract change; see
the [completion record](phase-49.4-mcl-seam-cleanup_completed.md). Its rollback is to restore that
single project reference (or revert the accepted merge commit).

49.4b moved the neutral retrieval contract to Core and inverted Scout's dependency; see its
[completion record](phase-49.4b-retrieval-contract-inversion_completed.md). Its rollback is a
normal revert; no package, deployment, identity, or store change exists.

49.4c moves shared OCI registry behavior to MissionRegistry, removes Runner/Orchestration CLI
reuse, and preserves Desktop's light AOT closure; see its
[completion record](phase-49.4c-runner-cli-dependency-cut_completed.md).

## Later cards

Local MCL seam cleanup is complete. Package and repository extraction remains governed by the
Phase 49 decision ledger and the private package foundation card.
