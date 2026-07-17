using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Rooms.Data;

public static class RoomsDataServiceCollectionExtensions
{
    /// <summary>
    /// Wires the Rooms persistence layer. Two connection-string slots
    /// (ReadConnection / WriteConnection) point at the same DB initially; a read
    /// replica later is a config change, not code.
    ///
    /// <para>The ledger + platform-key stores live in <c>ForgeMission.Billing</c> over
    /// <c>authbilling_db</c> now (42.6) — wire them with <c>AddAuthBilling</c>, not here.</para>
    /// </summary>
    public static IServiceCollection AddRoomsData(
        this IServiceCollection services, string readConnection, string writeConnection)
    {
        services.AddDbContextFactory<RoomsDbContext>(o => o.UseNpgsql(writeConnection));
        services.AddDbContextFactory<ReadRoomsDbContext>(o => o
            .UseNpgsql(readConnection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddSingleton<IReadStore, ReadStore>();
        services.AddSingleton<IWriteStore, WriteStore>();
        return services;
    }
}
