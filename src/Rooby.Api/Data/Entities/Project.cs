namespace Rooby.Api.Data.Entities;

public class Project
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsDisabled { get; set; }

    public UserLog Created { get; set; } = new();

    public UserLog? LastModified { get; set; }

    public string Remark { get; set; } = string.Empty;

    public List<Profile> Profiles { get; set; } = [];

    public List<Schema> Schemas { get; set; } = [];
}
