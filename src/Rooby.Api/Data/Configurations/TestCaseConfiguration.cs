using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rooby.Api.Data.Entities;
using Version = Rooby.Api.Data.Entities.Version;

namespace Rooby.Api.Data.Configurations;

public sealed class TestCaseConfiguration : IEntityTypeConfiguration<TestCase>
{
    public void Configure(EntityTypeBuilder<TestCase> builder)
    {
        builder.ToTable("test_case");
        builder.HasKey(t => new { t.Id, t.VersionId });
        builder.Property(t => t.ItemKey).HasMaxLength(30).IsRequired();
        builder.Property(t => t.Remarks).HasMaxLength(4000);
        builder.Property(t => t.InputData).HasColumnType("jsonb").IsRequired();
        builder.Property(t => t.OutputValue).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(t => new { t.ProfileId, t.VersionId });
        builder.HasIndex(t => new { t.ProfileId, t.ItemKey });

        builder.HasOne<Version>()
            .WithMany()
            .HasForeignKey(t => new { t.ProfileId, t.VersionId })
            .HasPrincipalKey(v => new { v.ProfileId, v.VersionId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(t => t.Created, o =>
        {
            o.Property(u => u.At).HasColumnName("created_at").IsRequired();
            o.Property(u => u.ByUserId).HasColumnName("created_by_user_id").IsRequired();
        });
        builder.OwnsOne(t => t.LastModified, o =>
        {
            o.Property(u => u.At).HasColumnName("last_modified_at");
            o.Property(u => u.ByUserId).HasColumnName("last_modified_by_user_id");
        });
        builder.OwnsOne(t => t.Deleted, o =>
        {
            o.Property(u => u.At).HasColumnName("deleted_at");
            o.Property(u => u.ByUserId).HasColumnName("deleted_by_user_id");
        });
        builder.Navigation(t => t.Created).IsRequired();
        builder.Navigation(t => t.LastModified).IsRequired(false);
        builder.Navigation(t => t.Deleted).IsRequired(false);

        builder.Property<uint>("RowVersion").IsRowVersion();
    }
}
