using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Versioning;

public sealed partial class VersionStore
{
    public async Task<Item> CreateItemAsync(
        Guid profileId, string key, ItemType itemType, DataType dataType, Guid? schemaId, string description,
        JsonDocument content, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);

        var workingSet = await GetWorkingSetAsync(profileId, cancellationToken);
        if (workingSet.Items.Any(i => i.Key == key))
        {
            throw new InvalidOperationException($"Key '{key}' is already in use.");
        }

        var item = new Item
        {
            Id = Guid.CreateVersion7(),
            ProfileId = profileId,
            Key = key,
            VersionId = -1,
            ItemType = itemType,
            DataType = dataType,
            SchemaId = schemaId,
            Description = description,
            Content = content,
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId },
        };
        db.Items.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<Item> EditItemAsync(
        Guid profileId, Guid itemId, string key, string description, JsonDocument content, uint expectedRowVersion,
        int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);
        var everPublished = await db.Items.AnyAsync(i => i.ProfileId == profileId && i.Id == itemId && i.VersionId > 0, cancellationToken);

        return await UpsertDraftItemAsync(profileId, itemId, expectedRowVersion, userId, i =>
        {
            if (everPublished && i.Key != key)
            {
                throw new InvalidOperationException("Key is immutable once the item has been published.");
            }

            i.Key = key;
            i.Description = description;
            i.Content = content;
        }, cancellationToken);
    }

    public async Task DeleteItemAsync(Guid profileId, Guid itemId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);

        var draft = await db.Items.SingleOrDefaultAsync(i => i.ProfileId == profileId && i.Id == itemId && i.VersionId == -1, cancellationToken);
        if (draft is not null)
        {
            var everPublished = await db.Items.AnyAsync(i => i.ProfileId == profileId && i.Id == itemId && i.VersionId > 0, cancellationToken);
            if (!everPublished)
            {
                // Never published: no history to keep, hard-delete the item and its draft lines (§7.3).
                db.ItemLines.RemoveRange(db.ItemLines.Where(l => l.ProfileId == profileId && l.ItemId == itemId && l.VersionId == -1));
                db.Items.Remove(draft);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        await UpsertDraftItemAsync(profileId, itemId, expectedRowVersion, userId, i =>
        {
            i.IsDeleted = true;
            i.Deleted = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId };
        }, cancellationToken);
    }

    public async Task<Item> RestoreItemAsync(Guid profileId, Guid itemId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);
        return await UpsertDraftItemAsync(profileId, itemId, expectedRowVersion, userId, i =>
        {
            i.IsDeleted = false;
            i.Deleted = null;
        }, cancellationToken);
    }

    private async Task<Item> UpsertDraftItemAsync(
        Guid profileId, Guid itemId, uint expectedRowVersion, int userId, Action<Item> mutate, CancellationToken cancellationToken)
    {
        var draft = await db.Items.SingleOrDefaultAsync(i => i.ProfileId == profileId && i.Id == itemId && i.VersionId == -1, cancellationToken);
        if (draft is not null)
        {
            db.Entry(draft).Property("RowVersion").OriginalValue = expectedRowVersion;
            mutate(draft);
            draft.LastModified = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId };
            await SaveWithConcurrencyCheckAsync(cancellationToken);
            return draft;
        }

        var latest = await db.Items
            .Where(i => i.ProfileId == profileId && i.Id == itemId && i.VersionId > 0)
            .OrderByDescending(i => i.VersionId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Item {itemId} not found.");

        var latestRowVersion = (uint)db.Entry(latest).Property("RowVersion").CurrentValue!;
        if (latestRowVersion != expectedRowVersion)
        {
            throw new ConcurrencyConflictException("Item was modified since it was read.");
        }

        var copy = new Item
        {
            Id = latest.Id,
            ProfileId = profileId,
            Key = latest.Key,
            VersionId = -1,
            ItemType = latest.ItemType,
            DataType = latest.DataType,
            SchemaId = latest.SchemaId,
            Description = latest.Description,
            Content = latest.Content,
            IsDeleted = latest.IsDeleted,
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId },
        };
        mutate(copy);
        db.Items.Add(copy);
        await db.SaveChangesAsync(cancellationToken);
        return copy;
    }
}
