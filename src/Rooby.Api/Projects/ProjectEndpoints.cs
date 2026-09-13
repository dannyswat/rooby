using Microsoft.EntityFrameworkCore;
using Rooby.Api.Auth;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;
using Rooby.Api.Validation;

namespace Rooby.Api.Projects;

public sealed record ProjectResponse(Guid Id, string Code, string Name, bool IsDisabled, string Remark);

public sealed record CreateProjectRequest(string Code, string Name, string? Remark);

public sealed record UpdateProjectRequest(string Name, bool IsDisabled, string? Remark);

/// <summary>Project CRUD (SPEC §3.1, §10.1). Route segment "project" is Project.Code.</summary>
public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var projects = app.MapGroup("/api/projects");

        projects.MapGet("/", ListAsync);
        projects.MapPost("/", CreateAsync).RequireAccess(AccessLevel.SystemAdmin);

        var project = projects.MapGroup("/{project}");
        project.MapGet("/", GetAsync).RequireAccess(AccessLevel.Read, AccessScope.Project);
        project.MapPatch("/", UpdateAsync).RequireAccess(AccessLevel.ManageProject, AccessScope.Project);
    }

    private static async Task<IResult> ListAsync(
        ICurrentUser currentUser, IAccessService accessService, RoobyDbContext db, CancellationToken ct)
    {
        var userId = await currentUser.GetUserIdAsync(ct);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var systemAccess = await accessService.GetEffectiveAccessAsync(userId.Value, null, null, ct);
        var query = db.Projects.Where(p => !p.IsDisabled);
        if (!systemAccess.HasFlag(AccessLevel.SystemAdmin) && !systemAccess.HasFlag(AccessLevel.Read))
        {
            var accessibleProjectIds = await db.UserAccesses
                .Where(a => a.UserId == userId && !a.IsRevoked && a.ProjectId != null)
                .Select(a => a.ProjectId!.Value)
                .Distinct()
                .ToListAsync(ct);
            query = query.Where(p => accessibleProjectIds.Contains(p.Id));
        }

        var results = await query.OrderBy(p => p.Code).ToListAsync(ct);
        return Results.Ok(results.Select(ToResponse));
    }

    private static async Task<IResult> CreateAsync(
        CreateProjectRequest request, ICurrentUser currentUser, RoobyDbContext db, CancellationToken ct)
    {
        var problems = new List<FieldProblem>();
        if (!IdentifierValidator.IsValid(request.Code))
        {
            problems.Add(new FieldProblem("code", "invalid_identifier", "Code must match ^[A-Za-z][A-Za-z0-9_]*$."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            problems.Add(new FieldProblem("name", "required", "Name is required."));
        }

        if (problems.Count == 0 && await db.Projects.AnyAsync(p => p.Code == request.Code, ct))
        {
            problems.Add(new FieldProblem("code", "duplicate", "Code is already in use."));
        }

        if (problems.Count > 0)
        {
            return ValidationResults.Problem(problems);
        }

        var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
        var project = new Project
        {
            Id = Guid.CreateVersion7(),
            Code = request.Code,
            Name = request.Name,
            Remark = request.Remark ?? string.Empty,
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId },
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/projects/{project.Code}", ToResponse(project));
    }

    private static async Task<IResult> GetAsync(string project, RoobyDbContext db, CancellationToken ct)
    {
        var entity = await db.Projects.SingleOrDefaultAsync(p => p.Code == project, ct);
        return entity is null ? Results.NotFound() : Results.Ok(ToResponse(entity));
    }

    private static async Task<IResult> UpdateAsync(
        string project, UpdateProjectRequest request, ICurrentUser currentUser, RoobyDbContext db, CancellationToken ct)
    {
        var entity = await db.Projects.SingleOrDefaultAsync(p => p.Code == project, ct);
        if (entity is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationResults.Problem([new FieldProblem("name", "required", "Name is required.")]);
        }

        entity.Name = request.Name;
        entity.IsDisabled = request.IsDisabled;
        entity.Remark = request.Remark ?? string.Empty;
        entity.LastModified = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = (await currentUser.GetUserIdAsync(ct))!.Value };

        await db.SaveChangesAsync(ct);

        return Results.Ok(ToResponse(entity));
    }

    private static ProjectResponse ToResponse(Project project) =>
        new(project.Id, project.Code, project.Name, project.IsDisabled, project.Remark);
}
