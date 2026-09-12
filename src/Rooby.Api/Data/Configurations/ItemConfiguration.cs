using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rooby.Api.Data.Entities;
using Version = Rooby.Api.Data.Entities.Version;

namespace Rooby.Api.Data.Configurations;

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("item");
        builder.HasKey(i => new { i.Id, i.VersionId });
        builder.Property(i => i.Key).HasMaxLength(30).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(100);
        builder.Property(i => i.Content).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(i => new { i.ProfileId, i.VersionId });
        builder.HasIndex(i => new { i.ProfileId, i.Key, i.VersionId });
        builder.HasIndex(i => new { i.ProfileId, i.Key })
            .IsUnique()
            .HasFilter("version_id = -1")
            .HasDatabaseName("ix_item_profile_id_key_draft");

        builder.HasOne<Version>()
            .WithMany()
            .HasForeignKey(i => new { i.ProfileId, i.VersionId })
            .HasPrincipalKey(v => new { v.ProfileId, v.VersionId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(i => i.Created, o =>
        {
            o.Property(l => l.At).HasColumnName("created_at").IsRequired();
            o.Property(l => l.ByUserId).HasColumnName("created_by_user_id").IsRequired();
        });
        builder.OwnsOne(i => i.LastModified, o =>
        {
            o.Property(l => l.At).HasColumnName("last_modified_at");
            o.Property(l => l.ByUserId).HasColumnName("last_modified_by_user_id");
        });
        builder.OwnsOne(i => i.Deleted, o =>
        {
            o.Property(l => l.At).HasColumnName("deleted_at");
            o.Property(l => l.ByUserId).HasColumnName("deleted_by_user_id");
        });
        builder.Navigation(i => i.Created).IsRequired();
        builder.Navigation(i => i.LastModified).IsRequired(false);
        builder.Navigation(i => i.Deleted).IsRequired(false);

        builder.Property<uint>("RowVersion").IsRowVersion();
    }
}
