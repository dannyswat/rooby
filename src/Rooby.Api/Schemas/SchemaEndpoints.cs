using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Rooby.Api.Auth;
using Rooby.Api.Concurrency;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;
using Rooby.Api.Validation;

namespace Rooby.Api.Schemas;

public sealed record SchemaResponse(Guid Id, string Code, string Name, DateOnly ValidFrom, DateOnly? ValidTo, JsonElement Definition, string ETag);

public sealed record SchemaRequest(string Code, string Name, DateOnly ValidFrom, DateOnly? ValidTo, JsonElement Definition);

/// <summary>Project-level schema CRUD (SPEC §3.8, §6, §10.1). Route segment "project" is Project.Code.</summary>
public static class SchemaEndpoints
{
    public static void MapSchemaEndpoints(this IEndpointRouteBuilder app)
    {
        var schemas = app.MapGroup("/api/projects/{project}/schemas");

        schemas.MapGet("/", ListAsync).RequireAccess(AccessLevel.Read, AccessScope.Project);
        schemas.MapPost("/", CreateAsync).RequireAccess(AccessLevel.ManageSchema, AccessScope.Project);

        var schema = schemas.MapGroup("/{schemaId:guid}");
        schema.MapGet("/", GetAsync).RequireAccess(AccessLevel.Read, AccessScope.Project);
        schema.MapPut("/", ReplaceAsync).RequireAccess(AccessLevel.ManageSchema, AccessScope.Project);
    }

    private static async Task<IResult> ListAsync(string project, RoobyDbContext db, CancellationToken ct)
    {
        var projectEntity = await db.Projects.SingleOrDefaultAsync(p => p.Code == project, ct);
        if (projectEntity is null)
        {
            return Results.NotFound();
        }

        var results = await db.Schemas.Where(s => s.ProjectId == projectEntity.Id).OrderBy(s => s.Code).ToListAsync(ct);
        return Results.Ok(results.Select(s => ToResponse(s, db)));
    }

    private static async Task<IResult> CreateAsync(string project, SchemaRequest request, RoobyDbContext db, CancellationToken ct)
    {
        var projectEntity = await db.Projects.SingleOrDefaultAsync(p => p.Code == project, ct);
        if (projectEntity is null)
        {
            return Results.NotFound();
        }

        var problems = Validate(request);
        if (problems.Count > 0)
        {
            return ValidationResults.Problem(problems);
        }

        var schema = new Schema
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectEntity.Id,
            Code = request.Code,
            Name = request.Name,
            Validity = ToRange(request.ValidFrom, request.ValidTo),
            Definition = JsonDocument.Parse(request.Definition.GetRawText()),
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = 0 },
        };
        db.Schemas.Add(schema);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsExclusionViolation(ex))
        {
            return ValidationResults.Problem(
                [new FieldProblem("validFrom", "overlapping_validity", "Another definition of this schema code already covers part of this validity period.")]);
        }

        return Results.Created($"/api/projects/{project}/schemas/{schema.Id}", ToResponse(schema, db));
    }

    private static async Task<IResult> GetAsync(string project, Guid schemaId, RoobyDbContext db, CancellationToken ct)
    {
        var entity = await FindAsync(db, project, schemaId, ct);
        return entity is null ? Results.NotFound() : Results.Ok(ToResponse(entity, db));
    }

    private static async Task<IResult> ReplaceAsync(
        string project, Guid schemaId, SchemaRequest request, HttpRequest httpRequest, RoobyDbContext db, CancellationToken ct)
    {
        var entity = await FindAsync(db, project, schemaId, ct);
        if (entity is null)
        {
            return Results.NotFound();
        }

        var problems = Validate(request);
        if (problems.Count > 0)
        {
            return ValidationResults.Problem(problems);
        }

        var entry = db.Entry(entity);
        if (!entry.TryApplyIfMatch(httpRequest.Headers.IfMatch))
        {
            return Results.Problem(statusCode: StatusCodes.Status412PreconditionFailed, title: "If-Match header is required.");
        }

        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.Validity = ToRange(request.ValidFrom, request.ValidTo);
        entity.Definition = JsonDocument.Parse(request.Definition.GetRawText());
        entity.LastModified = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = 0 };

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Schema was modified by another request.");
        }
        catch (DbUpdateException ex) when (IsExclusionViolation(ex))
        {
            return ValidationResults.Problem(
                [new FieldProblem("validFrom", "overlapping_validity", "Another definition of this schema code already covers part of this validity period.")]);
        }

        return Results.Ok(ToResponse(entity, db));
    }

    private static Task<Schema?> FindAsync(RoobyDbContext db, string project, Guid schemaId, CancellationToken ct) =>
        db.Schemas.SingleOrDefaultAsync(s => s.Id == schemaId && s.Project.Code == project, ct);

    private static List<FieldProblem> Validate(SchemaRequest request)
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

        if (request.ValidTo is not null && request.ValidTo <= request.ValidFrom)
        {
            problems.Add(new FieldProblem("validTo", "invalid_range", "validTo must be after validFrom."));
        }

        foreach (var error in RoobySchemaValidator.Validate(request.Definition))
        {
            problems.Add(new FieldProblem("definition", "unsupported_keyword", error));
        }

        return problems;
    }

    private static bool IsExclusionViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23P01" };

    private static NpgsqlRange<DateOnly> ToRange(DateOnly from, DateOnly? to) =>
        to is { } upper
            ? new NpgsqlRange<DateOnly>(from, true, false, upper, false, false)
            : new NpgsqlRange<DateOnly>(from, true, false, default, false, true);

    private static SchemaResponse ToResponse(Schema schema, RoobyDbContext db) =>
        new(
            schema.Id, schema.Code, schema.Name, schema.Validity.LowerBound,
            schema.Validity.UpperBoundInfinite ? null : schema.Validity.UpperBound,
            schema.Definition.RootElement.Clone(), db.Entry(schema).ToETag());
}
