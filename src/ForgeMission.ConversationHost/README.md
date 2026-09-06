---
type: software-component
title: Conversation Host
description: The durable conversation service that orders facts, owns conversation state, and projects typed HTTP/SSE access.
resource: src/ForgeMission.ConversationHost
tags: [host, conversations, orleans, durable]
---

# Conversation Host

## Purpose

Own the durable coordination boundary for conversation commands, ordered facts, replay, and run projections.

## Why this exists

One service must serialize conversation mutation and retain canonical state so reconnects and at-least-once delivery cannot create a competing history.

## Owns

- Orleans grains, sequence allocation, command/progress acceptance, Table/Blob persistence, and Project Mission indexes.
- HTTP/SSE projections and Service Bus dispatch/consumption for the Conversation bounded context.

## Does not own

- Mission reasoning or provider execution (the Worker owns those).
- Platform identity/billing, Rooms data, Desktop/Application state, or Bob’s local capability authorization and execution.

## Change admission

A change belongs here only if it advances this component’s reason for existence.

For mission execution, caller identity, billing, or local tools, compose with the Worker, platform edge, or Client Runtime owner instead.

## Use these pieces

- [Service composition and health entry point](Program.cs)
- [Transport-neutral handlers and HTTP/SSE route projection](Api/ConversationApiEndpoints.cs)
- [Canonical conversation owner](Grains/ConversationGrain.cs) and [run state machine](Grains/MissionRunGrain.cs)
- Boundary coverage: [API routes](../ForgeMission.ConversationHost.Tests/ConversationApiTests.cs), [grain recovery](../ForgeMission.ConversationHost.Tests/ConversationGrainTests.cs), and [persistence](../ForgeMission.ConversationHost.Tests/ConversationPersistenceTests.cs)

## Communicates with

```mermaid
flowchart LR
  Edge[Application / Tier-1 adapter] -->|typed HTTP + SSE| Host
  Host -->|ordered events| Store[Conversation Table / Blob]
  Host -->|mission-command queue| Worker[Conversation Worker]
  Worker -->|conversation-progress queue| Host
```

## Important flows and constraints

- The Host is the sole Conversation-store writer and grain caller; other bounded contexts do not query its stores directly.
- Queue delivery is at least once; stable IDs and grain acceptance make recovery idempotent.
- Current routes are an adapter projection, not the semantic definition of the shared messages. Tier-1 callers must supply identity through their own boundary; the current local API has a development tenant seam.

## Related documentation

- [Durable conversations](../../docs/design/durable-conversations.md)
- [Security architecture](../../docs/design/security-architecture.md)
- [Phase 43.23 completed ownership evidence](../../docs/phases/phase-43.23-domain-ownership_completed.md)
