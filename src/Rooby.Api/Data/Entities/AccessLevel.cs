namespace Rooby.Api.Data.Entities;

[Flags]
public enum AccessLevel
{
    None = 0,
    Read = 1,
    Edit = 2,
    Publish = 4,
    ManageProfile = 8,
    ManageSchema = 16,
    ManageProject = 32,
    SystemAdmin = 64,
}
