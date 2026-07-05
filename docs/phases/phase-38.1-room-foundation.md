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

## Project layout

Two new projects (bounded context = "Rooms", distinct from the mission engine). The split is
driven by the **AOT boundary**, not aesthetics.

```
ForgeMission.Rooms         domain POCOs (Room, Member, Message, Membership, AgentHandle)
                           + domain logic. NO EF dependency. Pure + testable.
ForgeMission.Rooms.Data    RoomsDbContext, IEntityTypeConfiguration<T> (Fluent API),
                           migrations, read/write store services. Depends on
                           EF Core + Npgsql + ForgeMission.Rooms.
[Blazor host]              references both; SignalR hubs + Blazor components live here.
ForgeMission.Rooms.Tests   integration tests via Testcontainers; references .Data.
```

**AOT boundary (hard rule):** EF Core is reflection-heavy and NOT AOT-safe.
`ForgeMission.Rooms.Data` (and EF Core) must **never** be referenced by the AOT
`ForgeMission.Cli`. Persistence is referenced only by the Blazor Server host (not the AOT
binary). Domain POCOs carry **no** EF attributes — all mapping is Fluent API in `.Data`,
keeping the domain persistence-ignorant.

**No onion.** Two projects (domain + data) is the right amount of structure — do not add
Application/Infrastructure/etc. layers upfront.

### POCO vs DTO
- **POCO / entity** = persisted state, DB-shaped. Lives in `ForgeMission.Rooms`. Includes a
  typed `MessagePayload` POCO mapped to the `jsonb` column.
- **DTO / view model** = boundary-shaped (SignalR → Blazor client). **Never send EF entities
  over SignalR** (navigation cycles, proxy leaks, schema coupling). Reuse the Phase 35 view
  models (`ChatMessage`, `PipelineTraceEvent`, `TrustSignal`) as the DTO layer.
- **Mapping is manual** — explicit `entity.ToDto()` extension methods. **No AutoMapper**
  (opinionated + now commercially licensed; manual mapping is clearer and AOT-friendly).
- DTOs live in the Blazor host for now; promote to `ForgeMission.Rooms.Contracts` only when a
  second consumer (e.g. a REST API) appears — same "promote on need" discipline as jsonb→column.

## Developer environment

Local Postgres runs in a container; the Makefile stays thin and delegates to `scripts/`.

```
docker-compose.yml            postgres:16, named volume, healthcheck; mounts
                              scripts/db/init/ → /docker-entrypoint-initdb.d/
scripts/
  db/init/01-init.sql         CREATE DATABASE + least-privilege app ROLE (+ extensions if needed)
  dev-up.sh                   compose up -d → wait healthy → dotnet ef database update → seed
  dev-down.sh                 compose stop
  dev-reset.sh                compose down -v → dev-up          (clean slate)
Makefile                      one-line targets: dev-up / dev-down / dev-reset → scripts
```

**Division of labor — DB creation vs schema:**
- **Container init script** (privileged, one-time, runs on first init via
  `/docker-entrypoint-initdb.d/`) owns the **database, the least-privilege app role, and any
  extensions**. `postgres:16` has built-in `gen_random_uuid()`, so no extension is needed
  initially.
- **EF Core migrations** own everything from **tables onward** (indexes, constraints, jsonb +
  generated columns). Applied via `dotnet ef database update` in `dev-up.sh`, and via
  `Database.Migrate()` on startup **in Development only** (self-healing). **Never** auto-migrate
  in prod.
- Boundary: **script = the database exists; EF = the schema is correct.** The app role does not
  need `CREATEDB` (the container made the DB) — least privilege.

**Seeder** (Development-only, idempotent): dev-stub users, a demo room, the built-in
`@forge/hallucination-guard` agent — so 38.1's stub identity and 38.2's first agent have data.

**Tests:** integration tests use **Testcontainers (`Testcontainers.PostgreSql`)** — an ephemeral
Postgres per run, migrated then exercised. Verify-gates that touch state run against **real**
Postgres, not an in-memory fake (unit tests miss jsonb/constraint bugs).

## Data-access seam & scale-readiness

Messaging grows unbounded and is recency-skewed. To keep future scaling a *config/decorator*
change rather than surgery, establish **one seam + two disciplines now** — and build **no**
speculative infrastructure.

**Decide & prep now (cheap, hard to retrofit):**
- **Append-only, immutable messages** — `INSERT` only, never `UPDATE`. Edits/reactions are new
  rows referencing the original. (Makes archival + partitioning trivial later.)
- **Access is `(room_id, created_at)` paginated — always.** No code path loads a whole room.
- **`IReadStore` / `IWriteStore` seam** — call sites declare read vs write intent. This is the
  expensive-to-retrofit part; do it now.
- **Two connection-string slots** (`ReadConnection`, `WriteConnection`) — same DB initially; a
  read replica later is config, not code.
- **`AsNoTracking()` on all reads.**

**Defer (build only when metrics demand):** read replica; table **partitioning** by `created_at`
(revisit *before* volume — the one genuinely hard-to-retrofit item, but premature at zero users);
Redis **cache-aside** (a decorator on `IReadStore` — the seam makes it a drop-in); archival via
partition-detach; **SignalR Redis backplane** (a DI one-liner when multi-instance — no prep).

**Explicitly avoid:**
- **Write-behind cache** (cache as the write path) — a crashed/evicted cache loses messages;
  durability is non-negotiable. Pattern is **DB = source of truth; Redis = read accelerator +
  backplane.**
- Full CQRS / separate read models / eventual-consistency machinery.
- AutoMapper.

**Deferred cost to remember:** a read replica brings **replication lag / read-your-writes** — not
solved now; just don't assume reads and writes share a connection in the abstractions.

## Tasks (dependency order)

1. ✅ **Scaffold — projects + dev environment.** Create `ForgeMission.Rooms` and
   `ForgeMission.Rooms.Data` (per Project layout); `docker-compose.yml` (postgres:16);
   `scripts/db/init/01-init.sql` (DB + app role), `scripts/dev-up|down|reset.sh`; thin Makefile
   targets. **Done when** `make dev-up` brings up Postgres with the app DB + role present.
   *Verified: `make dev-up` / `dev-down` / `dev-reset` all pass; `forge_rooms` DB + `forge_app`
   role present, TCP password auth works, app role has schema CREATE but no CREATEDB/SUPERUSER.*
2. **Domain model.** In `ForgeMission.Rooms` (POCOs, no EF): `Room`, `Member`
   (`Kind: Human | Agent`), `RoomMembership` (unique `(room_id, member_id)`), `Message` =
   columns `Id`, `RoomId`, `SenderId`, `SenderKind`, `Kind`, `ReplyTo`, `CreatedAt` + `Payload`
   (`MessagePayload` POCO → jsonb); `Room.Metadata` (jsonb). **Append-only** messages; sender on
   every message; payloads carry a `v`/`kind` discriminator (see Storage model).
3. **Persistence + data-access seam.** In `ForgeMission.Rooms.Data`: `RoomsDbContext`, Fluent
   `IEntityTypeConfiguration<T>` (FKs, `UNIQUE` on skeleton, jsonb columns, index
   `messages (room_id, created_at)`), initial migration. `IReadStore`/`IWriteStore` with
   `AsNoTracking()` reads and `(room_id, created_at)` pagination; `ReadConnection`/
   `WriteConnection` config slots (same DB initially). `Database.Migrate()` on Development startup.
4. **SignalR `ChatHub`.** `JoinRoom`/`LeaveRoom` (one SignalR group per room), `SendMessage`;
   broadcast to the room group only. Hub speaks **DTOs** (`ChatMessage`-style), never entities.
5. **Send pipeline.** On send → `IWriteStore` append (insert) → broadcast DTO to the room group.
   On join → `IReadStore` paginated history (recent N).
6. **Minimal Blazor room view.** Room list, message list (sender attribution), input, hub
   connection; `entity.ToDto()` mapping. **Dev-stub identity**: selectable/hardcoded current user
   (replaced in 38.4).
7. **Verify.** Two browser sessions in one room see each other's messages live; history survives
   reload; a second room is isolated. **Testcontainers** integration test covers persist +
   paginated read + membership isolation.

## Not in scope
Real auth (38.4), agents (38.2), trust rendering (38.3). Artifact upload/rendering is handled
in 38.2 (input) and 38.3 (output). Web only.
