# Phase 76 — Unified cloud mission execution

**Status:** Supervisor workflow authorized on 2026-10-09. Product requirements remain recorded in
[the requirements spoke](phase-76.1-unified-cloud-run-requirements.md); the supervisor's
[contract proposal](phase-76.2-unified-cloud-run-contracts.md) had passed technical reviews for
the former image-owned-only policy. On 2026-10-10 the operator chose arbitrary user-vetted code
and deferred execution permissions, then approved persistent Conversation Host content and ephemeral
Runner execution. Revised executable contracts passed full current reviews and are supervisor-locked. The independent OCI integrity/authentication prerequisite is merged, published as
Katasec.OciClient0.5.0, and accepted from a fresh restored-package Native AOT consumer. Core0.1.8 is merged, normally published and accepted through fresh remote-package and installed default checks; the Runner hosting prerequisite is verified. Later consumer tasks, deployment
and unified runtime acceptance remain open.

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
| Current documentation handoff | OCI, hosting and Core verified; [content contract producer](phase-76.6-mission-content-contracts.md) plan approved, implementation in progress. |
| Current cloud implementation | Source inspection only; [baseline and limits](phase-76.1-unified-cloud-run-requirements.md#current-baseline--observed-not-the-new-contract) |
| Contract design | Revised generic design locked after full round 7 PASS; [contracts and current work](phase-76.2-unified-cloud-run-contracts.md#current-work) |
| OCI prerequisite | Published 0.5.0 accepted; [API contract](phase-76.3-oci-integrity-auth.md), [product PR/publication/default evidence](phase-76.3-oci-integrity-auth_completed.md) |
| Cloud implementation and acceptance | [Core producer](phase-76.4-core-cloud-primitives.md) verified; Host, Runner and Client consumers remain; unified default-path acceptance not started |
| Process-hosting prerequisite | [Runner init](phase-76.5-runner-process-hosting.md) verified: merged/published0.20.6, normal Make deployment, actual process/source and installed ChatPASS |

## Dependency-ordered work

| Order | Task | Done when |
|---|---|---|
| 1 | Capture operator requirements | The spoke records the final decisions, rejected alternatives, ownership, current facts, and remaining contract work; documentation checks pass. |
| 2 | Supervisor designs the contracts | Every item in the spoke's contract-work table is defined against the owning repositories, with explicit failure/recovery behavior, security/engineering gates, and default-path observations. Sequential independent design reviews pass before the design is locked. |
| 3 | Deliver the independent OCI prerequisite | Its own complete design/plan/code reviews and restored published-package acceptance pass. It grants no approval for the pending cloud boundaries. |
| 4 | Plan and implement remaining repository tasks | Follow the [supervisor workflow](../design/supervisor-workflow.md); no executable changes before implementer-plan approval. Define dependency-ordered implementation spokes after the cloud contracts are locked. |
| 5 | Merge, publish/deploy, accept, close | Required checks and the real default path pass, including cloud reasoning and client tool execution across multiple declared folders; completion evidence names actual artifacts and observations. |

## Required delivery coverage

These are required outcomes of tasks 4–5 above, not completed work. Detailed implementation
spokes follow the reviewed contracts in dependency order; OCI, hosting and the bounded Core
producer are verified.
The implementation areas are coverage groups, not a fixed count of PRs. Keep future tasks small
and independently deliverable: complete review, merge/publication and applicable default-path
acceptance for one bounded increment before starting the next.

Operator clarification2026-10-10: **"AOT is at the end after everything works..."**, **"not for testing"**.
Use managed builds and functional tests during implementation. Native AOT is a final delivery
gate after the complete unified flow works, not a repeated per-increment test. The supervisor's
extra Docker native-consumer route was stopped at the operator's correction; do not resume it.

| Required outcome | Implementation owner / task coverage | Completion observation |
|---|---|---|
| One local/OCI `forge run`, named text and file inputs | Core/package primitives → shared Client preparation → CLI/source resolution | Installed CLI runs both sources through the same real cloud path; each named file reaches its matching input |
| Cloud expert workflow and user-vetted executable code | Core adapters/package validation → Runner execution | Real multi-expert run, arbitrary packaged executable fixture, OCR text/PDF and fresh-worker continuation succeed |
| Local Read/Write/Edit across declared folders | Client Projects/Sessions/Hands | Independent byte inspection in both disposable roots; outside/symlink/terminal requests denied; empty roots grant no file tools |
| Persistent conversation, steps, inputs and generated files; ephemeral Runner scratch | Host contracts/content storage/admission → Runner staging/progress | Authenticated history and artifact bytes remain readable after CLI exit and Runner segment replacement |
| Opaque final stdout, diagnostics stderr, clean command migration | Shared Client follow → CLI/config/registry and clean cut | Exact final bytes including JSON/empty text, flag-independent durable step history; removed syntax rejected |
| Delivered product on supported defaults | Normal package publication → infrastructure deployment → supervisor acceptance | Merged source/package/image provenance, required checks and default-path actions recorded; all touched repos clean on current main |

## Next

OCI, [Runner hosting](phase-76.5-runner-process-hosting.md) and [Core](phase-76.4-core-cloud-primitives.md)
are verified. Deliver the small [content contract producer](phase-76.6-mission-content-contracts.md)
through sequential reviews and published-package acceptance, then Host content storage/admission. Future consumers adopt exact
published OCI0.5.0/Core0.1.8 packages. Follow the
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
and plan approval. The operator has settled executable trust and persistent Host / ephemeral Runner
ownership. Their technical execution/environment and content contracts have passed full current reviews and are locked. The independent OCI prerequisite
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
