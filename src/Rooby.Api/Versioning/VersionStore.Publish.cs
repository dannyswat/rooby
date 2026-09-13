using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Versioning;

public sealed partial class VersionStore
{
    public async Task<DraftDiff> DiffDraftsAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var latestPublished = await LatestPublishedIdAsync(profileId, cancellationToken);
        var baseSnapshot = await GetSnapshotAsync(profileId, latestPublished, cancellationToken);
        var baseItemsById = baseSnapshot.Items.ToDictionary(i => i.Id);
        var baseLinesByItem = baseSnapshot.Lines.GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.ToList());

        var draftItems = await db.Items.Where(i => i.ProfileId == profileId && i.VersionId == -1).ToListAsync(cancellationToken);
        var draftLines = await db.ItemLines.Where(l => l.ProfileId == profileId && l.VersionId == -1).ToListAsync(cancellationToken);
        var draftLinesByItem = draftLines.GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.ToList());

        var itemDiffs = new List<ItemDiff>();
        foreach (var draftItem in draftItems)
        {
            baseItemsById.TryGetValue(draftItem.Id, out var baseItem);
            var change = baseItem is null ? ChangeKind.Added : draftItem.IsDeleted ? ChangeKind.Deleted : ChangeKind.Modified;

            draftLinesByItem.TryGetValue(draftItem.Id, out var draftLinesForItem);
            baseLinesByItem.TryGetValue(draftItem.Id, out var baseLinesForItem);
            var baseLinesById = (baseLinesForItem ?? []).ToDictionary(l => l.Id);

            var lineDiffs = new List<LineDiff>();
            foreach (var line in draftLinesForItem ?? [])
            {
                baseLinesById.TryGetValue(line.Id, out var baseLine);
                var lineChange = baseLine is null ? ChangeKind.Added : line.IsDeleted ? ChangeKind.Deleted : ChangeKind.Modified;
                lineDiffs.Add(new LineDiff(line.Id, lineChange, baseLine?.Content.RootElement, line.IsDeleted ? null : line.Content.RootElement));
            }

            itemDiffs.Add(new ItemDiff(
                draftItem.Id, draftItem.Key, change,
                baseItem?.Content.RootElement, draftItem.IsDeleted ? null : draftItem.Content.RootElement, lineDiffs));
        }

        var schemaUpdates = await GetPendingSchemaUpdatesAsync(profileId, latestPublished, cancellationToken);
        return new DraftDiff(itemDiffs, schemaUpdates);
    }

    public async Task<int> PublishAsync(
        Guid profileId, string description, uint expectedProfileRowVersion, int userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Lock the profile row before allocating ids or flipping VersionId (VERSION_CONTROL §5).
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM profile WHERE id = {profileId} FOR UPDATE", cancellationToken);

        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.Id == profileId, cancellationToken)
            ?? throw new KeyNotFoundException($"Profile {profileId} not found.");

        var currentProfileRowVersion = (uint)db.Entry(profile).Property("RowVersion").CurrentValue!;
        if (currentProfileRowVersion != expectedProfileRowVersion)
        {
            throw new ConcurrencyConflictException("Profile was modified since it was read.");
        }

        var capturedItemVersions = await GetDraftRowVersionsAsync(db, "item", profileId, cancellationToken);
        var capturedLineVersions = await GetDraftRowVersionsAsync(db, "item_line", profileId, cancellationToken);
        var capturedTestVersions = await GetDraftRowVersionsAsync(db, "test_case", profileId, cancellationToken);

        if (OnDraftVersionsCapturedForTesting is not null)
        {
            await OnDraftVersionsCapturedForTesting(cancellationToken);
        }

        if (capturedItemVersions.Count == 0 && capturedLineVersions.Count == 0 && capturedTestVersions.Count == 0)
        {
            throw new InvalidOperationException("Nothing to publish.");
        }

        var nextVersionId = (await db.Versions
            .Where(v => v.ProfileId == profileId && v.VersionId > 0)
            .Select(v => (int?)v.VersionId)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        // Re-read draft xmin values; abort if anything changed since we captured them above.
        if (!await DraftUnchangedAsync(db, "item", profileId, capturedItemVersions, cancellationToken) ||
            !await DraftUnchangedAsync(db, "item_line", profileId, capturedLineVersions, cancellationToken) ||
            !await DraftUnchangedAsync(db, "test_case", profileId, capturedTestVersions, cancellationToken))
        {
            throw new ConcurrencyConflictException("The draft was modified during publish.");
        }

        // The Version row must reach nextVersionId before Item/ItemLine/TestCase rows are flipped to
        // it, since those tables have a composite FK to Version(ProfileId, VersionId). EF treats
        // VersionId as part of a principal key (it's the FK target), so it can't be changed through
        // the change tracker on an already-tracked entity; flip it with raw SQL instead.
        var now = DateTimeOffset.UtcNow;
        var hasDraftVersion = await db.Versions.AnyAsync(v => v.ProfileId == profileId && v.VersionId == -1, cancellationToken);
        if (hasDraftVersion)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE version
                SET version_id = {nextVersionId}, description = {description}, published_at = {now}, published_by_user_id = {userId}
                WHERE profile_id = {profileId} AND version_id = -1
                """,
                cancellationToken);
        }
        else
        {
            db.Versions.Add(new Data.Entities.Version
            {
                ProjectId = profile.ProjectId,
                ProfileId = profileId,
                VersionId = nextVersionId,
                FromVersionId = nextVersionId - 1,
                Description = description,
                Created = new UserLog { At = now, ByUserId = userId },
                Published = new UserLog { At = now, ByUserId = userId },
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await db.Items.Where(i => i.ProfileId == profileId && i.VersionId == -1)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.VersionId, nextVersionId), cancellationToken);
        await db.ItemLines.Where(l => l.ProfileId == profileId && l.VersionId == -1)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.VersionId, nextVersionId), cancellationToken);
        await db.TestCases.Where(t => t.ProfileId == profileId && t.VersionId == -1)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.VersionId, nextVersionId), cancellationToken);

        // Postgres never bumps a row's xmin unless one of its own columns is written, so force a
        // touch here: it's what lets a second concurrent publish (reading the profile's RowVersion
        // before either started) detect that this publish already happened and fail with 409.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE profile SET id = id WHERE id = {profileId}", cancellationToken);

        // The flips above bypassed the change tracker; forget any now-stale tracked entities for
        // this profile so the schema capture (and anything the caller does afterwards) reads fresh
        // data. Only entries for this profile are detached, so unrelated tracked entities (e.g. a
        // Schema the caller is still editing) are left alone.
        DetachTrackedVersionedRows(profileId);

        await CaptureSchemasAsync(profileId, nextVersionId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return nextVersionId;
    }

    public async Task DiscardDraftAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await db.Items.Where(i => i.ProfileId == profileId && i.VersionId == -1).ExecuteDeleteAsync(cancellationToken);
        await db.ItemLines.Where(l => l.ProfileId == profileId && l.VersionId == -1).ExecuteDeleteAsync(cancellationToken);
        await db.TestCases.Where(t => t.ProfileId == profileId && t.VersionId == -1).ExecuteDeleteAsync(cancellationToken);
        await db.Versions.Where(v => v.ProfileId == profileId && v.VersionId == -1).ExecuteDeleteAsync(cancellationToken);
        DetachTrackedVersionedRows(profileId);
    }

    private void DetachTrackedVersionedRows(Guid profileId)
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity switch
        {
            Item i => i.ProfileId == profileId,
            ItemLine l => l.ProfileId == profileId,
            TestCase t => t.ProfileId == profileId,
            Data.Entities.Version v => v.ProfileId == profileId,
            _ => false,
        }).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task CaptureSchemasAsync(Guid profileId, int versionId, CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(profileId, versionId, cancellationToken);
        var schemaIds = snapshot.Items.Select(i => i.SchemaId)
            .Concat(snapshot.Lines.Select(l => l.SchemaId))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct();

        foreach (var schemaId in schemaIds)
        {
            var schema = await db.Schemas.FindAsync([schemaId], cancellationToken);
            if (schema is null)
            {
                continue; // dangling reference; caught by WP7 publish validation
            }

            if (await HasSchemaChangedSinceLastCaptureAsync(profileId, schemaId, versionId, schema, cancellationToken))
            {
                db.VersionSchemas.Add(new VersionSchema
                {
                    ProfileId = profileId,
                    VersionId = versionId,
                    SchemaId = schemaId,
                    Definition = JsonDocument.Parse(schema.Definition.RootElement.GetRawText()),
                    Validity = schema.Validity,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<SchemaUpdate>> GetPendingSchemaUpdatesAsync(Guid profileId, int latestPublished, CancellationToken cancellationToken)
    {
        var workingSet = await GetWorkingSetAsync(profileId, cancellationToken);
        var schemaIds = workingSet.Items.Select(i => i.SchemaId)
            .Concat(workingSet.Lines.Select(l => l.SchemaId))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct();

        var updates = new List<SchemaUpdate>();
        foreach (var schemaId in schemaIds)
        {
            var schema = await db.Schemas.FindAsync([schemaId], cancellationToken);
            if (schema is null)
            {
                continue;
            }

            if (await HasSchemaChangedSinceLastCaptureAsync(profileId, schemaId, latestPublished + 1, schema, cancellationToken))
            {
                updates.Add(new SchemaUpdate(schema.Id, schema.Code));
            }
        }

        return updates;
    }

    private async Task<bool> HasSchemaChangedSinceLastCaptureAsync(
        Guid profileId, Guid schemaId, int beforeVersionId, Schema schema, CancellationToken cancellationToken)
    {
        var lastCapture = await db.VersionSchemas
            .Where(vs => vs.ProfileId == profileId && vs.SchemaId == schemaId && vs.VersionId < beforeVersionId)
            .OrderByDescending(vs => vs.VersionId)
            .FirstOrDefaultAsync(cancellationToken);

        return lastCapture is null
            || !JsonEquals(lastCapture.Definition.RootElement, schema.Definition.RootElement)
            || !Equals(lastCapture.Validity, schema.Validity);
    }
}
