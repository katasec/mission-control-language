using ForgeMission.Billing;
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

    /// <summary>
    /// Wires the request-path platform-key resolver (42.5 ③) — used by the runner to resolve a
    /// presented <c>fg_live_…</c> token to its member + balance. Needs <see cref="AddRoomsData"/>
    /// (for the key + ledger stores). The HMAC key must match the issuer's <c>PlatformKeys:HmacKey</c>.
    /// </summary>
    public static IServiceCollection AddPlatformKeyResolver(
        this IServiceCollection services, PlatformKeyResolverOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IPlatformKeyResolver, PlatformKeyResolver>();
        return services;
    }
}
