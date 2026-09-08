# Phase 46 — domain-ownership remediation

> **Status:** Step 1 inventory and the generic mission-to-hands design closure are complete; both
> await separate Codex-supervisor review. No source behavior changes are authorized until an
> individual finding has an approved implementation plan.

## Why this phase exists

Phase 43.23 established the current Application, Host, Client Runtime, and durable-service
ownership model. A follow-up review identified a higher-risk question that its acceptance did not
close: whether Janus, Naive, or other concrete missions have caused mission-specific execution
paths to leak into generic language/runtime code. A language must execute new declarative missions
without compiling a new runner or adding a mission-name branch. This phase re-evaluates the whole
repository against that requirement and removes only confirmed ownership failures.

The prior Phase 43.23 end state is evidence and a starting hypothesis, not a substitute for this
inventory. In particular, retained legacy Janus compatibility code must be proved to be a narrow
protocol boundary or be redesigned as generic mission behavior.

## Locked outcome

`ForgeMission.Core` and its generic pipeline execution model own interpretation and execution of
the MCL language. A mission declaration, expert package, supported generic step, or explicit
external-protocol adapter may supply behavior; a mission name, concrete mission class, or
mission-specific run loop must not select a different compiled execution path. A new mission must
be expressible and executable without changing generic runtime code.

This does not require a speculative plug-in framework, a generic dispatcher, a new registry, or a
second runtime. An adapter remains valid only where it contains a real external protocol, security
policy, side effect, or failure boundary with a named owner.

## Dependency-ordered spokes

| Order | Spoke | Deliverable | Gate before next spoke |
|---|---|---|---|
| 46.1 | [Repository-wide inventory and end state](phase-46.1-domain-ownership-inventory.md) | Evidence-backed ownership ledger, including the locked generic mission-to-hands boundary, and dependency-ordered remediation backlog. | Codex supervisor approves each finding's design disposition and task boundary. |
| 46.2 | [Codex-supervised remediation](phase-46.2-codex-supervised-remediation.md) | One bounded, reviewed implementation task per approved finding, followed by adversarial review and acceptance evidence. | Every approved task is complete, or any remaining item is explicitly deferred with a rationale and removal condition. |

## Scope and non-goals

The inventory covers all production source projects, command/process entry points, shared
contracts, build/publish wiring, mission/expert assets that define runtime behavior, and
architecture tests. Tests and documentation are evidence for ownership and behavior, but do not
create a second production owner. The inventory must trace each mission from declaration through
resolution, execution, provider/tool dispatch, adapters, progress, and observable result.

This phase is not permission to rewrite working components for stylistic uniformity, broaden
mission capabilities, replace a verified external protocol, redesign UI, change public wire or
storage schemas, or introduce a new deployment topology. Those changes require separately
approved designs once a finding proves them necessary.

## Governance and acceptance

Phase 46 follows the repository-wide [Codex supervisor workflow](../design/codex-supervisor-workflow.md).
It is the validated predecessor of that workflow, not a special exception or a weakened gate:

1. A supervising Codex agent owns the architecture, task card, contracts, risk assessment, and
   acceptance decision for one finding.
2. Bounded Codex sub-agents may investigate, trace dependencies, challenge the design, plan, or
   implement. They report to the supervisor and may not broaden their assigned task.
3. Before any source edit, the supervisor performs an adversarial plan review and explicitly
   approves the plan. An unresolved ownership, contract, security, or failure question returns to
   design.
4. The approved implementer alone makes the scoped change and returns actual evidence.
5. The supervisor independently reviews the diff and evidence against the approved plan and
   `Done when`; failed review returns the task for correction. An implementer never self-approves.

The supervisor applies the Design-first, Security Architecture, Engineering Philosophy,
Progressive Disclosure, Native-AOT, and Default-Path Acceptance gates. Every remediation task
states component fit, a single owner, dependency direction, public/wire/persistence effects,
failure containment, and a named verification observation. Runtime, integration, deployment, or
user-visible changes must pass the normal product route; controlled tests are supporting evidence,
not acceptance. Step 1 is documentation-only and records default-path/build/test acceptance as
N/A.

Phase 45 implementation remains unapproved until Step 1 identifies whether its planned work
depends on a runtime ownership correction. The inventory may clear it, establish an ordering
dependency, or identify a separately scoped redesign; it must not silently alter Phase 45's
contracts.

## Done when

The Step 1 ledger covers the stated repository scope and has a reviewed disposition for every
confirmed concern. Every remediation task derived from it has either passed the supervisor's
adversarial review and its required acceptance evidence, or is explicitly deferred with owner,
rationale, and removal/review condition. The final record demonstrates that a new declarative
mission does not require a mission-specific core execution path.
