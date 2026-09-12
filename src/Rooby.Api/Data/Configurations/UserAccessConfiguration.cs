using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Data.Configurations;

public sealed class UserAccessConfiguration : IEntityTypeConfiguration<UserAccess>
{
    public void Configure(EntityTypeBuilder<UserAccess> builder)
    {
        builder.ToTable("user_access", t =>
            t.HasCheckConstraint("ck_user_access_scope", "project_id IS NOT NULL OR profile_id IS NULL"));
        builder.HasKey(a => a.Id);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(a => a.Created, o =>
        {
            o.Property(l => l.At).HasColumnName("created_at").IsRequired();
            o.Property(l => l.ByUserId).HasColumnName("created_by_user_id").IsRequired();
        });
        builder.OwnsOne(a => a.LastModified, o =>
        {
            o.Property(l => l.At).HasColumnName("last_modified_at");
            o.Property(l => l.ByUserId).HasColumnName("last_modified_by_user_id");
        });
        builder.OwnsOne(a => a.Revoked, o =>
        {
            o.Property(l => l.At).HasColumnName("revoked_at");
            o.Property(l => l.ByUserId).HasColumnName("revoked_by_user_id");
        });
        builder.Navigation(a => a.Created).IsRequired();
        builder.Navigation(a => a.LastModified).IsRequired(false);
        builder.Navigation(a => a.Revoked).IsRequired(false);
    }
}
