using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Auth;

public sealed record MeResponse(int Id, string LoginName, string DisplayName, AccessLevel SystemAccess);

public sealed record UserResponse(int Id, string LoginName, string DisplayName, bool IsDisabled);

public sealed record UserAccessResponse(int Id, int UserId, Guid? ProjectId, Guid? ProfileId, AccessLevel AccessLevel, bool IsRevoked);

public sealed record GrantAccessRequest(int UserId, Guid? ProjectId, Guid? ProfileId, AccessLevel AccessLevel);

/// <summary>`/me`, `/users` and `/access` management endpoints (SPEC §10.1, §3.4).</summary>
public static class AccessEndpoints
{
    public static void MapAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/me", async (ICurrentUser currentUser, IAccessService accessService, RoobyDbContext db, CancellationToken ct) =>
        {
            var userId = await currentUser.GetUserIdAsync(ct);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FindAsync([userId.Value], ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var systemAccess = await accessService.GetEffectiveAccessAsync(userId.Value, null, null, ct);
            return Results.Ok(new MeResponse(user.Id, user.LoginName, user.DisplayName, systemAccess));
        });

        group.MapGet("/users", async (RoobyDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Users
                .OrderBy(u => u.LoginName)
                .Select(u => new UserResponse(u.Id, u.LoginName, u.DisplayName, u.IsDisabled))
                .ToListAsync(ct)))
            .RequireAccess(AccessLevel.SystemAdmin);

        group.MapGet("/access", async (int? userId, Guid? projectId, Guid? profileId, RoobyDbContext db, CancellationToken ct) =>
        {
            var query = db.UserAccesses.AsQueryable();
            if (userId is not null)
            {
                query = query.Where(a => a.UserId == userId);
            }

            if (projectId is not null)
            {
                query = query.Where(a => a.ProjectId == projectId);
            }

            if (profileId is not null)
            {
                query = query.Where(a => a.ProfileId == profileId);
            }

            var rows = await query
                .OrderBy(a => a.Id)
                .Select(a => new UserAccessResponse(a.Id, a.UserId, a.ProjectId, a.ProfileId, a.AccessLevel, a.IsRevoked))
                .ToListAsync(ct);
            return Results.Ok(rows);
        }).RequireAccess(AccessLevel.SystemAdmin);

        group.MapPost("/access", async (GrantAccessRequest request, IAccessService accessService, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var granterId = (await currentUser.GetUserIdAsync(ct))!.Value;
            var created = await accessService.GrantAsync(
                request.UserId, request.ProjectId, request.ProfileId, request.AccessLevel, granterId, ct);
            return Results.Created(
                $"/api/access/{created.Id}",
                new UserAccessResponse(created.Id, created.UserId, created.ProjectId, created.ProfileId, created.AccessLevel, created.IsRevoked));
        }).RequireAccess(AccessLevel.SystemAdmin);

        group.MapPost("/access/{id:int}/revoke", async (int id, IAccessService accessService, ICurrentUser currentUser, CancellationToken ct) =>
        {
            var revokerId = (await currentUser.GetUserIdAsync(ct))!.Value;
            try
            {
                await accessService.RevokeAsync(id, revokerId, ct);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }

            return Results.NoContent();
        }).RequireAccess(AccessLevel.SystemAdmin);
    }
}
