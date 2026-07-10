using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        return services;
    }

    /// <summary>
    /// Wires the room-artifact store (Phase 38.9). Dev uses a local volume rooted at
    /// <paramref name="localRoot"/>; prod swaps in an Azure Blob implementation behind the same
    /// <see cref="IArtifactStore"/> seam (D2) — a DI change, not a call-site change.
    /// </summary>
    public static IServiceCollection AddArtifactStore(this IServiceCollection services, string localRoot)
    {
        services.AddSingleton<IArtifactStore>(sp => new LocalVolumeArtifactStore(
            localRoot,
            sp.GetRequiredService<IReadStore>(),
            sp.GetRequiredService<ILogger<LocalVolumeArtifactStore>>()));
        return services;
    }
}
