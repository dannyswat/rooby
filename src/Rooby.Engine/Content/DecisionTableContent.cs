using System.Text.Json;
using System.Text.Json.Serialization;
using Rooby.Engine.Json;

namespace Rooby.Engine.Content;

/// <summary>Hit policy for a DecisionTable (SPEC §4.5).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DecisionTableHitPolicy
{
    First,
    Unique,
    Priority,
    Any,
    Collect,
    CollectSum,
    CollectMin,
    CollectMax,
    CollectCount,
}

/// <summary>Whether a DecisionTable column is a condition (input) or output column (SPEC §4.5).</summary>
[JsonConverter(typeof(LowercaseEnumConverter<DecisionTableColumnKind>))]
public enum DecisionTableColumnKind
{
    Condition,
    Output,
}

public sealed record DecisionTableColumn
{
    public required string Name { get; init; }

    public required DecisionTableColumnKind Kind { get; init; }

    /// <summary>Set for condition columns: a CEL expression evaluated against <c>input</c>/<c>vars</c>.</summary>
    public string? Expression { get; init; }

    /// <summary>Set for output columns.</summary>
    public DataType? Type { get; init; }
}

/// <summary>Item.Content shape for ItemType.DecisionTable (SPEC §4.5).</summary>
public sealed record DecisionTableContent
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public DecisionTableHitPolicy HitPolicy { get; init; } = DecisionTableHitPolicy.First;

    public required IReadOnlyList<DecisionTableColumn> Columns { get; init; }

    public JsonElement? Default { get; init; }
}

/// <summary>
/// ItemLine.Content shape for one DecisionTable row. <see cref="Cells"/> maps condition column name to
/// its shorthand cell-grammar text (SPEC §4.5); <see cref="Output"/> maps output column name to its
/// value slot.
/// </summary>
public sealed record DecisionTableRow
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public IReadOnlyDictionary<string, string>? Cells { get; init; }

    public required IReadOnlyDictionary<string, ValueSlot> Output { get; init; }

    /// <summary>Row priority, used by the <c>Priority</c> hit policy.</summary>
    public int? Priority { get; init; }
}
