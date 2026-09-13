using System.Text.Json.Serialization;

namespace Rooby.Engine;

/// <summary>Mirrors Rooby.Api.Data.Entities.ItemType (SPEC §3.6, §4) — Engine has no reference to Api.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ItemType
{
    SingleValue,
    Lookup,
    Matrix,
    Basket,
    ExpressionRule,
    DecisionTable,
    DecisionTree,
    RuleList,
}
