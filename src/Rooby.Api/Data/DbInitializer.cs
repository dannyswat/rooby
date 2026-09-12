using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Data;

/// <summary>Dev-only bootstrap so a fresh database has one usable SystemAdmin login.</summary>
public static class DbInitializer
{
    public static async Task SeedLocalAdminAsync(
        RoobyDbContext db, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var loginName = configuration["Bootstrap:AdminLoginName"];
        if (string.IsNullOrWhiteSpace(loginName))
        {
            return;
        }

        if (await db.Users.AnyAsync(u => u.LoginName == loginName, cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            LoginName = loginName,
            LoginProvider = LoginProvider.Local,
            DisplayName = configuration["Bootstrap:AdminDisplayName"] ?? loginName,
            Created = new UserLog { At = now, ByUserId = 0 },
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        db.UserAccesses.Add(new UserAccess
        {
            UserId = user.Id,
            AccessLevel = AccessLevel.SystemAdmin,
            Created = new UserLog { At = now, ByUserId = user.Id },
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
