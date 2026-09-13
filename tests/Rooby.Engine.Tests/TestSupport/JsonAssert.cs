using System.Text.Json;

namespace Rooby.Engine.Tests.TestSupport;

/// <summary>Structural (order-independent for objects) JsonElement equality, for content round-trip tests.</summary>
public static class JsonAssert
{
    public static void Equivalent(string expectedJson, JsonElement actual)
    {
        using var doc = JsonDocument.Parse(expectedJson);
        Equivalent(doc.RootElement, actual);
    }

    public static void Equivalent(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected JSON kind {expected.ValueKind} but got {actual.ValueKind}. Expected: {expected.GetRawText()} Actual: {actual.GetRawText()}");
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedProps = expected.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                var actualProps = actual.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                foreach (var (name, expectedValue) in expectedProps)
                {
                    if (!actualProps.TryGetValue(name, out var actualValue))
                    {
                        throw new Xunit.Sdk.XunitException($"Missing property '{name}'. Actual: {actual.GetRawText()}");
                    }

                    Equivalent(expectedValue, actualValue);
                }

                // Extra actual-only properties are tolerated when null (our records serialize every
                // optional property explicitly, even when the fixture only lists the ones it cares about).
                foreach (var (name, actualValue) in actualProps)
                {
                    if (!expectedProps.ContainsKey(name) && actualValue.ValueKind != JsonValueKind.Null)
                    {
                        throw new Xunit.Sdk.XunitException($"Unexpected non-null property '{name}'. Actual: {actual.GetRawText()}");
                    }
                }

                break;
            case JsonValueKind.Array:
                var expectedItems = expected.EnumerateArray().ToList();
                var actualItems = actual.EnumerateArray().ToList();
                if (expectedItems.Count != actualItems.Count)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"Expected {expectedItems.Count} items but got {actualItems.Count}. Expected: {expected.GetRawText()} Actual: {actual.GetRawText()}");
                }

                for (var i = 0; i < expectedItems.Count; i++)
                {
                    Equivalent(expectedItems[i], actualItems[i]);
                }

                break;
            case JsonValueKind.Number:
                if (expected.GetDecimal() != actual.GetDecimal())
                {
                    throw new Xunit.Sdk.XunitException($"Expected {expected.GetRawText()} but got {actual.GetRawText()}.");
                }

                break;
            case JsonValueKind.String:
                if (expected.GetString() != actual.GetString())
                {
                    throw new Xunit.Sdk.XunitException($"Expected \"{expected.GetString()}\" but got \"{actual.GetString()}\".");
                }

                break;
            default:
                // True/False/Null have no payload beyond ValueKind, already compared above.
                break;
        }
    }
}
