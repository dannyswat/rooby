using Rooby.Api.Data.Entities;

namespace Rooby.Api.Auth;

/// <summary>Endpoint filter enforcing a minimum <see cref="AccessLevel"/> flag, scoped by route values
/// "projectId" / "profileId" (SPEC §3.4, §11.2).</summary>
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

        var projectId = scope is AccessScope.Project or AccessScope.Profile ? ReadRouteGuid(http, "projectId") : null;
        var profileId = scope is AccessScope.Profile ? ReadRouteGuid(http, "profileId") : null;

        var accessService = http.RequestServices.GetRequiredService<IAccessService>();
        var effective = await accessService.GetEffectiveAccessAsync(userId.Value, projectId, profileId, http.RequestAborted);
        if (!effective.HasFlag(flag))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: $"Requires '{flag}' access.");
        }

        return await next(context);
    }

    private static Guid? ReadRouteGuid(HttpContext http, string name) =>
        http.Request.RouteValues.TryGetValue(name, out var value)
        && Guid.TryParse(value?.ToString(), out var guid)
            ? guid
            : null;
}

public static class RequireAccessEndpointExtensions
{
    public static RouteHandlerBuilder RequireAccess(
        this RouteHandlerBuilder builder, AccessLevel flag, AccessScope scope = AccessScope.System) =>
        builder.AddEndpointFilter(new RequireAccessFilter(flag, scope));
}
