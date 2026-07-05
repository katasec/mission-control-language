using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeMission.Rooms.Data.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.ToTable("messages");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id");
        b.Property(m => m.RoomId).HasColumnName("room_id");
        b.Property(m => m.SenderId).HasColumnName("sender_id");
        b.Property(m => m.SenderKind)
            .HasColumnName("sender_kind")
            .HasConversion(v => v.ToString().ToLowerInvariant(), s => Enum.Parse<MemberKind>(s, true))
            .HasMaxLength(16);
        b.Property(m => m.Kind)
            .HasColumnName("kind")
            .HasConversion(v => v.ToString().ToLowerInvariant(), s => Enum.Parse<MessageKind>(s, true))
            .HasMaxLength(16);
        b.Property(m => m.ReplyTo).HasColumnName("reply_to");
        b.Property(m => m.CreatedAt).HasColumnName("created_at");

        // Access is always (room_id, created_at) paginated — this is the index it rides.
        b.HasIndex(m => new { m.RoomId, m.CreatedAt })
            .HasDatabaseName("ix_messages_room_id_created_at");

        b.HasOne<Room>().WithMany().HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Member>().WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Message>().WithMany().HasForeignKey(m => m.ReplyTo).OnDelete(DeleteBehavior.Restrict);

        // Fluid payload — jsonb. Lowercase keys pinned so the documented promotion
        // pathway (payload->>'verified' generated column) has stable keys.
        b.OwnsOne(m => m.Payload, p =>
        {
            p.ToJson("payload");
            p.Property(x => x.V).HasJsonPropertyName("v");
            p.Property(x => x.Kind).HasJsonPropertyName("kind");
            p.Property(x => x.Text).HasJsonPropertyName("text");
        });
    }
}
