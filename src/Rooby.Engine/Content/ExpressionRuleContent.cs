using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rooby.Engine.Content;

/// <summary>A binding resolved by the engine before the CEL expression runs (SPEC §4.4).</summary>
public sealed record ExpressionBinding
{
    public required string Name { get; init; }

    /// <summary>Key of the referenced Lookup item.</summary>
    public required string Lookup { get; init; }

    /// <summary>CEL expressions producing the lookup's key columns, in order.</summary>
    public required IReadOnlyList<string> Keys { get; init; }
}

/// <summary>Item.Content shape for ItemType.ExpressionRule (SPEC §4.4).</summary>
public sealed record ExpressionRuleContent
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public IReadOnlyList<ExpressionBinding>? Bindings { get; init; }

    public required string Expression { get; init; }

    /// <summary>Returned when the expression yields null or errors with a default.</summary>
    public JsonElement? Default { get; init; }
}
