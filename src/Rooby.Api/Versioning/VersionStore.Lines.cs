using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Versioning;

public sealed partial class VersionStore
{
    public async Task<ItemLine> CreateLineAsync(
        Guid profileId, Guid itemId, Guid? schemaId, bool inheritSchema, NpgsqlRange<DateOnly>? validity,
        int sortOrder, string remarks, JsonDocument content, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);

        var line = new ItemLine
        {
            Id = Guid.CreateVersion7(),
            ProfileId = profileId,
            ItemId = itemId,
            VersionId = -1,
            SortOrder = sortOrder,
            SchemaId = schemaId,
            InheritSchema = inheritSchema,
            Validity = validity,
            Remarks = remarks,
            Content = content,
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId },
        };
        db.ItemLines.Add(line);
        await db.SaveChangesAsync(cancellationToken);
        return line;
    }

    public async Task<ItemLine> EditLineAsync(
        Guid profileId, Guid lineId, Guid? schemaId, bool inheritSchema, NpgsqlRange<DateOnly>? validity,
        string remarks, JsonDocument content, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);
        return await UpsertDraftLineAsync(profileId, lineId, expectedRowVersion, userId, l =>
        {
            l.SchemaId = schemaId;
            l.InheritSchema = inheritSchema;
            l.Validity = validity;
            l.Remarks = remarks;
            l.Content = content;
        }, cancellationToken);
    }

    public async Task DeleteLineAsync(Guid profileId, Guid lineId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);

        var draft = await db.ItemLines.SingleOrDefaultAsync(l => l.ProfileId == profileId && l.Id == lineId && l.VersionId == -1, cancellationToken);
        if (draft is not null)
        {
            var everPublished = await db.ItemLines.AnyAsync(l => l.ProfileId == profileId && l.Id == lineId && l.VersionId > 0, cancellationToken);
            if (!everPublished)
            {
                db.ItemLines.Remove(draft);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        await UpsertDraftLineAsync(profileId, lineId, expectedRowVersion, userId, l =>
        {
            l.IsDeleted = true;
            l.Deleted = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId };
        }, cancellationToken);
    }

    public async Task<ItemLine> RestoreLineAsync(Guid profileId, Guid lineId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);
        return await UpsertDraftLineAsync(profileId, lineId, expectedRowVersion, userId, l =>
        {
            l.IsDeleted = false;
            l.Deleted = null;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ItemLine>> ReplaceLinesAsync(
        Guid profileId, Guid itemId, IReadOnlyList<LineInput> lines, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);

        var workingSet = await GetWorkingSetAsync(profileId, cancellationToken);
        var currentLines = workingSet.Lines.Where(l => l.ItemId == itemId).ToDictionary(l => l.Id);

        var result = new List<ItemLine>();
        var seenIds = new HashSet<Guid>();
        var sortOrder = 1000;

        foreach (var input in lines)
        {
            var existing = input.Id is { } id && currentLines.TryGetValue(id, out var found) ? found : null;

            if (existing is not null && IsUnchanged(existing, input, sortOrder))
            {
                result.Add(existing);
                seenIds.Add(existing.Id);
                sortOrder += 1000;
                continue;
            }

            ItemLine written;
            if (existing is not null)
            {
                var rowVersion = (uint)db.Entry(existing).Property("RowVersion").CurrentValue!;
                written = await UpsertDraftLineAsync(profileId, existing.Id, rowVersion, userId, l =>
                {
                    l.SortOrder = sortOrder;
                    l.SchemaId = input.SchemaId;
                    l.InheritSchema = input.InheritSchema;
                    l.Validity = input.Validity;
                    l.Remarks = input.Remarks;
                    l.Content = input.Content;
                }, cancellationToken);
                seenIds.Add(existing.Id);
            }
            else
            {
                written = await CreateLineAsync(
                    profileId, itemId, input.SchemaId, input.InheritSchema, input.Validity, sortOrder, input.Remarks,
                    input.Content, userId, cancellationToken);
                seenIds.Add(written.Id);
            }

            result.Add(written);
            sortOrder += 1000;
        }

        foreach (var stale in currentLines.Values.Where(l => !seenIds.Contains(l.Id)))
        {
            var rowVersion = (uint)db.Entry(stale).Property("RowVersion").CurrentValue!;
            await DeleteLineAsync(profileId, stale.Id, rowVersion, userId, cancellationToken);
        }

        return result;
    }

    public async Task ReorderLinesAsync(
        Guid profileId, Guid itemId, IReadOnlyList<Guid> orderedLineIds, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);

        var workingSet = await GetWorkingSetAsync(profileId, cancellationToken);
        var currentById = workingSet.Lines.Where(l => l.ItemId == itemId).ToDictionary(l => l.Id);

        var currentOrder = currentById.Values.OrderBy(l => l.SortOrder).Select(l => l.Id).ToList();
        if (currentOrder.SequenceEqual(orderedLineIds))
        {
            return;
        }

        // Simplified gap allocation: renumber every line to the next gap step. A minimal-touch
        // "renumber only when a gap is exhausted" optimisation is left for a follow-up if profiling
        // shows reorder-heavy grids need it; ReplaceLines already avoids COW for untouched content.
        var sortOrder = 1000;
        foreach (var lineId in orderedLineIds)
        {
            if (!currentById.TryGetValue(lineId, out var line))
            {
                throw new KeyNotFoundException($"Line {lineId} is not part of item {itemId}'s working set.");
            }

            if (line.SortOrder != sortOrder)
            {
                var rowVersion = (uint)db.Entry(line).Property("RowVersion").CurrentValue!;
                await UpsertDraftLineAsync(profileId, lineId, rowVersion, userId, l => l.SortOrder = sortOrder, cancellationToken);
            }

            sortOrder += 1000;
        }
    }

    private static bool IsUnchanged(ItemLine existing, LineInput input, int sortOrder) =>
        existing.SortOrder == sortOrder
        && existing.SchemaId == input.SchemaId
        && existing.InheritSchema == input.InheritSchema
        && Equals(existing.Validity, input.Validity)
        && existing.Remarks == input.Remarks
        && JsonEquals(existing.Content.RootElement, input.Content.RootElement);

    private async Task<ItemLine> UpsertDraftLineAsync(
        Guid profileId, Guid lineId, uint expectedRowVersion, int userId, Action<ItemLine> mutate, CancellationToken cancellationToken)
    {
        var draft = await db.ItemLines.SingleOrDefaultAsync(l => l.ProfileId == profileId && l.Id == lineId && l.VersionId == -1, cancellationToken);
        if (draft is not null)
        {
            db.Entry(draft).Property("RowVersion").OriginalValue = expectedRowVersion;
            mutate(draft);
            draft.LastModified = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId };
            await SaveWithConcurrencyCheckAsync(cancellationToken);
            return draft;
        }

        var latest = await db.ItemLines
            .Where(l => l.ProfileId == profileId && l.Id == lineId && l.VersionId > 0)
            .OrderByDescending(l => l.VersionId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Line {lineId} not found.");

        var latestRowVersion = (uint)db.Entry(latest).Property("RowVersion").CurrentValue!;
        if (latestRowVersion != expectedRowVersion)
        {
            throw new ConcurrencyConflictException("Line was modified since it was read.");
        }

        var copy = new ItemLine
        {
            Id = latest.Id,
            ProfileId = profileId,
            ItemId = latest.ItemId,
            VersionId = -1,
            SortOrder = latest.SortOrder,
            SchemaId = latest.SchemaId,
            InheritSchema = latest.InheritSchema,
            Validity = latest.Validity,
            Remarks = latest.Remarks,
            Content = latest.Content,
            IsDeleted = latest.IsDeleted,
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId },
        };
        mutate(copy);
        db.ItemLines.Add(copy);
        await db.SaveChangesAsync(cancellationToken);
        return copy;
    }
}
