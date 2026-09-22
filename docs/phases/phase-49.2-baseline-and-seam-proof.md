# Phase 49.2 — Baseline and seam proof

> **Status:** Accepted 2026-09-22. Default-Path Acceptance is N/A because no product, runtime,
> integration, deployment, or repository configuration behavior changed.

## Purpose and component fit

This card establishes the measurable compatibility baseline that every later repository extraction
must preserve. It advances Phase 49's responsibility to remove accidental build-graph coupling
without treating a cosmetic project move as an AOT optimization. It changes neither an existing
component nor its public behavior.

## Locked scope

| Area | Required observation | Boundary |
|---|---|---|
| Source graph | Source universe, solution membership, evaluated direct `ProjectReference` edges, package/Docker closures, executable and AOT roots | Distinguish the three managed AOT roots—CLI, Application Host, Desktop Supervisor—from the MAUI package stage. |
| Local verification | Full build/test result and separately timed RID restore/publish for each managed AOT root and the MAUI workload/publish/package stages | Work in a fresh `codex/phase49-baseline` worktree; do not clear global caches or alter the current checkout. Any toolchain mutation is a bounded exception, never called read-only. |
| Delivery | Workflow definitions, latest workflow runs/jobs/artifacts, releases, remote rollback-tag object chain, GitHub Packages inventory/visibility/repository access, Actions permissions/environments | Record names and policy only; never record secret/variable values or credential-bearing URLs. |
| Infrastructure | `forge-infra` source commit/cleanliness; Azure resource/identity/OIDC/ACR/Container Apps/revision facts; harmless public-route header observation | No deploy, migration, database change, image push, or default-product action. A denied API call is recorded as unobserved, not inferred. |

## Design and safety gates

| Gate | Decision |
|---|---|
| Security Architecture | PASS for read-only discovery: no new tier, store, public entry point, credential, contract, or identity is introduced. Existing cross-context boundaries are observed, not changed. |
| Engineering Philosophy | PASS with bounded local-toolchain exception: one evidence record owns the facts; raw logs, binlogs, TRX, and publish output remain outside Git; no speculative abstraction or “fix while measuring” change is allowed. |
| Default-path acceptance | N/A. The harmless route observation is availability evidence only; it cannot prove a signed-in product action. |
| Failure boundary | The collector stops on a potentially mutating command unless that operation is explicitly approved and scoped. `dotnet workload restore --skip-manifest-update` unexpectedly wrote workload-install records and garbage-collected feature bands; it made no repository/cloud/product change. Its containment, no-manual-deletion rule, and future-removal condition are recorded in the evidence. A permission failure is bounded to the named capability and does not imply failure of another capability. |

## Accepted evidence

The Supervisor accepted the independent final review PASS. The durable observations and exact
commands are in [the completion record](phase-49.2-baseline-and-seam-proof_completed.md) and its
[inventory](phase-49.2-baseline-and-seam-proof_completed_inventory.md). The acceptance preserves
the observed aggregate-build failure, unobserved Make target, bounded local-toolchain mutation,
and D49-01 fence; it authorizes no extraction.

## Done when

- source, project, package, container, and executable/AOT-root dependency tables are evidence-backed;
- build/test and separate AOT/MAUI/desktop-package timings are recorded with output artifacts outside Git;
- workflow/run/artifact/release, remote rollback-tag, private GitHub Packages access, Actions,
  OIDC/ACR/infra, deployed revision/image/ingress, and harmless route facts are recorded or each
  unavailable observation is named precisely;
- the evidence contains no secret, names the bounded local-toolchain mutation and has no
  repository/package/cloud/product behavior-changing command; and
- the Supervisor accepts the evidence and records the next eligible seam card.
