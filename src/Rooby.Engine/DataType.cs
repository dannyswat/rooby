using System.Text.Json.Serialization;

namespace Rooby.Engine;

/// <summary>Mirrors Rooby.Api.Data.Entities.DataType (SPEC §2, §3.6) — Engine has no reference to Api.</summary>
#pragma warning disable CA1720 // member names match SPEC §2/§3.6 DataType values exactly
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataType
{
    Boolean,
    Number,
    String,
    List,
    Date,
    Object,
}
#pragma warning restore CA1720
