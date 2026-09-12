namespace Rooby.Api.Data.Entities;

public class User
{
    public int Id { get; set; }

    public string LoginName { get; set; } = string.Empty;

    public LoginProvider LoginProvider { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public bool IsDisabled { get; set; }

    public UserLog Created { get; set; } = new();

    public UserLog? LastModified { get; set; }
}
