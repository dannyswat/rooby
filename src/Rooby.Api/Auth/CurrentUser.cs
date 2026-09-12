using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Auth;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor, RoobyDbContext db, IConfiguration configuration)
    : ICurrentUser
{
    private bool _resolved;
    private int? _userId;

    public ClaimsPrincipal Principal => httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());

    public async Task<int?> GetUserIdAsync(CancellationToken cancellationToken = default)
    {
        if (_resolved)
        {
            return _userId;
        }

        _resolved = true;
        _userId = await ResolveAsync(cancellationToken);
        return _userId;
    }

    private async Task<int?> ResolveAsync(CancellationToken cancellationToken)
    {
        if (Principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var loginName = Principal.FindFirstValue(RoobyClaimTypes.LoginName)
            ?? Principal.FindFirstValue("preferred_username")
            ?? Principal.FindFirstValue(ClaimTypes.Email)
            ?? Principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Principal.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(loginName))
        {
            return null;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.LoginName == loginName, cancellationToken);
        if (user is null)
        {
            user = await ProvisionAsync(loginName, cancellationToken);
        }

        return user.IsDisabled ? null : user.Id;
    }

    private async Task<User> ProvisionAsync(string loginName, CancellationToken cancellationToken)
    {
        var autoProvision = configuration.GetValue("Authentication:AutoProvision", false);
        var providerName = Principal.FindFirstValue(RoobyClaimTypes.Provider);
        var provider = Enum.TryParse<LoginProvider>(providerName, out var parsed) ? parsed : LoginProvider.Oidc;

        var user = new User
        {
            LoginName = loginName,
            LoginProvider = provider,
            DisplayName = Principal.FindFirstValue(ClaimTypes.Name) ?? loginName,
            IsDisabled = !autoProvision,
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = 0 },
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }
}
