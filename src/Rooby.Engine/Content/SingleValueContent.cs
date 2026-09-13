using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rooby.Engine.Content;

/// <summary>Item.Content shape for ItemType.SingleValue (SPEC §4.1).</summary>
public sealed record SingleValueContent
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public required JsonElement Value { get; init; }

    public string? Unit { get; init; }

    /// <summary>UI hint only; not interpreted by the engine.</summary>
    public JsonElement? Editor { get; init; }
}
