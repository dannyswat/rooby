using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.LoginName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(250).IsRequired();

        // Case-insensitive uniqueness is enforced by a raw-SQL expression index added in the migration
        // (UNIQUE (lower(login_name))); EF's fluent HasIndex only supports plain-column indexes.

        builder.OwnsOne(u => u.Created, o =>
        {
            o.Property(l => l.At).HasColumnName("created_at").IsRequired();
            o.Property(l => l.ByUserId).HasColumnName("created_by_user_id").IsRequired();
        });
        builder.OwnsOne(u => u.LastModified, o =>
        {
            o.Property(l => l.At).HasColumnName("last_modified_at");
            o.Property(l => l.ByUserId).HasColumnName("last_modified_by_user_id");
        });
        builder.Navigation(u => u.Created).IsRequired();
        builder.Navigation(u => u.LastModified).IsRequired(false);
    }
}
