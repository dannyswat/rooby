using System.Security.Claims;

namespace Rooby.Api.Auth;

public interface ICurrentUser
{
    ClaimsPrincipal Principal { get; }

    /// <summary>Resolves the caller to a <see cref="Rooby.Api.Data.Entities.User"/> id, auto-provisioning
    /// (disabled unless <c>Authentication:AutoProvision</c> is true) on first login. Returns null if
    /// unauthenticated or the resolved user is disabled.</summary>
    Task<int?> GetUserIdAsync(CancellationToken cancellationToken = default);
}
