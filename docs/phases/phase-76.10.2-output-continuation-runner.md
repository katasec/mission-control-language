# Phase 76.10.2 — Runner output continuation consumer

**Status:** Scoped; implementation plan and reviews pending. Depends on the actually published
Contracts0.13.0 from [the producer](phase-76.10.1-output-continuation-producer.md).

## Scope and locked design

Requirement: “Resume Phase 76 from docs/plan.md and complete it autonomously.” This bounded
increment delivers the Runner consumer of the already-reviewed
[output continuation design](phase-76.10-output-continuations.md#failure-ownership-and-verification-gates).
That parent's locked manifest, lifecycle, failure, security and engineering decisions apply;
no new design decision or permission boundary is introduced. Separate design review is unnecessary
unless source investigation exposes a material deviation.

Owning repository: `/Users/ameerdeen/progs/forge-runner`; repository README and
`src/ForgeMission.Runner/README.md` define execution, scratch, progress and recovery ownership.
No Core, Client, Platform, infra or UI change in this increment. Native AOT remains the final
whole-phase gate. The required dependency must be inspected from the normal remote package before
plan approval; source project references and scratch package feeds are prohibited.

| Behaviour | Existing owner and seam |
|---|---|
| Retain successfully published output tuples and cumulative limits | RunWorkspace, existing collection/registration and verified path registry |
| Publish complete manifest with pause | ConversationFacts.HandsRequest and existing one-fact outbox/body publisher |
| Restore outputs into fresh segment before provider boundary | MissionCommandProcessor continuation/materialisation, RunWorkspace and existing Host body/content readers |
| Failed canonical read | Existing ConversationBodyReadException, broker redelivery/dead-letter handling; no silent empty workspace |

## Done when

1. Runner consumes actual published Contracts0.13.0 and uses its shared inert manifest grammar.
2. Every pause publishes a complete bounded manifest of successfully published outputs, including
   restored outputs and their original producing step/attempt. Persisted session/outbox retains
   references, not file bytes or a second durable output registry.
3. A fresh resumed segment verifies/stages immutable bytes without overwrite, registers relative
   paths, and seeds cumulative count/byte limits before saving the provider boundary. Restored
   files are not emitted again as new Artifacts. Segment cleanup remains joined and ephemeral.
4. Local managed functional evidence demonstrates produce → pause → dispose → new scratch →
   consume identical binary bytes, repeated pauses, no duplicate publication, limits, malformed
   references/manifest, missing/corrupt content and read outages before provider execution.
   Controlled providers/storage are labelled as component tests.
5. Sequential plan/code reviews, zero-warning managed build, required full tests and normal
   image publication pass from merged main. Record exact artifact/source provenance.

Runtime acceptance remains open until the parent's minimal API projection and normal paired
Host/Runner deployment. The supervisor then exercises the real authenticated default cloud
produce/pause/resume/binary read path; installed unified CLI acceptance is a later whole-phase gate.
UI gates are N/A because this increment has no rendered surface.

## Implementation plan

Pending the same implementer's source/package investigation, sequential reviews and explicit
supervisor approval. This scope record authorizes no executable edits.
