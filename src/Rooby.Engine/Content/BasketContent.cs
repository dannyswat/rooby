using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rooby.Engine.Content;

/// <summary>Item.Content shape for ItemType.Basket (SPEC §4.3).</summary>
public sealed record BasketContent
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public required DataType ElementType { get; init; }

    public bool Unique { get; init; }
}

/// <summary>ItemLine.Content shape for one Basket element (SPEC §4.3).</summary>
public sealed record BasketLine
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public required JsonElement Value { get; init; }
}
