using System.Text.Json;
using System.Text.Json.Serialization;
using Rooby.Engine.Content;

namespace Rooby.Engine.Json;

/// <summary>Shared <see cref="JsonSerializerOptions"/> for all Item/ItemLine Content (de)serialization.</summary>
public static class RoobyJsonOptions
{
    public static JsonSerializerOptions Default { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new ValueSlotJsonConverter());
        options.Converters.Add(new DecisionTreeNodeJsonConverter());
        options.Converters.Add(new RuleListStepJsonConverter());
        return options;
    }
}
