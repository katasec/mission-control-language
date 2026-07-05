using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeMission.Rooms.Data.Configurations;

public sealed class RoomInviteConfiguration : IEntityTypeConfiguration<RoomInvite>
{
    public void Configure(EntityTypeBuilder<RoomInvite> b)
    {
        b.ToTable("room_invites");
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).HasColumnName("id");
        b.Property(i => i.RoomId).HasColumnName("room_id");
        b.Property(i => i.Token).HasColumnName("token").HasMaxLength(128);
        b.Property(i => i.Role)
            .HasColumnName("role")
            .HasConversion(v => v.ToString().ToLowerInvariant(), s => Enum.Parse<MembershipRole>(s, true))
            .HasMaxLength(16);
        b.Property(i => i.CreatedBy).HasColumnName("created_by");
        b.Property(i => i.ExpiresAt).HasColumnName("expires_at");
        b.Property(i => i.CreatedAt).HasColumnName("created_at");

        // The token is the lookup key on accept — unique + indexed.
        b.HasIndex(i => i.Token).IsUnique().HasDatabaseName("ux_room_invites_token");

        b.HasOne<Room>().WithMany().HasForeignKey(i => i.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Member>().WithMany().HasForeignKey(i => i.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
