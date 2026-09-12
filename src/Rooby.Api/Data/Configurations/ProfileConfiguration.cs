using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Data.Configurations;

public sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("profile");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Code).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.TimeZone).HasMaxLength(64).IsRequired();
        builder.Property(p => p.PublishUri).HasMaxLength(500);
        builder.Property(p => p.PublishSecret).HasMaxLength(200);
        builder.Property(p => p.ApiKeyHash).HasMaxLength(128);
        builder.Property(p => p.Remark).HasMaxLength(4000);
        builder.Property(p => p.NextStashId).HasDefaultValue(-2);
        builder.Property(p => p.TestGate).HasConversion<string>().HasMaxLength(10);

        builder.HasIndex(p => new { p.ProjectId, p.Code }).IsUnique();

        builder.OwnsOne(p => p.Created, o =>
        {
            o.Property(u => u.At).HasColumnName("created_at").IsRequired();
            o.Property(u => u.ByUserId).HasColumnName("created_by_user_id").IsRequired();
        });
        builder.OwnsOne(p => p.LastModified, o =>
        {
            o.Property(u => u.At).HasColumnName("last_modified_at");
            o.Property(u => u.ByUserId).HasColumnName("last_modified_by_user_id");
        });
        builder.OwnsOne(p => p.ApiKeyRotated, o =>
        {
            o.Property(u => u.At).HasColumnName("api_key_rotated_at");
            o.Property(u => u.ByUserId).HasColumnName("api_key_rotated_by_user_id");
        });
        builder.Navigation(p => p.Created).IsRequired();
        builder.Navigation(p => p.LastModified).IsRequired(false);
        builder.Navigation(p => p.ApiKeyRotated).IsRequired(false);

        builder.Property<uint>("RowVersion").IsRowVersion();
    }
}
