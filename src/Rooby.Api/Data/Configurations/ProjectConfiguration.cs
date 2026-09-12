using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Data.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("project");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Code).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Remark).HasMaxLength(4000);
        builder.HasIndex(p => p.Code).IsUnique();

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
        builder.Navigation(p => p.Created).IsRequired();
        builder.Navigation(p => p.LastModified).IsRequired(false);

        builder.HasMany(p => p.Profiles)
            .WithOne(pr => pr.Project)
            .HasForeignKey(pr => pr.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Schemas)
            .WithOne()
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
