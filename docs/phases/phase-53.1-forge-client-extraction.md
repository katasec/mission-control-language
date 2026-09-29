# Phase 53.1 — forge-client extraction

> **Status: design (2026-09-29). Not build-ready** — see [Open questions](#open-questions).
> Hub: [Phase 53](phase-53-forge-client.md).

**Goal:** a new `forge-client` repo publishes `Katasec.Forge.Hands` (from `ForgeMission.ClientRuntime`)
and `Katasec.Forge.Client` (from `ForgeMission.Application`, minus Desktop session state).
forge-desktop consumes both packages. No behaviour changes.

## Locked decisions

| Area | Decision |
|---|---|
| Nature of the move | Move, not rewrite: code moves byte-identical except named namespace/reference edits (same rule as the Phase 50 extraction). |
| Packages | Two packages from one repo: `Katasec.Forge.Hands`, `Katasec.Forge.Client` (depends on Hands). |
| Stays in forge-desktop | Desktop session state (`Sessions/`), UI shell, Application Host, Transport, Supervisor, Orchestration. |
| Package access | Every new package gets GitHub Packages "Manage Actions access" for each consuming repo's workflows before its consumers' CI runs (lesson from 52.1: missing grants fail CI with 403). |

## Gates

| Gate | Result |
|---|---|
| Architecture / security | Client-side only; no server, store, or credential change. Hands keeps its sandbox and policy unchanged; Client must reach tools only through Hands. |
| Engineering philosophy | Removes a future duplicate path (CLI reimplementing the client). No new abstractions: packages are the existing code. |
| Default path | Unchanged. Proof: the 52.1 Task 8 default-path procedure (published bundle, zero arguments, `/transport/*` Project turn against the cloud, one debit) passes after the Desktop switches to the packages. |

## Done when

1. `forge-client` builds and tests pass; both packages are published.
2. forge-desktop references the packages instead of the two projects; its tests pass.
3. The 52.1 Task 8 default-path procedure passes on the published bundle.

## Open questions

1. **Exact cut line.** Which files in `ForgeMission.Application` are Desktop-only besides `Sessions/`
   (e.g. anything typed to Application Transport or `/transport` events)? Needs an inventory.
2. **Namespaces.** Keep `ForgeMission.*` namespaces for a pure move, or rename to `Katasec.Forge.*`
   now?
3. **Tests.** Which of `ForgeMission.Tests` move with the code into forge-client?
4. **Core dependency.** Which forge-mcl packages and versions the Client needs for mission authoring.
