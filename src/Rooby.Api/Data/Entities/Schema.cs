using System.Text.Json;
using NpgsqlTypes;

namespace Rooby.Api.Data.Entities;

public class Schema
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public NpgsqlRange<DateOnly> Validity { get; set; }

    public JsonDocument Definition { get; set; } = null!;

    public UserLog Created { get; set; } = new();

    public UserLog? LastModified { get; set; }

    public Project Project { get; set; } = null!;
}
