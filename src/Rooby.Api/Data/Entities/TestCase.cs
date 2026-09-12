using System.Text.Json;

namespace Rooby.Api.Data.Entities;

public class TestCase
{
    public Guid Id { get; set; }

    public Guid ProfileId { get; set; }

    public int VersionId { get; set; }

    public string ItemKey { get; set; } = string.Empty;

    public JsonDocument InputData { get; set; } = null!;

    public JsonDocument OutputValue { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public string Remarks { get; set; } = string.Empty;

    public UserLog Created { get; set; } = new();

    public UserLog? LastModified { get; set; }

    public UserLog? Deleted { get; set; }
}
