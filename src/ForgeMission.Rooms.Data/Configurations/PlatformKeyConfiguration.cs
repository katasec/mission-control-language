using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeMission.Rooms.Data.Configurations;

public sealed class PlatformKeyConfiguration : IEntityTypeConfiguration<PlatformKey>
{
    public void Configure(EntityTypeBuilder<PlatformKey> b)
    {
        b.ToTable("platform_keys");
        b.HasKey(k => k.KeyId);
        b.Property(k => k.KeyId).HasColumnName("key_id").HasMaxLength(64);
        b.Property(k => k.SecretHash).HasColumnName("secret_hash").HasMaxLength(128);
        b.Property(k => k.MemberId).HasColumnName("member_id");
        b.Property(k => k.CreatedAt).HasColumnName("created_at");
        b.Property(k => k.RevokedAt).HasColumnName("revoked_at");

        // Lookup by member (list/revoke a user's keys) — KeyId is already the PK, so point
        // resolution on the request path needs no extra index.
        b.HasIndex(k => k.MemberId).HasDatabaseName("ix_platform_keys_member_id");

        // Keys belong to a member; deleting a member removes their keys.
        b.HasOne<Member>()
            .WithMany()
            .HasForeignKey(k => k.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
