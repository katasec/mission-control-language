using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// Lets `dotnet ef` (migrations add / database update) run against this class
/// library without a startup project. Connection string comes from
/// ROOMS_WRITE_CONNECTION, falling back to the local dev compose defaults
/// (scripts/db/init/01-init.sql).
/// </summary>
public sealed class RoomsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<RoomsDbContext>
{
    public RoomsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ROOMS_WRITE_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=forge_rooms;Username=forge_app;Password=forge_app_dev";
        var options = new DbContextOptionsBuilder<RoomsDbContext>()
            .UseNpgsql(connection)
            .Options;
        return new RoomsDbContext(options);
    }
}
