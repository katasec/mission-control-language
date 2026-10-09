# Phase 76 — Unified cloud mission execution

**Status:** Supervisor workflow authorized on 2026-10-09. Product requirements remain recorded in
[the requirements spoke](phase-76.1-unified-cloud-run-requirements.md); the supervisor's
[contract proposal](phase-76.2-unified-cloud-run-contracts.md) had passed technical reviews for
the former image-owned-only policy. On 2026-10-10 the operator chose arbitrary user-vetted code
and deferred execution permissions. Executable contracts require redesign/full review; Host-owned
content remains an unapproved proposal, with an explanation requested. The independent OCI integrity/authentication prerequisite is merged, published as
Katasec.OciClient 0.5.0, and accepted from a fresh restored-package Native AOT consumer. No cloud implementation approval, deployment
or runtime acceptance. **Cloud work is not build-ready.**

## Intent

One `forge run` command selects a local or OCI mission and runs its reasoning in the cloud.
The client executes requested tools through Hands. A required `project.json` supplies stable
Project identity and multiple workspace folders. Inputs are named strings or local file artifacts;
the final response is opaque text. Cloud conversations and runs remain durably linked to the Project.

## Lookup

| Item | State / evidence |
|---|---|
| Product decisions | All four decision areas settled; [locked requirements](phase-76.1-unified-cloud-run-requirements.md#four-product-decisions) |
| Prior requirements verification | 2026-10-09: `git diff --check` and local Markdown/JSON check PASS (31 local links/anchors, two JSON examples, balanced fences, top-level phase index). Product tests/default-path acceptance N/A for that requirements-only task. |
| Current documentation handoff | Local validation PASS: eight files, 60 local links/anchors, two JSON examples, balanced fences and whole hub checked; diff checks PASS. OCI product evidence is recorded separately. |
| Current cloud implementation | Source inspection only; [baseline and limits](phase-76.1-unified-cloud-run-requirements.md#current-baseline--observed-not-the-new-contract) |
| Contract design | Executable policy decided; generic execution redesign/reviews and content choice open; [contracts and current work](phase-76.2-unified-cloud-run-contracts.md#current-work) |
| OCI prerequisite | Published 0.5.0 accepted; [API contract](phase-76.3-oci-integrity-auth.md), [product PR/publication/default evidence](phase-76.3-oci-integrity-auth_completed.md) |
| Cloud implementation and acceptance | Not started; [proposed default-path evidence](phase-76.1-unified-cloud-run-requirements.md#future-default-path-acceptance) |

## Dependency-ordered work

| Order | Task | Done when |
|---|---|---|
| 1 | Capture operator requirements | The spoke records the final decisions, rejected alternatives, ownership, current facts, and remaining contract work; documentation checks pass. |
| 2 | Supervisor designs the contracts | Every item in the spoke's contract-work table is defined against the owning repositories, with explicit failure/recovery behavior, security/engineering gates, and default-path observations. Sequential independent design reviews pass before the design is locked. |
| 3 | Deliver the independent OCI prerequisite | Its own complete design/plan/code reviews and restored published-package acceptance pass. It grants no approval for the pending cloud boundaries. |
| 4 | Plan and implement remaining repository tasks | Follow the [supervisor workflow](../design/supervisor-workflow.md); no executable changes before implementer-plan approval. Define dependency-ordered implementation spokes after the cloud contracts are locked. |
| 5 | Merge, publish/deploy, accept, close | Required checks and the real default path pass, including cloud reasoning and client tool execution across multiple declared folders; completion evidence names actual artifacts and observations. |

## Next

Explain/settle Host-owned content, redesign generic executable contracts under the recorded user-vetted
code policy, then obtain full current design reviews before locking and planning implementation. The OCI prerequisite is accepted;
future consumers adopt the exact published 0.5.0 package. Follow the
[current design state](phase-76.2-unified-cloud-run-contracts.md#current-work); implementation
must not infer missing contracts.

## Supervisor autonomy

The requirements are sufficient to start the supervisor's design stage; they do not require
the operator to write a technical design. The supervisor investigates, defines reversible
technical details within the agreed requirements, runs sequential independent reviews, locks
the design, and approves the implementer's plan before implementation. These approvals belong
to the supervisor, not a repeated human confirmation at every stage.

Zero further operator involvement is not yet assured. The governing
[scope stage](../design/supervisor-workflow.md#required-loop) says: **"Type-1 decisions go to the
operator."** New cross-context contracts, ownership, public-entry-point or credential-boundary
decisions must therefore be presented as concrete proposals where existing operator decisions
do not already settle them. A requirement change also returns to the operator. This record
was requirements-only. The operator has now invoked the full flow and authorized autonomous
execution within the agreed requirements. The supervisor handles reversible technical decisions
and plan approval. The workflow's explicit Type-1 operator gate remains applicable to the
remaining public content boundary. Executable trust is decided by the operator; its technical
execution/environment design still needs full review. The independent OCI prerequisite
narrows existing library authority and creates no cloud boundary or permission grant.

## Prior requirements-documentation gates

Documentation-only: supervisor edits and validates. Product tests, Native AOT, visual acceptance,
and Default-Path Acceptance are **N/A**. Security and engineering ownership requirements for the
future product design are recorded in the spoke, not claimed reviewed or implemented. Existing
supported defaults remain unchanged until product implementation and acceptance replace them.

## Documentation timing

| Stage / role / round | Start (UTC) | End (UTC) | Wall | Evidence |
|---|---|---|---|---|
| `[design:supervisor] Requirements record` | Unavailable; not recorded | Unavailable; not recorded | Unavailable | Requirements spoke and source observations |
| `[validate:supervisor] Documentation` | Unavailable; not recorded | 2026-10-08 23:08:06 | Unavailable | Markdown links/anchors, JSON examples, fences, phase index, and staged diff checks |

Product timing and tokens are N/A. Missing timestamps are not reconstructed.
