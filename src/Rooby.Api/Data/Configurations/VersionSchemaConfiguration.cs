using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Data.Configurations;

public sealed class VersionSchemaConfiguration : IEntityTypeConfiguration<VersionSchema>
{
    public void Configure(EntityTypeBuilder<VersionSchema> builder)
    {
        builder.ToTable("version_schema");
        builder.HasKey(vs => new { vs.ProfileId, vs.VersionId, vs.SchemaId });
        builder.Property(vs => vs.Definition).HasColumnType("jsonb").IsRequired();
        builder.Property(vs => vs.Validity).HasColumnType("daterange").IsRequired();
    }
}
