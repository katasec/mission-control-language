# Phase 46.2 — Codex-supervised remediation

> **Status:** blocked on the approved findings ledger in [Phase 46.1 — repository-wide ownership inventory and end state](phase-46.1-domain-ownership-inventory.md).
> Parent: [Phase 46 — domain-ownership remediation](phase-46-domain-ownership-remediation.md).

## Why this spoke exists

An inventory finding is not authorization to edit code. This spoke turns one approved finding at a
time into a small, dependency-ordered task with a supervising Codex agent as its architect and
acceptance authority. Codex sub-agents replace Claude for this initiative only.

## Per-finding delivery protocol — locked

| Stage | Owner | Required result |
|---|---|---|
| Scope card | Supervising Codex | Names one finding, component fit, exact owner/end state, non-goals, affected paths, migration order, and `Done when`. |
| Investigation and challenge | Bounded Codex sub-agents | Supply source evidence, dependency trace, contract/failure analysis, and adversarial objections to the supervisor; no source edits. |
| Implementation plan | Assigned Codex implementer | Returns a file-by-file plan only, including tests, default-path facts, failure containment, and open questions; no edits. |
| Adversarial approval | Supervising Codex | Explicitly accepts or rejects the plan after checking ownership, generic-runtime compliance, security/data boundaries, failure semantics, unnecessary abstractions, migration risk, and verification against Phase 46.1 and all governing gates. Missing facts return to design. |
| Implementation | Assigned Codex implementer | Makes only the approved scoped edits on an isolated `codex/` branch and reports the exact changes, test results, default-path observation where applicable, and any deviation. |
| Acceptance review | Supervising Codex | Reviews the diff, tests, negative cases, default journey where applicable, and component docs against `Done when`; accepts, rejects for correction, or records a genuine deferment. |

No sub-agent may implement before explicit supervisor approval. No implementer may approve its own
plan, resolve a design question by inference, enlarge scope, or mark a task complete. A supervisor
may use additional sub-agents for independent adversarial review, but the supervisor alone makes
the design and acceptance decision.

## Required task card

Every remediation task derived from the ledger must contain the following before an implementer is
assigned:

1. Finding ID, concrete ownership defect, authoritative target owner, and why the move/merge/delete
   advances that component's documented purpose.
2. Exact source and contract boundaries, compatibility decision, migration/deletion order, and all
   explicit non-goals.
3. Security review: bounded context/data owner, public entry point, tier/credential impact, Type-1
   or Type-2 decision, and enforcement/proof.
4. Failure-boundary record: expected failure, containment owner, caller-visible result, recovery,
   and negative-path observation.
5. Verification plan: focused tests; full required build/test/AOT checks; and default-path facts,
   action, and outcome for every executable behavior change. User-visible work also names its visual
   reference and presentation-parity result.

## Implementation constraints

- Prefer deletion, consolidation, or a direct move to the named owner over a wrapper, bridge,
  registry, framework, option, or generic dispatcher.
- A mission-specific branch may not remain in generic runtime code merely to preserve a legacy
  path. Retain a concrete adapter only when its external protocol/authority cannot be expressed at
  the generic mission boundary, and document that narrow reason.
- Preserve externally observable behavior unless the approved task explicitly changes a contract.
  Never silently change retry, cancellation, persistence, authorization, or error semantics during
  cleanup.
- Maintain Native AOT safety: source-generated JSON, no new reflection/dynamic discovery, and no
  new warning suppression without the required preservation rationale.
- Update nearest component README and ownership/design documentation whenever an approved change
  alters public responsibility, dependency direction, or “Why this exists.”

## Task ledger

| Task | Finding | Status | Supervisor approval | Acceptance evidence |
|---|---|---|---|---|
| [Task A — Core generic root-scoped continuation](phase-46.2-task-a-core-continuation.md) | F46.1-01 | Core implementation accepted; dependent durable migration remains. | Approved 2026-09-07 after independent planning and adversarial review. | Focused, full-suite, AOT, and independent-review evidence accepted; integrated default path remains pending Phase 45/46. |
| [Task B — immutable profiles and durable hands](phase-46.2-task-b-profile-hands.md) | D46.1-01/D46.1-02 | Implementation accepted; Worker/default-Desktop integration remains. | Approved 2026-09-07 after scope correction: profile/attachment/Bob/Host facts only. | Independent review accepted; focused, full-suite, and AOT evidence recorded. Final Desktop default path remains dependent on Task C and Phase 45.3. |
| [Task C — generic durable Worker execution and progress](phase-46.2-task-c-generic-worker-execution.md) | F46.1-02/F46.1-03 | Approved for implementation. | Approved 2026-09-07: generic immutable package, default durable execution, one Worker-owned provider binding, generic facts, and removal of compiled mission branches. | Pending separate implementation and adversarial acceptance; Desktop/TUI default-path proof remains Phase 45.3 work. |

## Step 2 done when

Each task in the approved Phase 46.1 remediation backlog is accepted through the delivery protocol
or explicitly deferred with a named owner, rationale, review condition, and removal condition. The
final acceptance record includes source and behavior evidence that adding a declarative mission no
longer requires a mission-specific compiled core execution path.
