using System.Text.Json;
using Rooby.Engine.Json;

namespace Rooby.Engine.Content;

/// <summary>Serializes/deserializes Item/ItemLine Content JSON to/from the typed shapes in this namespace.</summary>
public static class ItemContentSerializer
{
    public static T Deserialize<T>(JsonElement element) =>
        element.Deserialize<T>(RoobyJsonOptions.Default) ?? throw new JsonException($"Unable to deserialize {typeof(T).Name}.");

    public static JsonElement Serialize<T>(T value) =>
        JsonSerializer.SerializeToElement(value, RoobyJsonOptions.Default);

    /// <summary>Parses Item.Content per its ItemType (SPEC §4). Returns the concrete <c>*Content</c> record.</summary>
    public static object ParseContent(ItemType itemType, JsonElement content) => itemType switch
    {
        ItemType.SingleValue => Deserialize<SingleValueContent>(content),
        ItemType.Lookup => Deserialize<LookupContent>(content),
        ItemType.Matrix => Deserialize<MatrixContent>(content),
        ItemType.Basket => Deserialize<BasketContent>(content),
        ItemType.ExpressionRule => Deserialize<ExpressionRuleContent>(content),
        ItemType.DecisionTable => Deserialize<DecisionTableContent>(content),
        ItemType.DecisionTree => Deserialize<DecisionTreeContent>(content),
        ItemType.RuleList => Deserialize<RuleListContent>(content),
        _ => throw new ArgumentOutOfRangeException(nameof(itemType)),
    };

    /// <summary>Parses ItemLine.Content per the parent ItemType (SPEC §4). Null for line-less types.</summary>
    public static object? ParseLine(ItemType itemType, JsonElement line) => itemType switch
    {
        ItemType.SingleValue => null,
        ItemType.Lookup => Deserialize<LookupLine>(line),
        ItemType.Matrix => Deserialize<MatrixRow>(line),
        ItemType.Basket => Deserialize<BasketLine>(line),
        ItemType.ExpressionRule => null,
        ItemType.DecisionTable => Deserialize<DecisionTableRow>(line),
        ItemType.DecisionTree => null,
        ItemType.RuleList => Deserialize<RuleListStep>(line),
        _ => throw new ArgumentOutOfRangeException(nameof(itemType)),
    };
}
