using System.Text.Json;
using NpgsqlTypes;

namespace Rooby.Api.Data.Entities;

public class VersionSchema
{
    public Guid ProfileId { get; set; }

    public int VersionId { get; set; }

    public Guid SchemaId { get; set; }

    public JsonDocument Definition { get; set; } = null!;

    public NpgsqlRange<DateOnly> Validity { get; set; }
}
