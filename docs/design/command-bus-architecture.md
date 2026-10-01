# Command-bus architecture

> **Status: governing target architecture (2026-09-29).** Every phase builds toward this. A task
> may leave an existing path unmigrated, but it must not add a new path that points away from it.
> Security rules for queues are in [Security Architecture](security-architecture.md#service-bus-queue-classes).

## The rule

**Service Bus carries every command. No service sends a command to another service directly.**
Clients reach an edge over HTTP; everything behind the edge is decoupled through queues.

| Traffic | Mechanism |
|---|---|
| Client → edge | HTTP, authenticated (platform key or CIAM session). |
| Command (changes state) | Edge or service publishes to the owner's queue. The owner validates and applies it. |
| Synchronous command result | Service Bus request/reply: the sender awaits the owner's reply on a reply queue (session per request). Existing response DTOs are returned unchanged. |
| Query (reads state) | Direct request/response to the owning service's read endpoint — the CQRS read side. A read has no side effect worth decoupling, and a waiting user needs an answer. Includes run history, event streams, balances, platform-key resolution. |
| Settlement | Financial queue from forge-runner to Billing. |

## Components and tiers

| Tier | Component | Owns | Touches |
|---|---|---|---|
| 1 — edge | ForgeAPI | Nothing | Auth; ingress send; reply listen; queries to tier-2 owners |
| 1 — edge | ForgeUI | Nothing (target; Rooms extraction is later) | Same pattern |
| 2 | Conversation Host | Conversations: Azure Table + Blob | Ingress listen, reply send, internal work queues |
| 2 | forge-runner | Nothing durable | Internal work queues; financial send |
| 2 | Billing service | `authbilling_db` (keys, ledger, balances) | Financial listen; answers queries |
| 3 | Stores and private queues | — | Only their tier-2 owners |

forge-runner is **one container image**: the only place missions execute, for every caller,
one-shot or durable. It measures usage for every run segment and sends settlement.

## Current deviations (known, being removed)

| Deviation | Removed by |
|---|---|
| ForgeUI holds `authbilling_db` for two sign-in writes | Backlog: move `forge login` key issuance to ForgeAPI |
| ForgeUI holds `rooms_db` (Rooms context inside the edge) | Backlog: extract Rooms as a tier-2 service |
| Rooms and `forge exec` call forge-runner over HTTP `/run` | Backlog: move them to the bus; delete `/run` |
