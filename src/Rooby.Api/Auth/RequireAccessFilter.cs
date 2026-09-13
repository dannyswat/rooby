using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Auth;

/// <summary>Endpoint filter enforcing a minimum <see cref="AccessLevel"/> flag, scoped by route values
/// "project" / "profile" (Project.Code / Profile.Code, per SPEC §2 URL-safe identifiers) (§3.4, §11.2).
/// A SystemAdmin grant always satisfies the check.</summary>
public sealed class RequireAccessFilter(AccessLevel flag, AccessScope scope) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var currentUser = http.RequestServices.GetRequiredService<ICurrentUser>();
        var userId = await currentUser.GetUserIdAsync(http.RequestAborted);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        Guid? projectId = null;
        Guid? profileId = null;

        if (scope is AccessScope.Project or AccessScope.Profile)
        {
            var db = http.RequestServices.GetRequiredService<RoobyDbContext>();
            var projectCode = http.Request.RouteValues["project"]?.ToString();
            if (projectCode is null)
            {
                return Results.NotFound();
            }

            projectId = await db.Projects
                .Where(p => p.Code == projectCode)
                .Select(p => (Guid?)p.Id)
                .SingleOrDefaultAsync(http.RequestAborted);
            if (projectId is null)
            {
                return Results.NotFound();
            }

            if (scope is AccessScope.Profile)
            {
                var profileCode = http.Request.RouteValues["profile"]?.ToString();
                if (profileCode is null)
                {
                    return Results.NotFound();
                }

                profileId = await db.Profiles
                    .Where(p => p.ProjectId == projectId && p.Code == profileCode)
                    .Select(p => (Guid?)p.Id)
                    .SingleOrDefaultAsync(http.RequestAborted);
                if (profileId is null)
                {
                    return Results.NotFound();
                }
            }
        }

        var accessService = http.RequestServices.GetRequiredService<IAccessService>();
        var effective = await accessService.GetEffectiveAccessAsync(userId.Value, projectId, profileId, http.RequestAborted);
        if (!effective.HasFlag(AccessLevel.SystemAdmin) && !effective.HasFlag(flag))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: $"Requires '{flag}' access.");
        }

        return await next(context);
    }
}

public static class RequireAccessEndpointExtensions
{
    public static RouteHandlerBuilder RequireAccess(
        this RouteHandlerBuilder builder, AccessLevel flag, AccessScope scope = AccessScope.System) =>
        builder.AddEndpointFilter(new RequireAccessFilter(flag, scope));
}
