using System.Text.Json.Serialization;

namespace Rooby.Engine.Content;

/// <summary>
/// A DecisionTree node (SPEC §4.6): an <c>if</c>/<c>then</c>/<c>else</c>, a <c>switch</c>/<c>cases</c>/
/// <c>default</c>, or a leaf whose <c>result</c> is a value slot.
/// </summary>
[JsonConverter(typeof(DecisionTreeNodeJsonConverter))]
public abstract record DecisionTreeNode;

public sealed record DecisionTreeIfNode : DecisionTreeNode
{
    public required string If { get; init; }

    public required DecisionTreeNode Then { get; init; }

    public required DecisionTreeNode Else { get; init; }
}

public sealed record DecisionTreeSwitchNode : DecisionTreeNode
{
    public required string Switch { get; init; }

    public required IReadOnlyDictionary<string, DecisionTreeNode> Cases { get; init; }

    public DecisionTreeNode? Default { get; init; }
}

public sealed record DecisionTreeLeafNode : DecisionTreeNode
{
    public required ValueSlot Result { get; init; }
}

/// <summary>Item.Content shape for ItemType.DecisionTree (SPEC §4.6). Depth capped at 32.</summary>
public sealed record DecisionTreeContent
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public required DecisionTreeNode Root { get; init; }
}
