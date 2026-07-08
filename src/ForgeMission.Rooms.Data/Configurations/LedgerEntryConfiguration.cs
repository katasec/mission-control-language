using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeMission.Rooms.Data.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> b)
    {
        b.ToTable("ledger_entries");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id");
        b.Property(e => e.MemberId).HasColumnName("member_id");
        b.Property(e => e.AmountMicroUsd).HasColumnName("amount_micro_usd");
        b.Property(e => e.Kind)
            .HasColumnName("kind")
            .HasConversion(v => v.ToString().ToLowerInvariant(), s => Enum.Parse<LedgerEntryKind>(s, true))
            .HasMaxLength(16);
        b.Property(e => e.Description).HasColumnName("description").HasMaxLength(512);
        b.Property(e => e.MissionRef).HasColumnName("mission_ref").HasMaxLength(128);
        b.Property(e => e.Model).HasColumnName("model").HasMaxLength(128);
        b.Property(e => e.InputTokens).HasColumnName("input_tokens");
        b.Property(e => e.OutputTokens).HasColumnName("output_tokens");
        b.Property(e => e.ComputeSeconds).HasColumnName("compute_seconds");
        b.Property(e => e.CreatedAt).HasColumnName("created_at");

        // Balance is SUM(amount) per member — this index makes that (and the audit listing) cheap.
        b.HasIndex(e => e.MemberId).HasDatabaseName("ix_ledger_entries_member_id");

        // Entries belong to a member; deleting a member removes their ledger.
        b.HasOne<Member>()
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
