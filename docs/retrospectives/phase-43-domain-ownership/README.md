# Phase 43 — domain ownership

**Recommended end state finalized 2026-09-06. Implementation has not started.**

Client Runtime means the local capability execution and authorization library (Bob). The Application layer owns Project, submission, conversation and history use cases. A thin Application Host exposes their shared API and serves Presentation in the existing local child process. Fourteen concerns do not imply fourteen libraries, services or processes.

| Read | Purpose |
|---|---|
| [End state](end-state.md) | All fourteen dispositions, library/process boundaries, data and credential owners. |
| [Contracts](contracts.md) | Exact existing types to preserve, new local execution boundary, lifecycle and failure semantics. |
| [Reference evidence](references.md) | Pinned MCL, DeepSeek Harness, OpenHands and OpenCode sources; what is borrowed and what is not. |
| [Implementation plan and assignment](../../phases/phase-43.23-domain-ownership.md) | Ordered work, gates, acceptance and the implementer's first assignment. |

## Baseline and authority

The baseline is reconstruction [PR #99](https://github.com/katasec/mission-control-language/pull/99), merged at `ef2e636cd37db6702364745c337d3156674541c6`. The operator reported on 2026-09-06 that reconstruction finished and merged; merge and source state were independently checked. Older reconstruction spokes still contain pending rollout/visible-acceptance entries. This design does not claim to have rerun those checks, and does not treat those older entries as a new blocker to ownership implementation planning. The refactor must supply its own default-path evidence.

The numbered files preserve the original fourteen concerns recorded against `5faf6f2`. Their historical source locations and “no design approved” descriptions refer to that inventory, not the finalized recommendation. The documents linked above now determine their disposition. In particular, the reconstruction already deleted `ProjectControlRuntimeSession`; this design never recreates it.

This is the forward-looking ownership amendment linked by [Forge Architecture](../../design/forge-architecture.md). Existing deployment descriptions remain current until implementation updates them. The migration preserves product behavior and names explicit exceptions; it is not permission to generalize missions, expand grants or introduce a new runtime framework.

Documentation-only acceptance: **N/A** for runtime/default-path, build and test execution. Design verification consists of source/contract inspection, reference and relative-link checks, complete fourteen-concern coverage, dependency/failure review and diff review.
