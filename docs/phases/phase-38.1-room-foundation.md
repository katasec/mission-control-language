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

## Storage model — relational skeleton + `jsonb` payloads

Single Postgres, EF Core. **No separate document store** (a second datastore is an
opinionated dependency — avoid). The governing rule:

> **Column** it if you filter, sort, join, or enforce a constraint on it.
> **`jsonb`** it if you read it as a whole blob and its shape varies by subtype or is still
> churning.

**Relational skeleton (DB-enforced — never `jsonb`):** `users`, `rooms`, `memberships`,
`agents` (registry, added in 38.5). These carry FKs, `UNIQUE` constraints, and access
control. `memberships` is the confidentiality boundary (tenet 3) — a bug here is a data
leak, so the DB must enforce it, not app code.

**`jsonb` payloads (fluid / heterogeneous / read-as-blob):**
- `messages.payload` — content, and (for agent messages) the `StepEnvelope` trace +
  `TrustSignal` + per-step pass/fail + loop iterations + artifact *references*. Reuse the
  Core types straight into the payload; **do not** put large binary in `jsonb` (bytes → blob
  storage, a reference in the payload).
- `rooms.metadata` — name, description, avatar, settings (loose, never queried *by*).
- `agents.definition` (38.5) — the mission/chain snapshot; addressing fields
  (`handle`/`scope`/`owner_id`/`version`) stay columns.

**Payload versioning (mandatory):** every `jsonb` payload carries a discriminator/version
(`"v": 1`, `"kind": "human" | "agent"`) so future shape changes can migrate old rows
deterministically. This is the price of the flexibility — cheap if planned, painful if not.

### `jsonb` → column promotion pathway (read before "optimising")

A field starts in `jsonb` and is **promoted to a column only when a query needs to filter,
sort, join, or aggregate across rows by it** — not before. Promotion is a one-line,
zero-app-change migration via a Postgres **generated column**:

```sql
ALTER TABLE messages
  ADD COLUMN verified boolean
  GENERATED ALWAYS AS ((payload->>'verified')::boolean) STORED;
-- add an index if the new query needs one
CREATE INDEX ix_messages_verified ON messages (room_id, verified);
```

**Rules for future agents:**
1. **Do not pre-promote.** If nothing queries a field *across rows*, it stays in `jsonb`.
   Rendering a value for a row you already fetched (e.g. drawing the badge in 38.3) is **not**
   a promotion trigger.
2. **The known first candidate is `verified`** (the trust verdict). It **stays in `jsonb`
   through 38.3** (38.3 only *reads* it per-message). Promote it when the first *cross-message*
   query appears — the acquisition/quality views (38.6) or the eval harness (Phase 37) —
   using the migration above.
3. Promotion is additive and reversible; the source of truth remains the `jsonb` payload, so
   the generated column can be dropped without data loss.

## Tasks (dependency order)

1. **Domain model.** Relational: `Room`, `Member` (`Kind: Human | Agent`), `RoomMembership`
   (unique per `(room_id, member_id)`). `Message` = **columns** `Id`, `RoomId`, `SenderId`,
   `SenderKind`, `Kind`, `ReplyTo` (threading), `CreatedAt` + **`Payload` (`jsonb`)** for
   content/trace/trust/artifact-refs. `Room` gets a `Metadata` (`jsonb`) column. Model 1:1 as
   a two-member room; sender on every message (attribution is a day-one invariant). Payloads
   carry a `v`/`kind` discriminator (see Storage model).
2. **Persistence.** EF Core + Postgres `DbContext`; initial migration for
   rooms/members/messages/memberships with FKs + `UNIQUE` constraints on the skeleton and
   `jsonb` columns for payloads/metadata. Index `messages (room_id, created_at)` for the
   history query. Connection config in the Blazor host.
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
