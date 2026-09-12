using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rooby.Api.Data.Entities;
using Version = Rooby.Api.Data.Entities.Version;

namespace Rooby.Api.Data.Configurations;

public sealed class ItemLineConfiguration : IEntityTypeConfiguration<ItemLine>
{
    public void Configure(EntityTypeBuilder<ItemLine> builder)
    {
        builder.ToTable("item_line");
        builder.HasKey(l => new { l.Id, l.VersionId });
        builder.Property(l => l.Remarks).HasMaxLength(500);
        builder.Property(l => l.Content).HasColumnType("jsonb").IsRequired();
        builder.Property(l => l.Validity).HasColumnType("daterange");

        builder.HasIndex(l => new { l.ProfileId, l.ItemId, l.VersionId });
        builder.HasIndex(l => new { l.ProfileId, l.VersionId });

        builder.HasOne<Version>()
            .WithMany()
            .HasForeignKey(l => new { l.ProfileId, l.VersionId })
            .HasPrincipalKey(v => new { v.ProfileId, v.VersionId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(l => l.Created, o =>
        {
            o.Property(u => u.At).HasColumnName("created_at").IsRequired();
            o.Property(u => u.ByUserId).HasColumnName("created_by_user_id").IsRequired();
        });
        builder.OwnsOne(l => l.LastModified, o =>
        {
            o.Property(u => u.At).HasColumnName("last_modified_at");
            o.Property(u => u.ByUserId).HasColumnName("last_modified_by_user_id");
        });
        builder.OwnsOne(l => l.Deleted, o =>
        {
            o.Property(u => u.At).HasColumnName("deleted_at");
            o.Property(u => u.ByUserId).HasColumnName("deleted_by_user_id");
        });
        builder.Navigation(l => l.Created).IsRequired();
        builder.Navigation(l => l.LastModified).IsRequired(false);
        builder.Navigation(l => l.Deleted).IsRequired(false);

        builder.Property<uint>("RowVersion").IsRowVersion();
    }
}
