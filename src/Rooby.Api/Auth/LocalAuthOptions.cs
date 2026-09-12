namespace Rooby.Api.Auth;

/// <summary>Dev-only credential login that signs into the cookie scheme directly (SPEC §14 "Local provider").</summary>
public sealed class LocalAuthOptions
{
    public const string SectionName = "Authentication:Local";

    public bool Enabled { get; set; }

    public string LoginName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = "Local Administrator";
}
