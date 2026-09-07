---
type: software-component
title: Client Runtime (Bob)
description: Scoped local capability execution with policy, confirmation, audit, cancellation, and cleanup.
resource: src/ForgeMission.ClientRuntime
tags: [capabilities, local-execution, policy]
---

# Client Runtime (Bob)

## Purpose

Provides one scoped local capability session for an Application session.

## Why this exists

Capability execution needs a narrow, disposable authority boundary. Keeping policy and execution here prevents a Project, conversation, or UI concern from becoming a local-tool authority.

## Owns

- Creation, admission, cancellation, drain, and disposal of [`ClientExecutionSession`](ClientExecutionSession.cs).
- The local workspace file and terminal capability registry, policy enforcement, confirmation bridge, and in-memory audit used by that session.
- The closed mission profiles. `ProjectWorkspaceAndTerminal` runs only through the macOS deny-default sandbox boundary; unsupported platforms fail closed rather than treating a working directory as containment.

## Does not own

- Project manifests, mission selection, conversation state, HTTP/SSE, rendering, platform credentials, or model/provider reasoning.

## Change admission

A change belongs here only if it advances the scoped local-capability boundary. For Project/session attachment compose with `ForgeMission.Application`; for wire actions compose with `ForgeMission.Application.Transport`.

## Use these pieces

- [`ClientExecutionSession.Create`](ClientExecutionSession.cs) creates the public capability boundary.
- [`ICapabilityDispatcher`](../ForgeMission.Core/Tools/ICapabilityDispatcher.cs) is the dispatch contract it implements.
- [`ClientExecutionSessionTests`](../ForgeMission.Tests/ClientRuntime/ClientExecutionSessionTests.cs) prove closed admission and disposal drain.

## Communicates with

```mermaid
flowchart LR
  App[Application session] -->|in-process ICapabilityDispatcher| Bob[ClientExecutionSession]
  Bob -->|policy / confirmation / audit| Core[Core capability providers]
  Core -->|scoped workspace operations| Disk[Local Project workspace]
```

## Important flows and constraints

- An admitted dispatch is joined during disposal; a new dispatch after close returns an error.
- Tool declarations are compatibility declarations, not a reasoning loop or provider access.
- This is an in-process library boundary, not a separate process or HTTP service.

## Related documentation

- [Phase 43.23 contracts](../../docs/retrospectives/phase-43-domain-ownership/contracts.md#local-execution-boundary)
- [Ownership end state](../../docs/retrospectives/phase-43-domain-ownership/end-state.md)
