using System.Text.Json;

namespace Rooby.Api.Data.Entities;

public class Item
{
    public Guid Id { get; set; }

    public Guid ProfileId { get; set; }

    public string Key { get; set; } = string.Empty;

    public int VersionId { get; set; }

    public ItemType ItemType { get; set; }

    public DataType DataType { get; set; }

    public bool IsDeleted { get; set; }

    public Guid? SchemaId { get; set; }

    public string Description { get; set; } = string.Empty;

    public JsonDocument Content { get; set; } = null!;

    public UserLog Created { get; set; } = new();

    public UserLog? LastModified { get; set; }

    public UserLog? Deleted { get; set; }
}
