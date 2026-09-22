# Phase 49.4b — Retrieval contract inversion

> **Status:** Accepted 2026-09-22. The compact evidence record is
> [49.4b completion](phase-49.4b-retrieval-contract-inversion_completed.md). This card did not
> authorize a package or repository extraction.

## Purpose and component fit

Mission Core owns `kind: search` execution and reusable capability contracts. Scout owns the xAI
Grok HTTP/SSE adapter, not the provider-neutral types used by Core. Move the neutral retrieval
contract into Core so the graph is `Scout → Core.Retrieval`, never `Core → Scout`.

## Locked design

| Area | Decision |
|---|---|
| Contract | Move `IWebSearch`, `WebSearchRequest`, `WebSearchProgress`, `WebSearchResult`, `SourceRef`, and `WebSearchException` unchanged from `Scout` to `ForgeMission.Core.Retrieval`. Preserve member order, nullability, defaults, progress ordering, provider literals, and exception messages. |
| Dependency direction | Remove Core's Scout project reference; add Scout's Core project reference. Core imports `ForgeMission.Core.Retrieval`; Scout.Grok remains the sole concrete provider adapter; CLI remains its composition owner. |
| Compatibility | The moved types are not frozen durable/HTTP/SSE/pipe or persistent contracts. `ForgeMission.Core` and `ForgeMission.Scout` currently have no published MCL NuGet package or external consumer contract; every known consumer is rebuilt in this repository. This card produces/releases no package and adds no type-forwarding shim. D49-03 remains open for later package publication. |
| Stable failure | Keep this exact existing failure text at all three `PipelineRunner` dispatch sites: `kind: search requires a configured IWebSearch (Scout). Pass one to the PipelineRunner constructor.` No fallback, retry, or changed error behavior. |
| Non-goals | No package/repository move, Docker/workflow/IaC change, credential/provider/model/endpoint change, HTTP/SSE/wire/store/identity/UI change, or Phase 45 work. |

## Gates and failure boundary

| Gate | Decision |
|---|---|
| Security Architecture | Type-2 local dependency-direction correction only. No tier, public entry point, datastore, transport, identity, credential scope, or cross-context call changes. No service accesses another context's store. |
| Engineering Philosophy | One existing external seam remains `GrokWebSearch`; the move removes an accidental reverse dependency without a new abstraction, option, or fallback. |
| Failure boundary | Core owns the missing-backend failure. A taken `kind: search` without an injected `IWebSearch` raises the locked explicit `InvalidOperationException`; the caller owns injecting a backend. A focused negative test asserts the complete message. |
| Default path | N/A: CLI composition, `XAI_API_KEY`/`GROK_API_KEY` lookup, xAI endpoint/model, streaming handling, and user-visible execution remain unchanged. Controlled tests are not default-path acceptance. |
| AOT | No reflection, serialization, or provider-wire change. Scout continues direct HTTP and source-generated JSON; all managed AOT roots and the MAUI package are compared to the accepted baseline. |

## Implementation order

1. Move the unchanged contract source to `ForgeMission.Core/Retrieval/IWebSearch.cs`; change only
   its namespace and ownership documentation.
2. Invert the Core/Scout project-reference direction and update only in-repository imports:
   Core adapters/runtime, Scout.Grok, CLI composition, and Scout tests.
3. Add a network-free `SearchMissionPipelineTests` negative case that takes `kind: search` with no
   backend and asserts the locked full exception message.
4. Update Core/Scout/atlas ownership documentation and the stale API comment that names
   `Scout.SourceRef`.

## Done when

- evaluated MSBuild references show no Core → Scout edge and exactly Scout → Core;
- Core has no `using Scout`; the audit permits the locked error literal containing `Scout`;
- the full missing-backend text is proved by the new focused negative test;
- existing search pipeline and Grok SSE tests, then live Grok tests when xAI is available, pass;
- full solution build/test pass; managed AOT and MAUI observations show no new warning class;
- independent review confirms only this inversion, its in-repo consumers/tests, and required docs;
  and
- rollback is a revert of the accepted merge commit, restoring the original project edge and
  namespace without package, deployment, identity, or store remediation.
