# Engineering philosophy — small, explicit, inspectable systems

> **Status: governing design and review gate.** Applies to code, infrastructure, service
> boundaries, operational workflows, and agent handoffs. It complements the mandatory
> [Security Architecture](security-architecture.md) gate; it does not replace it.

## Governing stance

Reliable systems are built by composing, verifying, recovering from, and observing imperfect
components. Reliability is a property of the system's shape, not a claim that a model, network,
subprocess, or server will not fail.

The default is deliberately boring: small components, fixed conventions, explicit boundaries, and
named failure modes. Sophistication belongs in the composed behaviour, never in an opaque or
clever individual part.

## Design sensibilities

1. **One concern, one owner, one clear failure mode.** Split conceptually different work into
   narrow components with named interfaces. An external dependency has one visible seam; callers
   do not reach through that seam to its implementation.
2. **Prefer fixed conventions to knobs.** A configuration option, runtime switch, or abstraction
   must solve a present requirement. Flexibility is a cost, not a default virtue.
3. **Contain risk structurally.** Prefer a separate deliberate operation, bounded identity,
   queue, transaction, sandbox, or platform boundary to a warning, checklist, scrubber, or a
   human remembering the right sequence.
4. **Trust authoritative boundaries.** Use the OS, database, provider SDK, or platform to enforce
   what it owns. Do not add stale pre-checks or duplicate validation that creates false confidence.
5. **Make consequential behaviour explicit.** State ownership, mutation, control-flow changes,
   retries, and failure semantics in contracts. Default to opt-in for behaviour that can alter
   control flow or shared state; fail loudly rather than silently falling through.
6. **Progressive disclosure at every altitude.** A file, module, plan, document, and operational
   workflow reveal *what* first and expose *how* through named steps. A reader should isolate the
   relevant problem without reconstructing the whole system.
7. **Extract for a real seam, not anticipated reuse.** Introduce an interface, helper, package,
   or service when it improves present readability or enforces a real boundary. Three similar
   lines are preferable to a speculative framework.
8. **Verification is part of the design.** Define the observation that proves the important
   outcome: test result, live log, deployed-resource query, or user-visible behaviour. Written or
   merged is not done.

## Bad-smell review

Before approving a design or implementation plan, record the relevant answers in its active
spoke. “Not applicable” is acceptable only with a reason.

| Question | Passing answer |
|---|---|
| Is a component doing unrelated work? | Each responsibility has a named owner, interface, and failure boundary. |
| Is a new knob, option, or abstraction proposed? | A present requirement justifies it; otherwise use the simpler fixed convention. |
| Does safety rely on warning, cleanup, or memory? | A structural boundary contains the risk, or a temporary exception has an explicit removal path. |
| Is an external dependency accessed from multiple places? | One narrow adapter/seam owns it. |
| Is consequential behaviour implicit? | Contract, ownership, ordering, retry, and failure semantics are explicit. |
| Can a fresh reader locate the main flow? | Intent is visible first; detail is behind small named steps. |
| What proves success? | A named, proportionate verification observation is part of “Done when.” |

An implementation task is not build-ready if a material smell remains unexplained or is deferred
to implementation. The detailed code-reading rules live in [Code Style](code-style.md); tiering,
data ownership, and identity rules live in [Security Architecture](security-architecture.md).

## Working consequences

- Keep command entry points thin and put business behaviour behind focused services.
- Keep side effects isolated and named; do not mix network, filesystem, process, or datastore work
  into otherwise pure decision logic.
- Keep deployment, migration, and destructive actions as separate deliberate operations when that
  limits blast radius.
- Hand off self-contained, dependency-ordered tasks with concrete contracts and “Done when”
  evidence so execution is narrow and reviewable.
- Treat agent/runtime tooling as another system boundary: enforce material safety rules through
  code, IaC, hooks, or CI where feasible—not instructions alone.
