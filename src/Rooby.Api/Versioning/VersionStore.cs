using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;
using Version = Rooby.Api.Data.Entities.Version;

namespace Rooby.Api.Versioning;

public sealed partial class VersionStore(RoobyDbContext db) : IVersionStore
{
    /// <summary>Test-only seam: awaited right after draft RowVersions are captured during publish and
    /// before the re-read comparison, so tests can inject a deterministic concurrent edit.</summary>
    internal Func<CancellationToken, Task>? OnDraftVersionsCapturedForTesting { get; set; }


    public async Task<int> LatestPublishedIdAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        await db.Versions
            .Where(v => v.ProfileId == profileId && v.VersionId > 0)
            .Select(v => (int?)v.VersionId)
            .MaxAsync(cancellationToken) ?? 0;

    public async Task EnsureDraftAsync(Guid profileId, int userId, CancellationToken cancellationToken = default)
    {
        if (await db.Versions.AnyAsync(v => v.ProfileId == profileId && v.VersionId == -1, cancellationToken))
        {
            return;
        }

        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.Id == profileId, cancellationToken)
            ?? throw new KeyNotFoundException($"Profile {profileId} not found.");
        var latest = await LatestPublishedIdAsync(profileId, cancellationToken);

        db.Versions.Add(new Version
        {
            ProjectId = profile.ProjectId,
            ProfileId = profileId,
            VersionId = -1,
            FromVersionId = latest,
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId },
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<VersionSnapshot> GetSnapshotAsync(Guid profileId, int versionId, CancellationToken cancellationToken = default)
    {
        var items = await SnapshotRowsAsync(db.Items, "item", profileId, versionId, cancellationToken);
        var lines = await SnapshotRowsAsync(db.ItemLines, "item_line", profileId, versionId, cancellationToken);
        var testCases = await SnapshotRowsAsync(db.TestCases, "test_case", profileId, versionId, cancellationToken);

        var visibleItemIds = items.Select(i => i.Id).ToHashSet();
        lines = [.. lines.Where(l => visibleItemIds.Contains(l.ItemId))];

        return new VersionSnapshot(items, lines, testCases);
    }

    public async Task<VersionSnapshot> GetWorkingSetAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var latest = await LatestPublishedIdAsync(profileId, cancellationToken);
        var baseSnapshot = await GetSnapshotAsync(profileId, latest, cancellationToken);

        var draftItems = await db.Items.Where(i => i.ProfileId == profileId && i.VersionId == -1).ToListAsync(cancellationToken);
        var draftLines = await db.ItemLines.Where(l => l.ProfileId == profileId && l.VersionId == -1).ToListAsync(cancellationToken);
        var draftTests = await db.TestCases.Where(t => t.ProfileId == profileId && t.VersionId == -1).ToListAsync(cancellationToken);

        return OverlayOnto(baseSnapshot, draftItems, draftLines, draftTests);
    }

    public async Task<VersionSnapshot> GetStashPreviewAsync(Guid profileId, int stashId, CancellationToken cancellationToken = default)
    {
        var stashVersion = await db.Versions.SingleOrDefaultAsync(v => v.ProfileId == profileId && v.VersionId == stashId, cancellationToken)
            ?? throw new KeyNotFoundException($"Stash {stashId} not found.");

        var baseSnapshot = await GetSnapshotAsync(profileId, stashVersion.FromVersionId, cancellationToken);

        var stashItems = await db.Items.Where(i => i.ProfileId == profileId && i.VersionId == stashId).ToListAsync(cancellationToken);
        var stashLines = await db.ItemLines.Where(l => l.ProfileId == profileId && l.VersionId == stashId).ToListAsync(cancellationToken);
        var stashTests = await db.TestCases.Where(t => t.ProfileId == profileId && t.VersionId == stashId).ToListAsync(cancellationToken);

        return OverlayOnto(baseSnapshot, stashItems, stashLines, stashTests);
    }

    private static VersionSnapshot OverlayOnto(
        VersionSnapshot baseSnapshot, List<Item> overlayItems, List<ItemLine> overlayLines, List<TestCase> overlayTests)
    {
        var items = Overlay(baseSnapshot.Items, overlayItems);
        var visibleItemIds = items.Select(i => i.Id).ToHashSet();
        var lines = Overlay(baseSnapshot.Lines, overlayLines).Where(l => visibleItemIds.Contains(l.ItemId)).ToList();
        var tests = Overlay(baseSnapshot.TestCases, overlayTests);

        return new VersionSnapshot(items, lines, tests);
    }

    private static List<T> Overlay<T>(IReadOnlyList<T> baseline, IReadOnlyList<T> overlay)
        where T : class, IVersionedRow
    {
        var byId = baseline.ToDictionary(r => r.Id);
        foreach (var row in overlay)
        {
            if (row.IsDeleted)
            {
                byId.Remove(row.Id);
            }
            else
            {
                byId[row.Id] = row;
            }
        }

        return [.. byId.Values];
    }

    private static async Task<List<T>> SnapshotRowsAsync<T>(
        DbSet<T> set, string tableName, Guid profileId, int maxVersionId, CancellationToken cancellationToken)
        where T : class
    {
        if (maxVersionId <= 0)
        {
            return [];
        }

        // DISTINCT ON must run before the tombstone check, else it could pick a stale non-deleted
        // revision instead of correctly hiding an item whose true latest row is a tombstone.
        // Postgres SELECT * excludes system columns, so xmin must be listed explicitly for EF's
        // RowVersion shadow property to be populated on the tracked result.
        var sql = "SELECT * FROM (" +
            $"SELECT DISTINCT ON (id) *, xmin FROM {tableName} " +
            "WHERE profile_id = {0} AND version_id > 0 AND version_id <= {1} " +
            "ORDER BY id, version_id DESC" +
            ") t WHERE NOT is_deleted";
        return await set.FromSqlRaw(sql, profileId, maxVersionId).ToListAsync(cancellationToken);
    }

    /// <summary>Reads current xmin values without going through entity tracking: a tracked entity
    /// with the same PK already in the change tracker would otherwise mask fresh data via identity
    /// resolution, which is exactly the staleness this method exists to detect.</summary>
    private static async Task<Dictionary<Guid, uint>> GetDraftRowVersionsAsync(
        RoobyDbContext context, string tableName, Guid profileId, CancellationToken cancellationToken)
    {
        // Column aliases are snake_case: the DbContext's naming convention plugin expects
        // SqlQueryRaw<T> result columns to match its own naming convention, not the C# property names.
        var sql = $"SELECT id, xmin::text::bigint AS xmin_value FROM {tableName} WHERE profile_id = {{0}} AND version_id = -1";
        var rows = await context.Database.SqlQueryRaw<DraftRowVersion>(sql, profileId).ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Id, r => (uint)r.XminValue);
    }

    private sealed record DraftRowVersion(Guid Id, long XminValue);

    private static async Task<bool> DraftUnchangedAsync(
        RoobyDbContext context, string tableName, Guid profileId, Dictionary<Guid, uint> captured, CancellationToken cancellationToken)
    {
        var current = await GetDraftRowVersionsAsync(context, tableName, profileId, cancellationToken);
        if (current.Count != captured.Count)
        {
            return false;
        }

        foreach (var (id, rowVersion) in captured)
        {
            if (!current.TryGetValue(id, out var currentVersion) || currentVersion != rowVersion)
            {
                return false;
            }
        }


        return true;
    }

    private static bool JsonEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                var aProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                return aProps.Count == bProps.Count
                    && aProps.All(kv => bProps.TryGetValue(kv.Key, out var other) && JsonEquals(kv.Value, other));
            case JsonValueKind.Array:
                var aItems = a.EnumerateArray().ToList();
                var bItems = b.EnumerateArray().ToList();
                return aItems.Count == bItems.Count && aItems.Zip(bItems, JsonEquals).All(x => x);
            case JsonValueKind.String:
                return a.GetString() == b.GetString();
            default:
                return a.GetRawText() == b.GetRawText();
        }
    }

    private async Task SaveWithConcurrencyCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("The row was modified by another request.");
        }
    }
}
