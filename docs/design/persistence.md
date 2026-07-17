# Persistence — storage abstraction & the Table Storage seam

> **Status: decided 2026-07-17 (Phase 42.5 T4).** Records the repository seam the Rooms data
> layer already follows and the deferred move to Azure Table Storage, so it stops being an
> unwritten plan in someone's head.

## Decision

**Every persisted aggregate is fronted by an `I*Store` interface; callers depend on the
interface, never on `RoomsDbContext` or a provider type.** The current implementations are
EF Core / Postgres (`LedgerStore`, `ReadStore`, `WriteStore`, and now `PlatformKeyStore`),
registered in [`RoomsDataServiceCollectionExtensions`](../../src/ForgeMission.Rooms.Data/RoomsDataServiceCollectionExtensions.cs).
Swapping a store's backend is a **one-line DI change**, not a caller change.

**Azure Table Storage is deferred, not adopted.** Ship EF/Postgres now — the data project
already has the migration tooling, DI wiring, and `IDbContextFactory` pattern, so EF is zero
marginal cost. When the cheap-storage move happens, it slots in behind the existing interfaces.

## Why platform_keys is the first Table Storage candidate

When the move starts, [`platform_keys`](../../src/ForgeMission.Rooms.Data/) migrates first:

- **Pure key→value lookup by `key_id`** — a natural `PartitionKey`/`RowKey`, no joins, no
  aggregation. Table Storage's exact sweet spot.
- **Read-heavy, on the request path** (③ lookup lib, every hosted call) — Table Storage's
  cheap point-reads and independent scaling matter most here.
- **Independently movable.** The request path composes two stores: `IPlatformKeyStore`
  (key → user) and `ILedgerStore` (user → balance). Only the key half is a good fit — the
  ledger does `SUM(amount)` aggregation, which is clumsy in Table Storage and stays in
  Postgres. The interface split lets the key half move on its own.

Contrast: the ledger, rooms, memberships, and messages have relational shape (foreign keys,
aggregation, ordering) and stay in Postgres.

## AOT note

`RoomsDbContext` is host-only (Blazor Server `ForgeUI`, and `ForgeMission.Runner` which sets
`PublishAot=false`). It is **never** referenced by the AOT `ForgeMission.Cli` binary — the
CLAUDE.md AOT rules are scoped to that CLI. So EF Core is safe in both consumers of the store
layer; the AOT boundary is not a reason to avoid it here.

## Shape to follow when adding a store

1. Entity POCO (`Foo.cs`) + `IEntityTypeConfiguration<Foo>` (`Configurations/FooConfiguration.cs`,
   snake_case columns/table).
2. `DbSet<Foo>` on `RoomsDbContext` + `ApplyConfiguration` in `OnModelCreating`.
3. `IFooStore` interface + `FooStore(IDbContextFactory<RoomsDbContext>)` implementation.
4. Register in `AddRoomsData`.
5. `dotnet ef migrations add AddFoo` against `RoomsDbContext`.
