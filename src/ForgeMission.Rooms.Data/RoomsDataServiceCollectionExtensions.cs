using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Rooms.Data;

public static class RoomsDataServiceCollectionExtensions
{
    /// <summary>
    /// Wires the Rooms persistence layer. Two connection-string slots
    /// (ReadConnection / WriteConnection) point at the same DB initially; a read
    /// replica later is a config change, not code.
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
        services.AddSingleton<ILedgerStore, LedgerStore>();
        services.AddSingleton<IPlatformKeyStore, PlatformKeyStore>();
        return services;
    }
}
