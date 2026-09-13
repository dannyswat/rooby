using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rooby.Engine.Schema;

/// <summary>Primitive JSON-Schema-like value types allowed in a RoobySchema node (SPEC §6).</summary>
#pragma warning disable CA1720 // member names match SPEC §6 JSON type names
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SchemaValueType
{
    [JsonStringEnumMemberName("string")]
    String,
    [JsonStringEnumMemberName("integer")]
    Integer,
    [JsonStringEnumMemberName("number")]
    Number,
    [JsonStringEnumMemberName("boolean")]
    Boolean,
    [JsonStringEnumMemberName("array")]
    Array,
    [JsonStringEnumMemberName("object")]
    Object,
}
#pragma warning restore CA1720

/// <summary>
/// One node of a RoobySchema document (SPEC §6): a JSON-Schema-like subset restricted to what maps
/// cleanly to CEL types. Nested <see cref="Properties"/>/<see cref="Items"/> are the same shape.
/// </summary>
public record SchemaNode
{
    public required SchemaValueType Type { get; init; }

    /// <summary>For <see cref="SchemaValueType.String"/>: <c>date</c> or <c>date-time</c> → CEL <c>timestamp</c>.</summary>
    public string? Format { get; init; }

    public IReadOnlyList<string>? Enum { get; init; }

    /// <summary>For <see cref="SchemaValueType.Object"/>.</summary>
    public IReadOnlyDictionary<string, SchemaNode>? Properties { get; init; }

    /// <summary>For <see cref="SchemaValueType.Object"/>.</summary>
    public IReadOnlyList<string>? Required { get; init; }

    /// <summary>
    /// For <see cref="SchemaValueType.Object"/> without declared <see cref="Properties"/>: either a
    /// boolean (<c>true</c> = open <c>map(string, dyn)</c>, <c>false</c> = no additional properties)
    /// or a nested schema constraining the value type.
    /// </summary>
    public JsonElement? AdditionalProperties { get; init; }

    /// <summary>For <see cref="SchemaValueType.Array"/>.</summary>
    public SchemaNode? Items { get; init; }
}

/// <summary>Root of a Schema.Definition document (SPEC §6): a <see cref="SchemaNode"/> plus <c>$v</c>.</summary>
public sealed record RoobySchema : SchemaNode
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;
}
