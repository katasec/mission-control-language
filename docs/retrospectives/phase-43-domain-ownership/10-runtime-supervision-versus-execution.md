# Concern 10 — Runtime supervision versus execution

> **2026-09-06 disposition:** resolved in the [finalized end state](end-state.md#disposition-of-all-fourteen-concerns); implementation follows [43.23](../../phases/phase-43.23-domain-ownership.md). The original inventory below is historical.

Recorded: 2026-09-05. Status: boundary already substantially separated; preserve and review after Project Mission reconstruction reaches a verified baseline. Evidence inspected through reconstruction commit `5faf6f2`. This is not an approved refactoring plan.

## Responsibilities and present owners

Resolve runtime endpoints, start required processes or containers, check readiness, establish local tunnels, and clean up owned children on shutdown or failure.

Current locations: `src/ForgeMission.Orchestration/` and `src/ForgeMission.Desktop/`, particularly `MissionRuntimeResolver`, `ConversationRuntimeBootstrap`, `DesktopBoot`, and `DesktopLifecycle`. The native Host owns the window separately.

## Boundary concern

Bob executes authorized local requests. Preparing the services he communicates with and supervising the application's process tree are separate infrastructure responsibilities. A Docker capability requested by a mission is also distinct from Docker used to provision the Mission Runtime.

Phase 43 already corrected this mismatch: commits `1312a85` and `5ddbfb3` moved runtime startup out of Client Runtime on August 4. Commit `47f0059` separated the Supervisor from the native Host on August 17. The [43.13 record](../../phases/phase-43.13-mission-runtime-orchestration.md) explains the original drift.

## Later discussion

Check that future application extraction preserves this ownership, readiness guarantees, credential placement, and cleanup responsibility. Do not treat this as a confirmed current defect or invent another supervisor. Default-path acceptance: N/A — documentation only.
