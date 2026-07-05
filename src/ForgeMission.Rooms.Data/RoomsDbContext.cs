using ForgeMission.Rooms.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ForgeMission.Rooms.Data;

/// <summary>
/// The write-side context; migrations live against this type.
/// Never referenced by the AOT ForgeMission.Cli — host-only (see phase-38.1 AOT boundary).
/// </summary>
public class RoomsDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomMembership> Memberships => Set<RoomMembership>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<RoomInvite> Invites => Set<RoomInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new MemberConfiguration());
        modelBuilder.ApplyConfiguration(new RoomConfiguration());
        modelBuilder.ApplyConfiguration(new RoomMembershipConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new RoomInviteConfiguration());
    }
}

/// <summary>
/// The read-side context. Same model, separate connection slot so a read replica
/// later is config, not code. Registered with NoTracking as the default.
/// </summary>
public sealed class ReadRoomsDbContext(DbContextOptions<ReadRoomsDbContext> options)
    : RoomsDbContext(options);
