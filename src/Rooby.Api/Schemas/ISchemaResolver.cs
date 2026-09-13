using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Schemas;

/// <summary>Resolves the Schema.Code definition current on a given date (SPEC §3.8); consumed by WP4/WP7.</summary>
public interface ISchemaResolver
{
    Task<Schema?> CurrentAsync(Guid projectId, string code, DateOnly dateInProfileZone, CancellationToken cancellationToken = default);
}

public sealed class SchemaResolver(RoobyDbContext db) : ISchemaResolver
{
    public Task<Schema?> CurrentAsync(Guid projectId, string code, DateOnly dateInProfileZone, CancellationToken cancellationToken = default) =>
        db.Schemas
            .Where(s => s.ProjectId == projectId && s.Code == code && s.Validity.Contains(dateInProfileZone))
            .SingleOrDefaultAsync(cancellationToken);
}
