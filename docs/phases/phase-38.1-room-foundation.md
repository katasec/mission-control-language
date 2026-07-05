# Phase 38.1 — Room Foundation

> **Status: Todo** · **Parent:** [Phase 38 — Forge Rooms](phase-38-forge-rooms.md)
> **Depends on:** existing engine only (`ForgeMission.Core`, Phase 35 Blazor host)
> **Done when:** two browser sessions (dev-stub users) chat in a shared room in real
> time, and messages persist across a reload.

The substrate. Establishes the **multi-party-first data model** (a room has members;
messages have a sender; 1:1 is a room of two) plus realtime delivery and a minimal room
view. No agents, no real auth yet — those are 38.2 and 38.4.

**Host / AOT note:** the Forge Rooms UI is **Blazor Server** (Phase 35), a normal .NET
host — *not* the AOT `forge` CLI binary. EF Core, SignalR, and reflection are fine here.
The AOT-first rules in `CLAUDE.md` apply only to code shared with the CLI/engine; any type
that flows into `ForgeMission.Core` must stay AOT-safe.

## Tasks (dependency order)

1. **Domain model.** `Room`, `Member` (`Kind: Human | Agent`), `Message` (`SenderId`,
   `Content`, `Kind`, `CreatedAt`), `RoomMembership`. Model 1:1 as a two-member room.
   Sender is on every message (attribution is a day-one invariant).
2. **Persistence.** EF Core + Postgres `DbContext`; initial migration for
   rooms/members/messages/memberships. Connection config in the Blazor host.
3. **SignalR `ChatHub`.** `JoinRoom`/`LeaveRoom` (one SignalR group per room),
   `SendMessage`. Broadcast to the room group only.
4. **Send pipeline.** On send → persist message → broadcast to the room group. Load room
   history on join.
5. **Minimal Blazor room view.** Room list, message list (with sender), message input,
   hub connection. **Dev-stub identity**: a selectable/hardcoded current user (replaced in
   38.4).
6. **Verify.** Two browser sessions in one room see each other's messages in real time;
   history survives reload; a second room is isolated from the first.

## Not in scope
Real auth (38.4), agents (38.2), trust rendering (38.3). Artifact upload/rendering is handled
in 38.2 (input) and 38.3 (output). Web only.
