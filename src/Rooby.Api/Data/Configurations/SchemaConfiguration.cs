using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Data.Configurations;

public sealed class SchemaConfiguration : IEntityTypeConfiguration<Schema>
{
    public void Configure(EntityTypeBuilder<Schema> builder)
    {
        builder.ToTable("schema");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Code).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Validity).HasColumnType("daterange").IsRequired();
        builder.Property(s => s.Definition).HasColumnType("jsonb").IsRequired();

        builder.HasOne(s => s.Project)
            .WithMany(p => p.Schemas)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // EXCLUDE USING gist (project_id WITH =, code WITH =, validity WITH &&) is added as raw SQL
        // in the initial migration; the Npgsql EF provider has no fluent API for exclusion constraints.

        builder.OwnsOne(s => s.Created, o =>
        {
            o.Property(u => u.At).HasColumnName("created_at").IsRequired();
            o.Property(u => u.ByUserId).HasColumnName("created_by_user_id").IsRequired();
        });
        builder.OwnsOne(s => s.LastModified, o =>
        {
            o.Property(u => u.At).HasColumnName("last_modified_at");
            o.Property(u => u.ByUserId).HasColumnName("last_modified_by_user_id");
        });
        builder.Navigation(s => s.Created).IsRequired();
        builder.Navigation(s => s.LastModified).IsRequired(false);

        builder.Property<uint>("RowVersion").IsRowVersion();
    }
}
