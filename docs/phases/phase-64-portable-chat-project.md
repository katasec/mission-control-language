# Phase 64 — Portable chat project

Status: selected next, 2026-10-04. Future-state direction agreed; not build-ready.

## Goal

Check a small project declaration and mission lock into Git, clone anywhere, and reconnect to
the same hosted chats under the signed-in account. Local generated files must be reconstructable;
opening chat must not depend on a local mission-authoring ledger.

## Work

| Spoke | Status / next action |
|---|---|
| [Portable files and chat reconnection](phase-64.1-portable-chat-contracts.md) | Discover and validate the exact file, resolution, ordering and restore contracts before implementation. |

## Scope

The [spoke](phase-64.1-portable-chat-contracts.md#agreed-future-state) records the agreed future
state and its validation gaps. Phase 63 remains the verified current implementation; Phase 64
replaces its required durable private-state arrangement when implemented and accepted.

## Done when

A fresh clone containing the declaration, lock and required source reconnects to its hosted
history without `obj/forge/project.state.json`, local evaluation results or remembered tool
consent. Fresh tool approval, message-order-based selection, explicit failures and the normal
installed `forge` path are verified against the locked contracts in the spoke.
