namespace Rooby.Api.Data.Entities;

public class UserAccess
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? ProfileId { get; set; }

    public AccessLevel AccessLevel { get; set; }

    public bool IsRevoked { get; set; }

    public UserLog Created { get; set; } = new();

    public UserLog? LastModified { get; set; }

    public UserLog? Revoked { get; set; }
}
