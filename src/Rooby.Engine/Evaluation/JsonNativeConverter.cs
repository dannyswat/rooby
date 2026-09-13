using System.Text.Json;

namespace Rooby.Engine.Evaluation;

/// <summary>Converts stored JSON (literals, input fixtures) to plain .NET values CEL/Celly understands.</summary>
public static class JsonNativeConverter
{
    public static object? ToNative(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => ToNativeNumber(element),
        JsonValueKind.Array => element.EnumerateArray().Select(ToNative).ToList(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToNative(p.Value), StringComparer.Ordinal),
        _ => null,
    };

    private static object ToNativeNumber(JsonElement element) =>
        element.TryGetInt64(out var integer) ? integer : element.GetDouble();
}
