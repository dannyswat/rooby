namespace Rooby.Api.Data.Entities;

[Flags]
public enum LoginProvider
{
    None = 0,
    Local = 1,
    ActiveDirectory = 2,
    Saml = 4,
    Oidc = 8,
}
