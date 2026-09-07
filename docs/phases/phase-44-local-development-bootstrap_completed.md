# Phase 44 — Local development bootstrap integrity — completed

> **Verified 2026-09-07.** Local Postgres initialization supplies both ForgeUI databases on a
> fresh volume, without granting the application role database or role creation rights.

## Task 1 — Restore fresh-volume Auth & Billing bootstrap

### Delivered

`scripts/db/init/01-init.sql` now creates `authbilling_db`, grants `forge_app` CONNECT access,
switches to its `public` schema, and grants only USAGE and CREATE there. The existing `forge_rooms`
setup and `forge_app` role (`NOSUPERUSER NOCREATEDB NOCREATEROLE`) remain unchanged. The script
header now names the two local databases and their separate table-schema owners.

`docs/phases/phase-44-local-development-bootstrap.md` also corrects the source path for
`AuthBillingSchema` to `src/ForgeMission.Billing/AuthBillingSchema.cs`.

### Design and ownership

The Docker Postgres initialization script remains the sole local superuser bootstrap owner: it
creates both development databases and assigns the least-privilege application role. Rooms EF Core
migrations continue to own `forge_rooms` tables; `AuthBillingSchema.EnsureCreatedAsync` continues
to own Auth & Billing's idempotent tables. No hosted configuration, production role, migration, or
user-owned local Compose volume changed.

This is a local Type-2 bootstrap repair: the bounded-context and datastore boundary remains two
databases joined only by existing application-level IDs. The reversal is removal of the peer
database/grant block if checked-in local infrastructure later supplies the same behavior.

### Verification

On 2026-09-07, the checked-in `scripts/db/init` directory was mounted read-only into stock
`postgres:16` in an isolated `forge-phase44-initcheck` container with a fresh
`forge_phase44_initcheck_pgdata` volume and no published host port.

- The init log ran `01-init.sql` and reached ready state without `ERROR` or `FATAL`.
- `pg_database` listed both `forge_rooms` and `authbilling_db`.
- In each database, `forge_app` successfully created and dropped a probe table in `public`.
- As `forge_app`, `CREATE DATABASE probe_44_db` and `CREATE ROLE probe_44_role` were denied;
  `probe_44_db` did not exist afterward, and `pg_roles` reported `rolsuper`, `rolcreatedb`, and
  `rolcreaterole` as false.
- Restarting the same disposable container reported that initialization was skipped and retained
  both databases, proving an existing volume is not reinitialized.
- The disposable container and volume were removed afterward. `forge-rooms-postgres` was not
  started or inspected, `mission-control-language_forge_rooms_pgdata` remained present and
  unmodified, and `make dev-reset` was not invoked.
- `dotnet build src/ForgeMission.slnx` succeeded with 0 warnings and 0 errors.
- `dotnet test src/ForgeMission.slnx` passed all five assemblies: 917 passed, 0 failed, and 11
  pre-existing live-provider/real-Docker integration tests skipped.

### Default-path acceptance

The normal local dependency is the checked-in Compose image and initialization path: `postgres:16`
with `scripts/db/init` mounted at `/docker-entrypoint-initdb.d`. The isolated container used that
same image and directory with no injected SQL, hand-prepared database, or configuration override,
but was explicitly a controlled fresh-volume bootstrap check rather than Desktop acceptance
evidence. Its observed outcome was PASS: both local ForgeUI databases initialized without manual
SQL and `forge_app` retained the required privilege boundary.
