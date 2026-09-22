# Phase 49.4b — Retrieval contract inversion: evidence

> **Status:** Accepted 2026-09-22. Raw publish output is outside Git at
> `/private/tmp/phase49-4b-20260922`.

## Outcome

The unchanged provider-neutral retrieval contract now belongs to
`ForgeMission.Core.Retrieval`; Scout depends on Core and retains only the Grok HTTP/SSE adapter.
The types are not durable, HTTP/SSE/pipe, persistent, or published-package contracts. Core and
Scout have no published MCL NuGet consumer; all consumers were rebuilt together and no package,
release, or type-forwarding shim was produced.

## Acceptance observations

| Check | Observation |
|---|---|
| Graph | Evaluated references: Core → Parser only; Scout → Core only. Core has no `using Scout`; all three locked missing-backend literals are unchanged. |
| Failure boundary | New network-free taken-search/no-backend test asserts the complete existing `InvalidOperationException` message. |
| Focused tests | Search pipeline plus Grok stream tests passed 7/7. |
| Full build | `dotnet build src/ForgeMission.slnx --no-restore` passed, 0 warnings and 0 errors. |
| Full suite | `dotnet test src/ForgeMission.slnx --no-restore --no-build` passed 731, skipped 6, failed 0, including live Grok cases. |
| AOT/MAUI | CLI, Application Host, Desktop Supervisor, and MAUI package publishes passed. Only the known macOS linker compatibility warnings appeared; no new warning class. |
| Independent review | PASS for design, scope, graph, stable failure, and compatibility constraints. |

## Gates and rollback

Security and Default-Path Acceptance are N/A: no public route, tier, store, identity, credential,
provider/default, or user-visible behavior changed. The Core-owned explicit missing-backend error
remains the failure boundary. Rollback is a revert of the accepted merge commit; it restores only
the former namespace and project-reference direction.
