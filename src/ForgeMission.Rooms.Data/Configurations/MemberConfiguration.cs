using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeMission.Rooms.Data.Configurations;

public sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> b)
    {
        b.ToTable("members");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id");
        b.Property(m => m.Kind)
            .HasColumnName("kind")
            .HasConversion(v => v.ToString().ToLowerInvariant(), s => Enum.Parse<MemberKind>(s, true))
            .HasMaxLength(16);
        b.Property(m => m.DisplayName).HasColumnName("display_name").HasMaxLength(256);
        b.Property(m => m.CreatedAt).HasColumnName("created_at");
    }
}
