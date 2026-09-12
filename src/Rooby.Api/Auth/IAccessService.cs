using Rooby.Api.Data.Entities;

namespace Rooby.Api.Auth;

public interface IAccessService
{
    /// <summary>Bitwise OR of non-revoked <see cref="UserAccess"/> rows matching the user at
    /// (null,null), (projectId,null) and (projectId,profileId) (SPEC §3.4).</summary>
    Task<AccessLevel> GetEffectiveAccessAsync(
        int userId, Guid? projectId, Guid? profileId, CancellationToken cancellationToken = default);

    Task<UserAccess> GrantAsync(
        int userId, Guid? projectId, Guid? profileId, AccessLevel accessLevel, int grantedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-revoke: sets IsRevoked + Revoked, never deletes the row.</summary>
    Task RevokeAsync(int accessId, int revokedByUserId, CancellationToken cancellationToken = default);
}
