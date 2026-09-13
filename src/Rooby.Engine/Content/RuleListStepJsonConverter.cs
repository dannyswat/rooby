using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rooby.Engine.Content;

/// <summary>(De)serializes the reference/inline-rule/bind union of <see cref="RuleListStep"/> (SPEC §4.7).</summary>
public sealed class RuleListStepJsonConverter : JsonConverter<RuleListStep>
{
    public override RuleListStep Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var element = doc.RootElement;

        var version = element.TryGetProperty("$v", out var versionElement) ? versionElement.GetInt32() : 1;
        var when = element.TryGetProperty("when", out var whenElement) ? whenElement.GetString() : null;
        var priority = element.TryGetProperty("priority", out var priorityElement) && priorityElement.ValueKind != JsonValueKind.Null
            ? priorityElement.GetInt32()
            : (int?)null;
        var enabled = !element.TryGetProperty("enabled", out var enabledElement) || enabledElement.GetBoolean();

        if (element.TryGetProperty("ref", out var refElement))
        {
            return new RuleListReferenceStep
            {
                Version = version,
                When = when,
                Priority = priority,
                Enabled = enabled,
                Ref = refElement.GetString() ?? string.Empty,
            };
        }

        if (element.TryGetProperty("rule", out var ruleElement))
        {
            return new RuleListInlineStep
            {
                Version = version,
                When = when,
                Priority = priority,
                Enabled = enabled,
                Rule = ValueSlotJsonConverter.ReadInlineRule(ruleElement, options),
            };
        }

        if (element.TryGetProperty("bind", out var bindElement))
        {
            var keys = element.TryGetProperty("keys", out var keysElement)
                ? keysElement.EnumerateArray().Select(k => k.GetString() ?? string.Empty).ToList()
                : [];
            return new RuleListBindStep
            {
                Version = version,
                When = when,
                Priority = priority,
                Enabled = enabled,
                Bind = bindElement.GetString() ?? string.Empty,
                Lookup = element.TryGetProperty("lookup", out var lookupElement) ? lookupElement.GetString() ?? string.Empty : string.Empty,
                Keys = keys,
            };
        }

        throw new JsonException("RuleList step must have 'ref', 'rule', or 'bind'.");
    }

    public override void Write(Utf8JsonWriter writer, RuleListStep value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("$v", value.Version);
        if (value.When is not null)
        {
            writer.WriteString("when", value.When);
        }

        if (value.Priority is not null)
        {
            writer.WriteNumber("priority", value.Priority.Value);
        }

        writer.WriteBoolean("enabled", value.Enabled);

        switch (value)
        {
            case RuleListReferenceStep referenceStep:
                writer.WriteString("ref", referenceStep.Ref);
                break;
            case RuleListInlineStep inlineStep:
                writer.WritePropertyName("rule");
                ValueSlotJsonConverter.WriteInlineRule(writer, inlineStep.Rule, options);
                break;
            case RuleListBindStep bindStep:
                writer.WriteString("bind", bindStep.Bind);
                writer.WriteString("lookup", bindStep.Lookup);
                writer.WritePropertyName("keys");
                writer.WriteStartArray();
                foreach (var key in bindStep.Keys)
                {
                    writer.WriteStringValue(key);
                }

                writer.WriteEndArray();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown rule list step.");
        }

        writer.WriteEndObject();
    }
}
