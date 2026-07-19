using ForgeMission.Billing;
using ForgeMission.Rooms.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// Ephemeral real Postgres per test class (Testcontainers), migrated then exercised.
/// Integration tests run against real Postgres — an in-memory fake would miss jsonb
/// and constraint behaviour (phase-38.1).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var connection = _container.GetConnectionString();
        var services = new ServiceCollection();
        services.AddLogging(); // BillingService (and anything else DI-activated) takes an ILogger<T>
        services.AddRoomsData(connection, connection);
        // Billing (ledger + platform keys) lives in its own authbilling_db in prod; here it shares the
        // one container DB — the table names are distinct, so the raw-Npgsql stores coexist with the EF
        // rooms tables and exercise the real cloud code path.
        services.AddAuthBilling(connection);
        Services = services.BuildServiceProvider();

        var factory = Services.GetRequiredService<IDbContextFactory<RoomsDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
            await db.Database.MigrateAsync();

        // Bootstrap the billing schema the EF drop-migration just removed — now owned by raw SQL.
        await AuthBillingSchema.EnsureCreatedAsync(Services.GetRequiredService<NpgsqlDataSource>());
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        await _container.DisposeAsync();
    }
}
