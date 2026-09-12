using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Data;

/// <summary>Rejects UPDATE of item/item_line/test_case rows once VersionId > 0 (SPEC §7.2).</summary>
public sealed class PublishedRowImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Check(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            if (entry.Entity is not (Item or ItemLine or TestCase))
            {
                continue;
            }

            var versionId = (int)entry.Property("VersionId").CurrentValue!;
            if (versionId > 0)
            {
                throw new PublishedRowImmutableException(entry.Metadata.ClrType.Name, versionId);
            }
        }
    }
}
