using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeMission.Rooms.Data.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.ToTable("rooms");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id");
        b.Property(r => r.CreatedAt).HasColumnName("created_at");

        // Fluid payload — jsonb, read as a blob, never queried by (see storage model).
        // Json property names are pinned lowercase so future generated-column
        // promotions (metadata->>'...') have stable keys.
        b.OwnsOne(r => r.Metadata, p =>
        {
            p.ToJson("metadata");
            p.Property(x => x.V).HasJsonPropertyName("v");
            p.Property(x => x.Name).HasJsonPropertyName("name");
            p.Property(x => x.Description).HasJsonPropertyName("description");
            p.Property(x => x.Avatar).HasJsonPropertyName("avatar");
        });
    }
}
