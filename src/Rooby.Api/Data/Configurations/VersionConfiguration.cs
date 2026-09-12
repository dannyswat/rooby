using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rooby.Api.Data.Entities;
using Version = Rooby.Api.Data.Entities.Version;

namespace Rooby.Api.Data.Configurations;

public sealed class VersionConfiguration : IEntityTypeConfiguration<Version>
{
    public void Configure(EntityTypeBuilder<Version> builder)
    {
        builder.ToTable("version", t =>
            t.HasCheckConstraint("ck_version_ids", "version_id <> 0 AND from_version_id >= 0"));
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Description).HasMaxLength(100);

        builder.HasIndex(v => new { v.ProfileId, v.VersionId }).IsUnique();

        builder.HasOne<Profile>()
            .WithMany()
            .HasForeignKey(v => v.ProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(v => v.Created, o =>
        {
            o.Property(l => l.At).HasColumnName("created_at").IsRequired();
            o.Property(l => l.ByUserId).HasColumnName("created_by_user_id").IsRequired();
        });
        builder.OwnsOne(v => v.Published, o =>
        {
            o.Property(l => l.At).HasColumnName("published_at");
            o.Property(l => l.ByUserId).HasColumnName("published_by_user_id");
        });
        builder.Navigation(v => v.Created).IsRequired();
        builder.Navigation(v => v.Published).IsRequired(false);

        builder.Property<uint>("RowVersion").IsRowVersion();
    }
}
