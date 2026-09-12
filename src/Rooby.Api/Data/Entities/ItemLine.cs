using System.Text.Json;
using NpgsqlTypes;

namespace Rooby.Api.Data.Entities;

public class ItemLine
{
    public Guid Id { get; set; }

    public Guid ProfileId { get; set; }

    /// <summary>Logical <see cref="Item.Id"/>; not a FK to a single revision row.</summary>
    public Guid ItemId { get; set; }

    public int VersionId { get; set; }

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }

    public Guid? SchemaId { get; set; }

    public bool InheritSchema { get; set; }

    public NpgsqlRange<DateOnly>? Validity { get; set; }

    public string Remarks { get; set; } = string.Empty;

    public JsonDocument Content { get; set; } = null!;

    public UserLog Created { get; set; } = new();

    public UserLog? LastModified { get; set; }

    public UserLog? Deleted { get; set; }
}
