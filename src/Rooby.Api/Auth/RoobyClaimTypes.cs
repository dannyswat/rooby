namespace Rooby.Api.Auth;

public static class RoobyClaimTypes
{
    /// <summary>Stable login identity, unique per <see cref="Rooby.Api.Data.Entities.User.LoginName"/>.</summary>
    public const string LoginName = "rooby:login_name";

    /// <summary>Name of the <see cref="Rooby.Api.Data.Entities.LoginProvider"/> used to authenticate this session.</summary>
    public const string Provider = "rooby:provider";
}
