using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rooby.Engine.Content;

/// <summary>RuleList execution strategy (SPEC §4.7).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleListStrategy
{
    FirstMatch,
    All,
    Chain,
    Aggregate,
    Priority,
}

/// <summary>Fold function used by the <c>Aggregate</c> strategy (SPEC §4.7).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AggregateFunction
{
    Sum,
    Min,
    Max,
    Count,
    Product,
}

public sealed record RuleListOutputSpec
{
    public required DataType Type { get; init; }
}

/// <summary>Item.Content shape for ItemType.RuleList (SPEC §4.7).</summary>
public sealed record RuleListContent
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public required RuleListStrategy Strategy { get; init; }

    /// <summary>Required (and only meaningful) when <see cref="Strategy"/> is <c>Aggregate</c>.</summary>
    public AggregateFunction? Aggregate { get; init; }

    /// <summary>Initial <c>prev</c> value for the <c>Chain</c> strategy.</summary>
    public JsonElement? Seed { get; init; }

    public required RuleListOutputSpec Output { get; init; }
}
