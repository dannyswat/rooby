using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Rooby.Api.Auth;
using Rooby.Api.Concurrency;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;
using Rooby.Api.Validation;

namespace Rooby.Api.Profiles;

public sealed record ProfileResponse(
    Guid Id, string Code, string Name, string TimeZone, string? PublishUri, bool HasPublishSecret,
    TestGate TestGate, bool IsDisabled, string Remark, string ETag);

public sealed record CreateProfileRequest(string Code, string Name, string? TimeZone, string? PublishUri, string? PublishSecret, string? Remark);

public sealed record UpdateProfileRequest(
    string Name, string TimeZone, string? PublishUri, string? PublishSecret, TestGate TestGate, bool IsDisabled, string? Remark);

public sealed record RotateApiKeyResponse(string ApiKey);

/// <summary>Profile CRUD and API key rotation (SPEC §3.2, §10.1). Route segments "project"/"profile" are Codes.</summary>
public static class ProfileEndpoints
{
    private const string PublishSecretProtectorPurpose = "Rooby.Profile.PublishSecret";

    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var profiles = app.MapGroup("/api/projects/{project}/profiles");

        profiles.MapGet("/", ListAsync);
        profiles.MapPost("/", CreateAsync).RequireAccess(AccessLevel.ManageProject, AccessScope.Project);

        var profile = profiles.MapGroup("/{profile}");
        profile.MapGet("/", GetAsync).RequireAccess(AccessLevel.Read, AccessScope.Profile);
        profile.MapPatch("/", UpdateAsync).RequireAccess(AccessLevel.ManageProfile, AccessScope.Profile);
        profile.MapPost("/apikey", RotateApiKeyAsync).RequireAccess(AccessLevel.ManageProfile, AccessScope.Profile);
    }

    private static async Task<IResult> ListAsync(
        string project, ICurrentUser currentUser, IAccessService accessService, RoobyDbContext db, CancellationToken ct)
    {
        var userId = await currentUser.GetUserIdAsync(ct);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var projectEntity = await db.Projects.SingleOrDefaultAsync(p => p.Code == project, ct);
        if (projectEntity is null)
        {
            return Results.NotFound();
        }

        var projectAccess = await accessService.GetEffectiveAccessAsync(userId.Value, projectEntity.Id, null, ct);
        List<Profile> results;
        if (projectAccess.HasFlag(AccessLevel.SystemAdmin) || projectAccess.HasFlag(AccessLevel.Read))
        {
            results = await db.Profiles.Where(p => p.ProjectId == projectEntity.Id).OrderBy(p => p.Code).ToListAsync(ct);
        }
        else
        {
            var accessibleProfileIds = await db.UserAccesses
                .Where(a => a.UserId == userId && !a.IsRevoked && a.ProjectId == projectEntity.Id && a.ProfileId != null)
                .Select(a => a.ProfileId!.Value)
                .Distinct()
                .ToListAsync(ct);
            results = await db.Profiles
                .Where(p => p.ProjectId == projectEntity.Id && accessibleProfileIds.Contains(p.Id))
                .OrderBy(p => p.Code)
                .ToListAsync(ct);
        }

        return Results.Ok(results.Select(p => ToResponse(p, db)));
    }

    private static async Task<IResult> CreateAsync(
        string project, CreateProfileRequest request, ICurrentUser currentUser, RoobyDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        var projectEntity = await db.Projects.SingleOrDefaultAsync(p => p.Code == project, ct);
        if (projectEntity is null)
        {
            return Results.NotFound();
        }

        var timeZone = string.IsNullOrEmpty(request.TimeZone) ? "UTC" : request.TimeZone;
        var problems = Validate(request.Code, request.Name, timeZone, request.PublishUri, configuration);
        if (problems.Count == 0 && await db.Profiles.AnyAsync(p => p.ProjectId == projectEntity.Id && p.Code == request.Code, ct))
        {
            problems.Add(new FieldProblem("code", "duplicate", "Code is already in use in this project."));
        }

        if (problems.Count > 0)
        {
            return ValidationResults.Problem(problems);
        }

        var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
        var profile = new Profile
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectEntity.Id,
            Code = request.Code,
            Name = request.Name,
            TimeZone = timeZone,
            PublishUri = request.PublishUri,
            Remark = request.Remark ?? string.Empty,
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId },
        };
        ApplyPublishSecret(profile, request.PublishSecret, db);
        db.Profiles.Add(profile);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/projects/{project}/profiles/{profile.Code}", ToResponse(profile, db));
    }

    private static async Task<IResult> GetAsync(string project, string profile, RoobyDbContext db, CancellationToken ct)
    {
        var entity = await FindAsync(db, project, profile, ct);
        return entity is null ? Results.NotFound() : Results.Ok(ToResponse(entity, db));
    }

    private static async Task<IResult> UpdateAsync(
        string project, string profile, UpdateProfileRequest request, ICurrentUser currentUser, HttpRequest httpRequest,
        RoobyDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        var entity = await FindAsync(db, project, profile, ct);
        if (entity is null)
        {
            return Results.NotFound();
        }

        var problems = Validate(entity.Code, request.Name, request.TimeZone, request.PublishUri, configuration);
        if (problems.Count > 0)
        {
            return ValidationResults.Problem(problems);
        }

        var entry = db.Entry(entity);
        if (!entry.TryApplyIfMatch(httpRequest.Headers.IfMatch))
        {
            return Results.Problem(statusCode: StatusCodes.Status412PreconditionFailed, title: "If-Match header is required.");
        }

        entity.Name = request.Name;
        entity.TimeZone = request.TimeZone;
        entity.PublishUri = request.PublishUri;
        entity.TestGate = request.TestGate;
        entity.IsDisabled = request.IsDisabled;
        entity.Remark = request.Remark ?? string.Empty;
        entity.LastModified = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = (await currentUser.GetUserIdAsync(ct))!.Value };
        if (request.PublishSecret is not null)
        {
            ApplyPublishSecret(entity, request.PublishSecret, db);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Profile was modified by another request.");
        }

        return Results.Ok(ToResponse(entity, db));
    }

    private static async Task<IResult> RotateApiKeyAsync(
        string project, string profile, ICurrentUser currentUser, HttpRequest httpRequest, RoobyDbContext db, CancellationToken ct)
    {
        var entity = await FindAsync(db, project, profile, ct);
        if (entity is null)
        {
            return Results.NotFound();
        }

        var entry = db.Entry(entity);
        if (!entry.TryApplyIfMatch(httpRequest.Headers.IfMatch))
        {
            return Results.Problem(statusCode: StatusCodes.Status412PreconditionFailed, title: "If-Match header is required.");
        }

        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var apiKey = Convert.ToHexStringLower(keyBytes);
        entity.ApiKeyHash = Convert.ToHexStringLower(SHA256.HashData(keyBytes));
        entity.ApiKeyRotated = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = (await currentUser.GetUserIdAsync(ct))!.Value };

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Profile was modified by another request.");
        }

        return Results.Ok(new RotateApiKeyResponse(apiKey));
    }

    private static Task<Profile?> FindAsync(RoobyDbContext db, string project, string profile, CancellationToken ct) =>
        db.Profiles.SingleOrDefaultAsync(p => p.Code == profile && p.Project.Code == project, ct);

    private static List<FieldProblem> Validate(string code, string name, string timeZone, string? publishUri, IConfiguration configuration)
    {
        var problems = new List<FieldProblem>();
        if (!IdentifierValidator.IsValid(code))
        {
            problems.Add(new FieldProblem("code", "invalid_identifier", "Code must match ^[A-Za-z][A-Za-z0-9_]*$."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            problems.Add(new FieldProblem("name", "required", "Name is required."));
        }

        if (!TimeZoneValidator.IsValid(timeZone))
        {
            problems.Add(new FieldProblem("timeZone", "invalid_time_zone", $"'{timeZone}' is not a known IANA time zone id."));
        }

        if (!PublishUriValidator.IsValid(publishUri, configuration))
        {
            problems.Add(new FieldProblem("publishUri", "invalid_publish_uri", "PublishUri must be an allow-listed https URL or a file:// path under the export root."));
        }

        return problems;
    }

    private static void ApplyPublishSecret(Profile profile, string? plainTextSecret, RoobyDbContext db)
    {
        if (string.IsNullOrEmpty(plainTextSecret))
        {
            return;
        }

        var protector = db.GetService<IDataProtectionProvider>().CreateProtector(PublishSecretProtectorPurpose);
        profile.PublishSecret = protector.Protect(plainTextSecret);
    }

    private static ProfileResponse ToResponse(Profile profile, RoobyDbContext db) =>
        new(
            profile.Id, profile.Code, profile.Name, profile.TimeZone, profile.PublishUri,
            !string.IsNullOrEmpty(profile.PublishSecret), profile.TestGate, profile.IsDisabled, profile.Remark,
            db.Entry(profile).ToETag());
}
