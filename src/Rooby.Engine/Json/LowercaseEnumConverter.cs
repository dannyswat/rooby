using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rooby.Engine.Json;

/// <summary>
/// Serializes single-word enum members lowercase (e.g. <c>Exact</c> → <c>"exact"</c>), for the few
/// SPEC §4 enums whose JSON values are lowercase rather than matching the C# member name verbatim.
/// </summary>
public sealed class LowercaseEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    public LowercaseEnumConverter()
        : base(JsonNamingPolicy.CamelCase)
    {
    }
}
