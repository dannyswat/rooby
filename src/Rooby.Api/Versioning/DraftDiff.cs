using System.Text.Json;

namespace Rooby.Api.Versioning;

public enum ChangeKind
{
    Added,
    Modified,
    Deleted,
}

public sealed record LineDiff(Guid LineId, ChangeKind Change, JsonElement? Before, JsonElement? After);

public sealed record ItemDiff(Guid ItemId, string Key, ChangeKind Change, JsonElement? Before, JsonElement? After, IReadOnlyList<LineDiff> Lines);

public sealed record SchemaUpdate(Guid SchemaId, string Code);

/// <summary>SPEC §7.5: pending changes between the working set and the last published (or base) snapshot.</summary>
public sealed record DraftDiff(IReadOnlyList<ItemDiff> Items, IReadOnlyList<SchemaUpdate> SchemaUpdates);
