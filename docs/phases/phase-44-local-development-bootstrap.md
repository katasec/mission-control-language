# Phase 44 — Local development bootstrap integrity

> **Status: design ready (2026-08-17).** Make a fresh local ForgeUI checkout start without a
> hand-created database, while preserving the existing Auth & Billing database boundary.

## Outcome

`make dev-up` creates every local database ForgeUI requires. A subsequent local ForgeUI start can
bootstrap the Auth & Billing tables using its existing application role; no developer needs to run
manual PostgreSQL commands.

## Read boundary

Read this spoke first. Then read only:

1. `scripts/db/init/01-init.sql` — the local PostgreSQL initialization owner;
2. `scripts/dev-up.sh` and `docker-compose.yml` — the local lifecycle and its named data volume;
3. `src/ForgeUI/Program.cs` and `src/ForgeMission.AuthBilling/AuthBillingSchema.cs` — the existing
   application schema bootstrap and connection derivation; and
4. [Security Architecture](../design/security-architecture.md) and
   [Engineering Philosophy](../design/engineering-philosophy.md) — the required design gates.

Do not change hosted infrastructure, production connection configuration, Auth & Billing schema
ownership, Rooms migrations, or the user-owned default local data volume. They are outside this
small local-bootstrap repair.

## Locked design

### One bootstrap owner per layer

`scripts/db/init/01-init.sql`, which runs once as the local Docker Postgres superuser on an empty
volume, owns creation of both local databases and the `forge_app` connection/schema grants. Add
the existing manual recovery steps there for `authbilling_db`:

```sql
CREATE DATABASE authbilling_db OWNER postgres;
GRANT CONNECT ON DATABASE authbilling_db TO forge_app;
\connect authbilling_db
GRANT USAGE, CREATE ON SCHEMA public TO forge_app;
```

The application remains unchanged. `ForgeUI` derives its local `AuthBillingConnection` from the
Rooms server, and `AuthBillingSchema.EnsureCreatedAsync` owns only its idempotent tables. It must
not try to create databases or gain `CREATEDB`/superuser privilege.

This is deliberately direct SQL beside the existing `forge_rooms` setup, not a new bootstrap
tool, runtime configuration knob, or migration mechanism.

### Failure behaviour

On a new volume, PostgreSQL's authoritative initialization either creates both required databases
and grants or fails the container startup visibly. On an existing volume, Docker Postgres does not
re-run initialization; `make dev-down` continues to preserve data and `make dev-reset` remains the
explicit destructive reinitialization command. This task must not invoke `make dev-reset` against
the operator's existing environment.

## Design gate

| Gate | Answer |
|---|---|
| Bounded context / data ownership | No ownership changes. Rooms and Auth & Billing remain separate databases; `member_id` remains their existing application-level link, with no cross-database query or foreign key. |
| Public entry point / tier change | Not applicable. This changes only the checked-in local Docker initialization script; no route, ingress, service, or hosted topology changes. |
| Tier-3 store and credentials | Local Postgres is the sole affected development-only store. The init script runs as the Docker-created local superuser; `forge_app` keeps `NOSUPERUSER NOCREATEDB NOCREATEROLE` and receives only CONNECT plus public-schema usage/create in each database. |
| Cross-context access | No. ForgeUI's existing Auth & Billing store access is unchanged; Rooms does not query or mutate Auth & Billing data. |
| Type and reversal | Type 2, local proof-only bootstrap repair. Reversal is removal of the four SQL lines; removal condition is a future replacement of the local Compose bootstrap with checked-in local infrastructure that creates both databases equivalently. It never applies to hosted infrastructure. |
| Enforcement and proof | An isolated, disposable Postgres container mounts the real init directory on a fresh volume. SQL observations prove both databases exist, `forge_app` can connect to each and create/drop a probe table, and lacks database-creation privilege. |
| Failure ownership | Docker initialization owns database/role creation; ForgeUI owns idempotent Auth & Billing table creation. Each failure remains at its owning layer. |
| Engineering-philosophy result | The existing single init file gains the missing peer database setup. No new options, services, abstractions, or duplicated schema bootstrap are introduced. |

## Task 1 — Restore fresh-volume Auth & Billing bootstrap

### Change

Add the `authbilling_db` creation and least-privilege grants to `scripts/db/init/01-init.sql`,
immediately alongside the existing local `forge_rooms` setup. Preserve the existing role definition
and Rooms initialization exactly.

### Verification

Use a uniquely named disposable Postgres container and fresh disposable volume, mounting the real
`scripts/db/init` directory. Do not touch `forge-rooms-postgres` or `forge_rooms_pgdata`.

Record these observations:

1. `forge_rooms` and `authbilling_db` both exist;
2. `forge_app` connects to each database and can create then drop a probe table in `public`;
3. `forge_app` cannot create a database; and
4. the normal solution build and test suite remain clean.

### Done when

An empty local Postgres volume initialized from the checked-in scripts supplies both ForgeUI
databases without manual SQL, while `forge_app` remains unable to create databases or roles. The
isolated SQL observations and normal build/test output are recorded in the completed record.
