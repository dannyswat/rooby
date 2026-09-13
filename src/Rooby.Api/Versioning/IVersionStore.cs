using System.Text.Json;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Versioning;

/// <summary>Draft/publish/stash algorithm for one profile's items, lines and test cases
/// (VERSION_CONTROL.md §4-§5.7, §5.9; SPEC §7).</summary>
public interface IVersionStore
{
    Task<int> LatestPublishedIdAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task EnsureDraftAsync(Guid profileId, int userId, CancellationToken cancellationToken = default);

    /// <summary>Published snapshot at version N (§7.1). N=0 returns an empty snapshot.</summary>
    Task<VersionSnapshot> GetSnapshotAsync(Guid profileId, int versionId, CancellationToken cancellationToken = default);

    /// <summary>Latest published snapshot overlaid with the draft (§4.2).</summary>
    Task<VersionSnapshot> GetWorkingSetAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Stash overlay on top of snapshot(stash.FromVersionId) (§4.3). Read-only; stash mutation is WP8.</summary>
    Task<VersionSnapshot> GetStashPreviewAsync(Guid profileId, int stashId, CancellationToken cancellationToken = default);

    Task<Item> CreateItemAsync(
        Guid profileId, string key, ItemType itemType, DataType dataType, Guid? schemaId, string description,
        JsonDocument content, int userId, CancellationToken cancellationToken = default);

    Task<Item> EditItemAsync(
        Guid profileId, Guid itemId, string key, string description, JsonDocument content, uint expectedRowVersion,
        int userId, CancellationToken cancellationToken = default);

    Task DeleteItemAsync(Guid profileId, Guid itemId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default);

    Task<Item> RestoreItemAsync(Guid profileId, Guid itemId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default);

    Task<ItemLine> CreateLineAsync(
        Guid profileId, Guid itemId, Guid? schemaId, bool inheritSchema, NpgsqlTypes.NpgsqlRange<DateOnly>? validity,
        int sortOrder, string remarks, JsonDocument content, int userId, CancellationToken cancellationToken = default);

    Task<ItemLine> EditLineAsync(
        Guid profileId, Guid lineId, Guid? schemaId, bool inheritSchema, NpgsqlTypes.NpgsqlRange<DateOnly>? validity,
        string remarks, JsonDocument content, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default);

    Task DeleteLineAsync(Guid profileId, Guid lineId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default);

    Task<ItemLine> RestoreLineAsync(Guid profileId, Guid lineId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default);

    /// <summary>Diffs the provided rows against the item's working-set lines and writes minimal COW rows.</summary>
    Task<IReadOnlyList<ItemLine>> ReplaceLinesAsync(
        Guid profileId, Guid itemId, IReadOnlyList<LineInput> lines, int userId, CancellationToken cancellationToken = default);

    /// <summary>Renumbers SortOrder only (no content change); renumbers all lines only once gaps are exhausted.</summary>
    Task ReorderLinesAsync(Guid profileId, Guid itemId, IReadOnlyList<Guid> orderedLineIds, int userId, CancellationToken cancellationToken = default);

    Task<TestCase> CreateTestCaseAsync(
        Guid profileId, string itemKey, JsonDocument inputData, JsonDocument outputValue, string remarks,
        int userId, CancellationToken cancellationToken = default);

    Task<TestCase> EditTestCaseAsync(
        Guid profileId, Guid testCaseId, JsonDocument inputData, JsonDocument outputValue, string remarks,
        uint expectedRowVersion, int userId, CancellationToken cancellationToken = default);

    Task DeleteTestCaseAsync(Guid profileId, Guid testCaseId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default);

    Task<TestCase> RestoreTestCaseAsync(Guid profileId, Guid testCaseId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default);

    /// <summary>Pending changes between the working set and the last published snapshot (§7.5).</summary>
    Task<DraftDiff> DiffDraftsAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Flips the draft to the next published version under a profile row lock (§5.5, §7.2).
    /// Returns the new VersionId.</summary>
    Task<int> PublishAsync(Guid profileId, string description, uint expectedProfileRowVersion, int userId, CancellationToken cancellationToken = default);

    Task DiscardDraftAsync(Guid profileId, CancellationToken cancellationToken = default);
}
