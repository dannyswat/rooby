using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Auth;

public sealed class AccessService(RoobyDbContext db) : IAccessService
{
    // Scoped service instance => this dictionary lives for one request (plan: "cached per request").
    private readonly Dictionary<(int, Guid?, Guid?), AccessLevel> _cache = [];

    public async Task<AccessLevel> GetEffectiveAccessAsync(
        int userId, Guid? projectId, Guid? profileId, CancellationToken cancellationToken = default)
    {
        var key = (userId, projectId, profileId);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var rows = await db.UserAccesses
            .Where(a => a.UserId == userId && !a.IsRevoked)
            .Where(a =>
                (a.ProjectId == null && a.ProfileId == null) ||
                (a.ProjectId == projectId && a.ProfileId == null) ||
                (a.ProjectId == projectId && a.ProfileId == profileId))
            .Select(a => a.AccessLevel)
            .ToListAsync(cancellationToken);

        var effective = rows.Aggregate(AccessLevel.None, (acc, level) => acc | level);
        _cache[key] = effective;
        return effective;
    }

    public async Task<UserAccess> GrantAsync(
        int userId, Guid? projectId, Guid? profileId, AccessLevel accessLevel, int grantedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (profileId is not null)
        {
            if (projectId is null)
            {
                throw new ArgumentException("A profile-scoped grant requires a projectId.", nameof(projectId));
            }

            var actualProjectId = await db.Profiles
                .Where(p => p.Id == profileId)
                .Select(p => p.ProjectId)
                .SingleOrDefaultAsync(cancellationToken);
            if (actualProjectId != projectId)
            {
                throw new ArgumentException("profileId does not belong to projectId.", nameof(profileId));
            }
        }

        var access = new UserAccess
        {
            UserId = userId,
            ProjectId = projectId,
            ProfileId = profileId,
            AccessLevel = accessLevel,
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = grantedByUserId },
        };
        db.UserAccesses.Add(access);
        await db.SaveChangesAsync(cancellationToken);
        _cache.Clear();
        return access;
    }

    public async Task RevokeAsync(int accessId, int revokedByUserId, CancellationToken cancellationToken = default)
    {
        var access = await db.UserAccesses.SingleOrDefaultAsync(a => a.Id == accessId, cancellationToken)
            ?? throw new KeyNotFoundException($"UserAccess {accessId} not found.");

        access.IsRevoked = true;
        access.Revoked = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = revokedByUserId };
        await db.SaveChangesAsync(cancellationToken);
        _cache.Clear();
    }
}
