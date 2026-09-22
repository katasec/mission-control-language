# Phase 49.3 — Compatibility freeze for local seam cleanup

> **Status:** Accepted 2026-09-22. This is a narrow Type-1 compatibility fence: it admits only the
> behavior-preserving local MCL seam-cleanup card. It does not admit package publication,
> repository extraction, deployment change, or Phase 45.5 implementation.

## Decision

For the limited non-extraction 49.4 scope, the Program Supervisor records a **narrow
compatibility fence** under the operator's standing program authorization. It freezes the
implemented Desktop/Conversation boundary against source baseline
`99eb355b1b896dd4b0b9f9c76f9060421f32b26a` and accepted evidence in
[49.2](phase-49.2-baseline-and-seam-proof_completed.md); it does not settle D49-01 for extraction.

Phase 45.5 is deliberately excluded: its static preview has no real open/replay/tail/send contract
yet, and it requires an existing Project plus Approved mission (or separately designed selection
route). It remains a future additive design, not a silently frozen or cancelled feature.

## Frozen implementation envelope

| Boundary | Must remain compatible |
|---|---|
| Durable contract | `Conversations.Contracts` positional fields, stable/additive string-enum values, numeric enum ordinal order, deterministic IDs, `DurableMissionLaunch`/package/hash/profile semantics, and create/list/submit/retry/cancel/evaluation records. Source-generated JSON only. |
| Durable service | Host remains the sole Table/Blob writer and sequence allocator; Worker only consumes queue commands and publishes progress. No store, provider credential, Service Bus direction, tenant, or identity expansion. |
| HTTP/SSE | Existing `/health`, `/conversations/*`, mission-conversation, evaluation, mission-hands and project-run routes, deterministic IDs, replay-to-live SSE behavior, and existing typed error/result semantics. |
| Application/transport | Existing `Application.Transport` action/event shapes, numeric application enums, separate camel-case string-enum conversation-relay JSON context, Project-owned launch resolution, pinned version/profile/package and ephemeral attachment semantics. Presentation never grants authority. |
| Desktop/default path | Zero-argument Desktop supervision, dynamic Application Host loopback protocol, native Host pipe framing, default Mission runtime route, default Conversation `127.0.0.1:18080` health/tunnel ownership, and reverse cleanup ordering. |
| UI boundary | Existing Phase 47 preview remains fixed design evidence only. No change to its markup, focus, keyboard, ARIA, renderer ownership, or CSS placement is bundled into seam cleanup. D49-05 remains open. |

## Admission and invalidation rules

49.4 may change only the proven MCL internal seams: ForgeUI's dead CLI reference, Core's concrete
Scout coupling, and Runner's CLI implementation dependencies. It may not change a frozen type,
route, payload, wire serialization, persistent fact, queue direction, credential/identity, default
endpoint/startup, Docker ownership, MCL package bytes, or provider execution semantics visible to
the durable path.

Any required change to the envelope invalidates this freeze and returns to D49-01 review. Any new
Phase 45.5 contract must be additive, separately owned by its Phase 45 spoke, and follow the
future consumer-version policy in D49-03.

## Verification required from 49.4

- source graph confirms no unauthorized boundary move and no cross-repository reference exists;
- focused contract, adapter, Worker, Application, Desktop, Orchestration and Presentation tests
  named by the affected task still pass where buildable;
- frozen JSON/SSE/pipe payload fixtures round-trip unchanged;
- CLI, Application Host, Desktop Supervisor AOT and MAUI package observations are compared to 49.2;
- the aggregate suite is run in an environment that has the required local project dependencies;
  a missing dependency is reported as a constraint, never treated as a pass;
- no default-path acceptance is claimed unless 49.4 changes runtime behavior. Phase 45.5's real
  default-path/browser acceptance remains open.

## Extraction remains blocked

D49-02 (repository disposition), D49-03 (contract consumer-version/private package policy),
D49-05 (shared renderer placement), D49-06 (Docker ownership), and Phase 45.5's missing live-chat
contract still block package or repository extraction. The 49.2 missing-local-project observation
is historical; 49.4a proved the aggregate suite on a capable local environment.
