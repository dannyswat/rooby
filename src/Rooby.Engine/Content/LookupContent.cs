using System.Text.Json;
using System.Text.Json.Serialization;
using Rooby.Engine.Json;

namespace Rooby.Engine.Content;

/// <summary>Match kind for a Lookup key column or a Matrix axis (SPEC §4.2, §4.8).</summary>
[JsonConverter(typeof(LowercaseEnumConverter<LookupMatch>))]
public enum LookupMatch
{
    Exact,
    Range,
}

public sealed record LookupKeyColumn
{
    public required string Name { get; init; }

    public required DataType Type { get; init; }
}

public sealed record LookupFieldSpec
{
    public required string Name { get; init; }

    public required DataType Type { get; init; }
}

/// <summary>The <c>value</c> spec of a Lookup: a scalar type, or an Object with declared fields.</summary>
public sealed record LookupValueSpec
{
    public required DataType Type { get; init; }

    public IReadOnlyList<LookupFieldSpec>? Fields { get; init; }
}

/// <summary>Item.Content shape for ItemType.Lookup (SPEC §4.2).</summary>
public sealed record LookupContent
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public required IReadOnlyList<LookupKeyColumn> Keys { get; init; }

    public LookupMatch Match { get; init; } = LookupMatch.Exact;

    public required LookupValueSpec Value { get; init; }

    public JsonElement? Default { get; init; }
}

/// <summary>
/// ItemLine.Content shape for a Lookup row. <see cref="Key"/> holds one entry per key column name for
/// exact match; for range match the last key column is replaced by <c>from</c>/<c>to</c> entries.
/// </summary>
public sealed record LookupLine
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public required IReadOnlyDictionary<string, JsonElement> Key { get; init; }

    public required JsonElement Value { get; init; }
}
