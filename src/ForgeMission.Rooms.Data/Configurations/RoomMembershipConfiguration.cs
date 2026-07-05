using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeMission.Rooms.Data.Configurations;

public sealed class RoomMembershipConfiguration : IEntityTypeConfiguration<RoomMembership>
{
    public void Configure(EntityTypeBuilder<RoomMembership> b)
    {
        b.ToTable("memberships");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id");
        b.Property(m => m.RoomId).HasColumnName("room_id");
        b.Property(m => m.MemberId).HasColumnName("member_id");
        b.Property(m => m.Role)
            .HasColumnName("role")
            .HasConversion(v => v.ToString().ToLowerInvariant(), s => Enum.Parse<MembershipRole>(s, true))
            .HasMaxLength(16);
        b.Property(m => m.JoinedAt).HasColumnName("joined_at");

        // The confidentiality boundary — DB-enforced, never jsonb.
        b.HasIndex(m => new { m.RoomId, m.MemberId })
            .IsUnique()
            .HasDatabaseName("ux_memberships_room_id_member_id");

        b.HasOne<Room>().WithMany().HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Member>().WithMany().HasForeignKey(m => m.MemberId).OnDelete(DeleteBehavior.Cascade);
    }
}
