# Phase 76 — Unified cloud mission execution

**Status:** Product requirements agreed with the operator on 2026-10-09 and recorded in
[the requirements spoke](phase-76.1-unified-cloud-run-requirements.md). No product changes,
implementation plan approval, deployment, or runtime acceptance in this session. **Not build-ready.**

## Intent

One `forge run` command selects a local or OCI mission and runs its reasoning in the cloud.
The client executes requested tools through Hands. A required `project.json` supplies stable
Project identity and multiple workspace folders. Inputs are named strings or local file artifacts;
the final response is opaque text. Cloud conversations and runs remain durably linked to the Project.

## Lookup

| Item | State / evidence |
|---|---|
| Product decisions | All four decision areas settled; [locked requirements](phase-76.1-unified-cloud-run-requirements.md#four-product-decisions) |
| Documentation verification | 2026-10-09: `git diff --check` and a local Markdown/JSON check PASS (31 local links/anchors, two JSON examples, balanced fences, top-level phase index). Product tests/default-path acceptance N/A. |
| Current implementation | Source inspection only; [baseline and limits](phase-76.1-unified-cloud-run-requirements.md#current-baseline--observed-not-the-new-contract) |
| Contract design | Next; [required design work](phase-76.1-unified-cloud-run-requirements.md#contract-work-before-implementation) |
| Product implementation and acceptance | Not started; [proposed default-path evidence](phase-76.1-unified-cloud-run-requirements.md#future-default-path-acceptance) |

## Dependency-ordered work

| Order | Task | Done when |
|---|---|---|
| 1 | Capture operator requirements | The spoke records the final decisions, rejected alternatives, ownership, current facts, and remaining contract work; documentation checks pass. |
| 2 | Supervisor designs the contracts | Every item in the spoke's contract-work table is defined against the owning repositories, with explicit failure/recovery behavior, security/engineering gates, and default-path observations. Sequential independent design reviews pass before the design is locked. |
| 3 | Plan and implement bounded repository tasks | Follow the [supervisor workflow](../design/supervisor-workflow.md); no executable changes before implementer-plan approval. Define dependency-ordered implementation spokes after the contracts are locked. |
| 4 | Merge, publish/deploy, accept, close | Required checks and the real default path pass, including cloud reasoning and client tool execution across multiple declared folders; completion evidence names actual artifacts and observations. |

## Next

Design and review immutable mission admission, named inputs/artifact bindings, Project/session
identity, config/auth resolution, multi-folder Hands authority, and result/diagnostic contracts.
Requirements are settled; DTOs, APIs, failure boundaries, and migration details are not yet a
reviewed implementation design. Do not infer missing contracts while building.

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
does not delegate those decisions or authorize product implementation; invoking the full flow
is a separate instruction from documenting its requirements.

## Gates for this documentation task

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
