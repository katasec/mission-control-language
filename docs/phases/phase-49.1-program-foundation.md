# Phase 49.1 — Repository decomposition program foundation

> **Status:** Accepted 2026-09-22. Documentation-only governance task; build, test and default-path
> acceptance were N/A because it changed no executable behavior.

## Purpose and component fit

This task owns the migration program's control plane: explicit ownership, card sequencing,
acceptance, rollback and durable handoff records. It does not change any Forge product behavior.
It advances the repository's existing hub/spoke and Codex-supervisor rules by making a multi-repo
program resumable without relying on agent memory.

## Agent personas

| Persona | Authority | Hard limit |
|---|---|---|
| Program Supervisor (root Codex) | Locks Type-1 decisions, approves/rejects plans, accepts/rejects evidence, updates status, owns PR/merge and personally performs browser/default-path inspection. | Does not implement a supervised code or IaC task. |
| Program Orchestrator | Maintains the Phase 49 ledger, identifies the next unblocked card, and may route read-only investigation/planning/review assignments. | Read-only; cannot assign executable work, settle design, approve a plan, accept work or edit files. |
| Boundary Investigator | Traces source, contracts, data/identity ownership and dependency direction. | Read-only; reports evidence, not decisions. |
| Release/Infra Investigator | Audits private package access, GitHub Actions, OIDC, ACR and Bicep implications. | Read-only; cannot create identities, repositories, packages or deployments. |
| Implementer Planner | Produces a file-by-file, verification-first plan for one card. | No edit until an explicit `PLAN APPROVED` message. |
| Single Implementer | Makes only the approved mutation on one `codex/` branch and records actual evidence. | One writer globally; cannot broaden scope or self-approve. |
| Independent Acceptance Reviewer | Fresh read-only review of the diff and evidence against the card. | Cannot replace Supervisor acceptance. |

The Program Supervisor alone assigns every executable task and issues `PLAN APPROVED`. The Program
Orchestrator may use child investigators/planners/reviewers for read-only work, but never
concurrent code writers. Agents receive only their current card, linked spoke, relevant component
READMEs and governing design docs. Their durable conclusions are folded into the ledger before the
next card.

## Required card shape

Before an implementer is assigned, every executable card records:

1. bounded outcome, component-fit statement, source owner, target owner and explicit non-goals;
2. source paths, target private repository, branch, base commit/tag and source-retirement order;
3. every public/wire/persistence/package/image contract and its consumers;
4. security answers: bounded context, tier route, datastore owner, credentials, cross-context
   contract and Type-1/Type-2 classification;
5. failure boundary: expected failure, containment owner, caller-visible result, recovery owner and
   negative-path observation;
6. focused/full/AOT verification and relevant default-path observation, or a documented N/A reason;
7. GitHub Packages access, GitHub Actions permissions, OIDC/ACR/infra impact, and named workflow
   observation;
8. roll-forward, rollback, exact source/package/image tags, and the condition that permits old
   source retirement; and
9. `Done when` conditions backed by named observations rather than intent.

No extraction card deletes old source. Deletion is a separately approved retirement card after a
new private repository's artifact, package consumers, required default path and rollback route
have all passed.

## External-change protocol

For each new private repository or private package, the approved plan must include the exact
`gh` creation command, clone location under `~/progs`, source history strategy, package identifier,
consumer repository grant, and CI identity. It must first create basic PR build/test validation.
Release/image workflows follow only after that repository proves a non-production artifact.

Before a workflow moves, retain existing ACR image names and deployment routes. A new repository
needs a dedicated GitHub OIDC federated credential and the minimum ACR/package permissions through
an approved `forge-infra` change. Image publication and deployment remain separate operations;
schema migration never joins either implicitly.

## Rollback protocol

The program starts from annotated tag `checkpoint-pre-repo-split-2026-09-22` at
`06e11af30b6eb17d2a3c32e1e5b883bbe42c0d51`. The Phase 49.2 baseline independently records the
remote tag observation before any cutover relies on it.

For every accepted card:

- retain the old artifact/route until the replacement default path is observed;
- tag source before and after cutover, publish additive prerelease packages before changing a
  consumer, and name package/image versions in the ledger;
- keep the old consumer route until the new route passes its normal product observation;
- use only an exact tag/version plus the documented `forge-infra` Make target for rollback; and
- never roll back by mutating production data or rerunning a destructive migration.

## Immediate sequence

| Next card | Bounded outcome | Gate |
|---|---|---|
| 49.2 | Reproducible baseline of solution graph, AOT timing, tests, workflows, private-package access, identities, images and stores. | 49.1 accepted |
| 49.3 | Establish a narrow no-extraction compatibility fence without changing the active product contract by inference. | 49.2 plus Supervisor review; D49-01 remains open for extraction |
| 49.4a | Remove the ForgeUI-to-CLI reference only if the focused build proves it dead. | 49.2/49.3 |
| 49.4b | Separate neutral retrieval vocabulary from concrete Grok transport. | 49.2/49.3 |
| 49.4c | Remove Runner/Orchestration reuse of CLI only through the smallest proven shared owner. | 49.2/49.3; no speculative catalog |

## Done when

The Phase 49 hub, program ledger, decision ledger, private-delivery and rollback rules, persona
limits and next-card order have been reviewed for consistency with the component atlas, active
Phase 45/46 interlock, Security Architecture, Engineering Philosophy and Codex supervisor
workflow. No executable behavior has changed.

## Acceptance evidence

PR [#166](https://github.com/katasec/mission-control-language/pull/166) introduced the foundation.
An independent reviewer rejected its initial authority and placement assumptions; corrective PR
[#167](https://github.com/katasec/mission-control-language/pull/167) was re-reviewed PASS before
merge. The accepted rules now reserve executable assignment and plan approval to the Program
Supervisor and leave shared conversation presentation and reusable Docker support as explicit open
decisions. Documentation diff checks passed; build/test/default-path verification were N/A.
