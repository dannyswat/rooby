using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rooby.Engine.Content;

/// <summary>(De)serializes the if/switch/leaf union of <see cref="DecisionTreeNode"/> (SPEC §4.6).</summary>
public sealed class DecisionTreeNodeJsonConverter : JsonConverter<DecisionTreeNode>
{
    public override DecisionTreeNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return ReadNode(doc.RootElement, options);
    }

    public override void Write(Utf8JsonWriter writer, DecisionTreeNode value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case DecisionTreeIfNode ifNode:
                writer.WriteStartObject();
                writer.WriteString("if", ifNode.If);
                writer.WritePropertyName("then");
                Write(writer, ifNode.Then, options);
                writer.WritePropertyName("else");
                Write(writer, ifNode.Else, options);
                writer.WriteEndObject();
                break;
            case DecisionTreeSwitchNode switchNode:
                writer.WriteStartObject();
                writer.WriteString("switch", switchNode.Switch);
                writer.WritePropertyName("cases");
                writer.WriteStartObject();
                foreach (var (caseValue, node) in switchNode.Cases)
                {
                    writer.WritePropertyName(caseValue);
                    Write(writer, node, options);
                }

                writer.WriteEndObject();
                if (switchNode.Default is not null)
                {
                    writer.WritePropertyName("default");
                    Write(writer, switchNode.Default, options);
                }

                writer.WriteEndObject();
                break;
            case DecisionTreeLeafNode leaf:
                writer.WriteStartObject();
                writer.WritePropertyName("result");
                JsonSerializer.Serialize(writer, leaf.Result, options);
                writer.WriteEndObject();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown decision tree node.");
        }
    }

    private static DecisionTreeNode ReadNode(JsonElement element, JsonSerializerOptions options)
    {
        if (element.TryGetProperty("if", out var ifElement))
        {
            return new DecisionTreeIfNode
            {
                If = ifElement.GetString() ?? string.Empty,
                Then = ReadNode(element.GetProperty("then"), options),
                Else = ReadNode(element.GetProperty("else"), options),
            };
        }

        if (element.TryGetProperty("switch", out var switchElement))
        {
            var cases = new Dictionary<string, DecisionTreeNode>(StringComparer.Ordinal);
            foreach (var prop in element.GetProperty("cases").EnumerateObject())
            {
                cases[prop.Name] = ReadNode(prop.Value, options);
            }

            var defaultNode = element.TryGetProperty("default", out var defaultElement) ? ReadNode(defaultElement, options) : null;
            return new DecisionTreeSwitchNode { Switch = switchElement.GetString() ?? string.Empty, Cases = cases, Default = defaultNode };
        }

        if (element.TryGetProperty("result", out var resultElement))
        {
            return new DecisionTreeLeafNode { Result = ValueSlotJsonConverter.FromElement(resultElement.Clone(), options) };
        }

        throw new JsonException("Decision tree node must have 'if', 'switch', or 'result'.");
    }
}
