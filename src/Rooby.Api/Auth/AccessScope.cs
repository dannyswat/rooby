namespace Rooby.Api.Auth;

/// <summary>Which UserAccess rows are eligible for an effective-access check (SPEC §3.4).</summary>
public enum AccessScope
{
    /// <summary>Only rows granted at (null, null) apply.</summary>
    System,

    /// <summary>Rows at (null,null) and (project,null) apply.</summary>
    Project,

    /// <summary>Rows at (null,null), (project,null) and (project,profile) apply.</summary>
    Profile,
}
