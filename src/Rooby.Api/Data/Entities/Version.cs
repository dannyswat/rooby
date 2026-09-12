namespace Rooby.Api.Data.Entities;

/// <summary>Named "Version" per SPEC §3.5; disambiguate from System.Version with a using alias where needed.</summary>
public class Version
{
    public int Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid ProfileId { get; set; }

    public int VersionId { get; set; }

    public int FromVersionId { get; set; }

    public string Description { get; set; } = string.Empty;

    public UserLog Created { get; set; } = new();

    public UserLog? Published { get; set; }
}
