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
        b.Property(m => m.Subject).HasColumnName("subject").HasMaxLength(256);
        b.Property(m => m.Issuer).HasColumnName("issuer").HasMaxLength(512);
        b.Property(m => m.Email).HasColumnName("email").HasMaxLength(320);
        b.Property(m => m.CreatedAt).HasColumnName("created_at");

        // Federated identity key — one member per (issuer, subject). Filtered so the many
        // agent/legacy rows with NULL identity don't collide on the unique constraint.
        b.HasIndex(m => new { m.Issuer, m.Subject })
            .IsUnique()
            .HasDatabaseName("ux_members_issuer_subject")
            .HasFilter("subject IS NOT NULL");
    }
}
