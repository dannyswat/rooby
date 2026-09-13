using System.Text.Json;

namespace Rooby.Api.Validation;

/// <summary>SPEC §6: Schema.Definition is a JSON-Schema-like document restricted to the subset that
/// maps to CEL types. Rejects any keyword or type outside that subset.</summary>
public static class RoobySchemaValidator
{
    private static readonly HashSet<string> AllowedTypes = ["object", "string", "integer", "number", "boolean", "array"];
    private static readonly HashSet<string> AllowedRootKeywords = ["$v", "type", "properties", "required", "additionalProperties"];
    private static readonly HashSet<string> AllowedNodeKeywords = ["type", "properties", "required", "enum", "minimum", "format", "items", "additionalProperties"];

    public static IReadOnlyList<string> Validate(JsonElement definition)
    {
        var errors = new List<string>();

        if (definition.ValueKind != JsonValueKind.Object)
        {
            errors.Add("$: definition must be a JSON object");
            return errors;
        }

        if (!definition.TryGetProperty("$v", out var version) || version.ValueKind != JsonValueKind.Number)
        {
            errors.Add("$.$v: missing required schema version number");
        }

        if (!definition.TryGetProperty("type", out var rootType) || rootType.ValueKind != JsonValueKind.String || rootType.GetString() != "object")
        {
            errors.Add("$.type: root schema must have \"type\": \"object\"");
        }

        ValidateNode(definition, "$", AllowedRootKeywords, errors);
        return errors;
    }

    private static void ValidateNode(JsonElement node, string path, HashSet<string> allowedKeywords, List<string> errors)
    {
        foreach (var property in node.EnumerateObject())
        {
            if (!allowedKeywords.Contains(property.Name))
            {
                errors.Add($"{path}.{property.Name}: unsupported keyword");
            }
        }

        if (!node.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            errors.Add($"{path}.type: missing or invalid 'type'");
            return;
        }

        var type = typeElement.GetString()!;
        if (!AllowedTypes.Contains(type))
        {
            errors.Add($"{path}.type: unsupported type '{type}'");
            return;
        }

        switch (type)
        {
            case "object":
                ValidateObjectNode(node, path, errors);
                break;
            case "array":
                if (node.TryGetProperty("items", out var items))
                {
                    ValidateNode(items, $"{path}.items", AllowedNodeKeywords, errors);
                }

                break;
            case "string":
                ValidateStringNode(node, path, errors);
                break;
            case "integer" or "number":
                if (node.TryGetProperty("minimum", out var minimum) && minimum.ValueKind != JsonValueKind.Number)
                {
                    errors.Add($"{path}.minimum: must be numeric");
                }

                break;
        }
    }

    private static void ValidateObjectNode(JsonElement node, string path, List<string> errors)
    {
        if (node.TryGetProperty("properties", out var properties))
        {
            if (properties.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}.properties: must be an object");
            }
            else
            {
                foreach (var property in properties.EnumerateObject())
                {
                    ValidateNode(property.Value, $"{path}.properties.{property.Name}", AllowedNodeKeywords, errors);
                }
            }
        }

        if (node.TryGetProperty("additionalProperties", out var additionalProperties) && additionalProperties.ValueKind == JsonValueKind.Object)
        {
            ValidateNode(additionalProperties, $"{path}.additionalProperties", AllowedNodeKeywords, errors);
        }

        if (node.TryGetProperty("required", out var required)
            && (required.ValueKind != JsonValueKind.Array || !required.EnumerateArray().All(e => e.ValueKind == JsonValueKind.String)))
        {
            errors.Add($"{path}.required: must be an array of strings");
        }
    }

    private static void ValidateStringNode(JsonElement node, string path, List<string> errors)
    {
        if (node.TryGetProperty("format", out var format)
            && (format.ValueKind != JsonValueKind.String || format.GetString() is not ("date" or "date-time")))
        {
            errors.Add($"{path}.format: unsupported format");
        }

        if (node.TryGetProperty("enum", out var enumValue)
            && (enumValue.ValueKind != JsonValueKind.Array || !enumValue.EnumerateArray().All(e => e.ValueKind == JsonValueKind.String)))
        {
            errors.Add($"{path}.enum: must be an array of strings");
        }
    }
}
