using System.Text.Json;
using Celly.Providers;
using Celly.Types;
using Celly.Values;
using Rooby.Engine.Json;

namespace Rooby.Engine.Schema;

/// <summary>
/// Builds the CEL <c>input</c> declaration type + a matching <see cref="ITypeProvider"/> from a §6
/// RoobySchema, for checked-mode compiles (SPEC §5.1). Each object node with declared properties
/// becomes a named struct type so unknown-field access is a real checker error; an object without
/// declared properties becomes an open <c>map(string, T)</c> (T from <c>additionalProperties</c>, or
/// <c>dyn</c>).
/// </summary>
public static class CelDeclarationBuilder
{
    public static (CelType InputType, ITypeProvider TypeProvider) Build(RoobySchema schema, string rootTypeName = "Input")
    {
        var structFields = new Dictionary<string, IReadOnlyDictionary<string, CelType>>(StringComparer.Ordinal);
        var inputType = BuildNode(schema, rootTypeName, structFields);
        return (inputType, new SchemaTypeProvider(structFields));
    }

    private static CelType BuildNode(SchemaNode node, string typeName, Dictionary<string, IReadOnlyDictionary<string, CelType>> registry) =>
        node.Type switch
        {
            SchemaValueType.String => node.Format is "date" or "date-time" ? CelType.Timestamp : CelType.String,
            SchemaValueType.Integer => CelType.Int,
            SchemaValueType.Number => CelType.Double,
            SchemaValueType.Boolean => CelType.Bool,
            SchemaValueType.Array => CelType.List(node.Items is null ? CelType.Dyn : BuildNode(node.Items, typeName + "[]", registry)),
            SchemaValueType.Object => BuildObject(node, typeName, registry),
            _ => throw new ArgumentOutOfRangeException(nameof(node)),
        };

    private static CelType BuildObject(SchemaNode node, string typeName, Dictionary<string, IReadOnlyDictionary<string, CelType>> registry)
    {
        if (node.Properties is { Count: > 0 })
        {
            var fields = new Dictionary<string, CelType>(StringComparer.Ordinal);
            foreach (var (name, child) in node.Properties)
            {
                fields[name] = BuildNode(child, $"{typeName}.{name}", registry);
            }

            registry[typeName] = fields;
            return CelType.Struct(typeName);
        }

        return CelType.Map(CelType.String, ResolveAdditionalPropertiesType(node, typeName, registry));
    }

    private static CelType ResolveAdditionalPropertiesType(SchemaNode node, string typeName, Dictionary<string, IReadOnlyDictionary<string, CelType>> registry)
    {
        if (node.AdditionalProperties is not { } additionalProperties)
        {
            return CelType.Dyn;
        }

        return additionalProperties.ValueKind switch
        {
            JsonValueKind.Object => BuildNode(additionalProperties.Deserialize<SchemaNode>(RoobyJsonOptions.Default)!, typeName + "+", registry),
            _ => CelType.Dyn,
        };
    }

    /// <summary>Resolves struct field types declared by <see cref="Build"/>; unknown fields return null so the CEL checker reports them.</summary>
    private sealed class SchemaTypeProvider(IReadOnlyDictionary<string, IReadOnlyDictionary<string, CelType>> structFields) : ITypeProvider
    {
        public CelType? FindStructType(string name) =>
            structFields.ContainsKey(name) ? CelType.Struct(name) : null;

        public CelType? FindStructFieldType(string messageName, string fieldName) =>
            structFields.TryGetValue(messageName, out var fields) && fields.TryGetValue(fieldName, out var type) ? type : null;

        public CelValue? FindIdent(string name) => null;

        public CelValue NewValue(string messageName, IReadOnlyList<KeyValuePair<string, CelValue>> fields) =>
            throw new NotSupportedException("Rooby schema-derived struct types are not constructible in CEL expressions.");
    }
}
