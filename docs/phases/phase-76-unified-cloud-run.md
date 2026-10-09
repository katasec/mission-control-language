# Phase 76 — Unified cloud mission execution

**Status:** The initial MCL package-input slice and the Desktop compatibility slice are merged;
the OCI client package is published. A bounded MCL process-runner repair is verified locally and
awaits merge before the blocked package publication can be retried. The remaining cloud-run slices
are pending. **Build-ready.**

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
| Initial MCL package-input slice | Merged as [forge-mcl PR #73](https://github.com/katasec/forge-mcl/pull/73); managed suite passed before release retry. |
| OCI client dependency | `Katasec.OciClient` 0.4.0 published after [PR #3](https://github.com/katasec/oci-client-dotnet/pull/3). |
| Desktop compatibility | Merged as [forge-desktop PR #10](https://github.com/katasec/forge-desktop/pull/10). |
| Package publication unblock | Bounded `ExecExpertRunner` macOS closed-stdin repair verified locally: 1,003 passed, 0 failed, 8 skipped; awaiting PR/merge and release retry. |
| Contract design | Locked; [Phase 76.2 contract design](phase-76.2-unified-cloud-run-contracts.md) |
| Implementation plan | Approved; [Phase 76.3 plan](phase-76.3-unified-cloud-run-implementation.md) |
| Remaining implementation and acceptance | Pending after package publication; [proposed default-path evidence](phase-76.1-unified-cloud-run-requirements.md#future-default-path-acceptance) |

## Supervisor workflow status

| Step | Start (Dubai, UTC+04:00) | End (Dubai, UTC+04:00) | Status |
|---|---|---|---|
| 0 — Scope | Unavailable; not recorded at boundary | Unavailable; not recorded at boundary | Completed |
| 1 — Supervisor contract design | Unavailable; not recorded at boundary | Unavailable; not recorded at boundary | Completed |
| 1a — Simplicity design reviews | Unavailable; not recorded at boundary | Unavailable; not recorded at boundary | Completed |
| 1b — Ownership design reviews | Unavailable; not recorded at boundary | Unavailable; not recorded at boundary | Completed |
| 2 — Implementer plan | Unavailable; not recorded at boundary | Unavailable; not recorded at boundary | Completed |
| 2a — Simplicity plan reviews | Unavailable; not recorded at boundary | Unavailable; not recorded at boundary | Completed |
| 2b — Ownership plan reviews | Unavailable; not recorded at boundary | Unavailable; not recorded at boundary | Completed |
| 2c — Supervisor plan approval | 2026-10-09; exact time not recorded | 2026-10-09; exact time not recorded | Completed |
| 1c — OCI dependency design correction | 2026-10-09T03:33:22+04:00 | 2026-10-09T03:44:25+04:00 | Completed |
| 2d — Revised plan review/approval | 2026-10-09T03:33:22+04:00 | 2026-10-09T03:44:25+04:00 | Completed |
| 3 — Initial MCL package-input implementation | Unavailable; not recorded at boundary | Unavailable; pre-current-timestamp capture | Completed (PR #73 merged) |
| 3a — Desktop compatibility implementation | Unavailable; not recorded at boundary | Unavailable; pre-current-timestamp capture | Completed (PR #10 merged) |
| 3b — Package-publication unblock plan/reviews | Unavailable; not recorded at boundary | Unavailable; pre-current-timestamp capture | Completed |
| 3c — Package-publication unblock implementation/reviews | Unavailable; not recorded at boundary | Unavailable; pre-current-timestamp capture | Completed locally; 1,003 passed, 0 failed, 8 skipped |
| 4 — Merge/publish/deploy | 2026-10-09T09:39:44+04:00 | Pending | In progress — commit, PR, merge, then retry package publication |
| 5 — Default-path acceptance | Pending | Pending | Pending |
| 6 — Closure | Pending | Pending | Pending |

## Dependency-ordered work

| Order | Task | Done when |
|---|---|---|
| 1 | Capture operator requirements | The spoke records the final decisions, rejected alternatives, ownership, current facts, and remaining contract work; documentation checks pass. |
| 2 | Supervisor designs the contracts | Every item in the spoke's contract-work table is defined against the owning repositories, with explicit failure/recovery behavior, security/engineering gates, and default-path observations. Sequential independent design reviews pass before the design is locked. |
| 3 | Plan and implement bounded repository tasks | Follow the [supervisor workflow](../design/supervisor-workflow.md); no executable changes before implementer-plan approval. Define dependency-ordered implementation spokes after the contracts are locked. |
| 4 | Merge, publish/deploy, accept, close | Required checks and the real default path pass, including cloud reasoning and client tool execution across multiple declared folders; completion evidence names actual artifacts and observations. |

## Next

Merge the verified MCL runner repair, retry package publication on its default release path, then
continue the dependency-ordered cloud-run implementation slices. Native AOT publication remains
the final confirmation only, after managed testing is complete.

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
