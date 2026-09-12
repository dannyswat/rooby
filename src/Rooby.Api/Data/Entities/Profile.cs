namespace Rooby.Api.Data.Entities;

public class Profile
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "UTC";

    public string? PublishUri { get; set; }

    public string? PublishSecret { get; set; }

    public string? ApiKeyHash { get; set; }

    public UserLog? ApiKeyRotated { get; set; }

    public int NextStashId { get; set; } = -2;

    public TestGate TestGate { get; set; } = TestGate.Block;

    public bool IsDisabled { get; set; }

    public UserLog Created { get; set; } = new();

    public UserLog? LastModified { get; set; }

    public string Remark { get; set; } = string.Empty;

    public Project Project { get; set; } = null!;
}
