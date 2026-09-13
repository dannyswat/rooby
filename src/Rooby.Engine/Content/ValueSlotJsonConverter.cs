using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rooby.Engine.Content;

/// <summary>
/// (De)serializes a <see cref="ValueSlot"/> from its four JSON forms (SPEC §4.0): a bare literal, or
/// an object keyed by exactly one of <c>expression</c>/<c>ref</c>/<c>rule</c>.
/// </summary>
public sealed class ValueSlotJsonConverter : JsonConverter<ValueSlot>
{
    public override ValueSlot Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return FromElement(doc.RootElement, options);
    }

    public override void Write(Utf8JsonWriter writer, ValueSlot value, JsonSerializerOptions options)
    {
        switch (value.Kind)
        {
            case ValueSlotKind.Literal:
                value.Literal.WriteTo(writer);
                break;
            case ValueSlotKind.Expression:
                writer.WriteStartObject();
                writer.WriteString("expression", value.Expression);
                writer.WriteEndObject();
                break;
            case ValueSlotKind.Ref:
                writer.WriteStartObject();
                writer.WriteString("ref", value.RefKey);
                if (value.RefKeys is { Count: > 0 })
                {
                    writer.WritePropertyName("keys");
                    writer.WriteStartArray();
                    foreach (var key in value.RefKeys)
                    {
                        writer.WriteStringValue(key);
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
                break;
            case ValueSlotKind.InlineRule:
                writer.WriteStartObject();
                writer.WritePropertyName("rule");
                WriteInlineRule(writer, value.Rule!, options);
                writer.WriteEndObject();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value.Kind, "Unknown value slot kind.");
        }
    }

    internal static ValueSlot FromElement(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("expression", out var expressionElement))
            {
                return ValueSlot.FromExpression(expressionElement.GetString() ?? string.Empty);
            }

            if (element.TryGetProperty("ref", out var refElement))
            {
                var key = refElement.GetString() ?? string.Empty;
                IReadOnlyList<string>? keys = null;
                if (element.TryGetProperty("keys", out var keysElement) && keysElement.ValueKind == JsonValueKind.Array)
                {
                    keys = keysElement.EnumerateArray().Select(k => k.GetString() ?? string.Empty).ToList();
                }

                return ValueSlot.FromRef(key, keys);
            }

            if (element.TryGetProperty("rule", out var ruleElement))
            {
                return ValueSlot.FromRule(ReadInlineRule(ruleElement, options));
            }
        }

        return ValueSlot.FromLiteral(element.Clone());
    }

    internal static InlineRuleSpec ReadInlineRule(JsonElement ruleElement, JsonSerializerOptions options)
    {
        _ = options;
        var itemType = Enum.Parse<ItemType>(ruleElement.GetProperty("itemType").GetString() ?? string.Empty);
        var content = ruleElement.GetProperty("content").Clone();
        IReadOnlyList<JsonElement>? lines = null;
        if (ruleElement.TryGetProperty("lines", out var linesElement) && linesElement.ValueKind == JsonValueKind.Array)
        {
            lines = linesElement.EnumerateArray().Select(l => l.Clone()).ToList();
        }

        return new InlineRuleSpec { ItemType = itemType, Content = content, Lines = lines };
    }

    internal static void WriteInlineRule(Utf8JsonWriter writer, InlineRuleSpec rule, JsonSerializerOptions options)
    {
        _ = options;
        writer.WriteStartObject();
        writer.WriteString("itemType", rule.ItemType.ToString());
        writer.WritePropertyName("content");
        rule.Content.WriteTo(writer);
        if (rule.Lines is { Count: > 0 })
        {
            writer.WritePropertyName("lines");
            writer.WriteStartArray();
            foreach (var line in rule.Lines)
            {
                line.WriteTo(writer);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
