using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Rooby.Api.Auth;
using Rooby.Api.Concurrency;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Versioning;

public sealed record ItemResponse(
    Guid Id, string Key, ItemType ItemType, DataType DataType, Guid? SchemaId, string Description,
    JsonElement Content, bool IsDeleted, int VersionId, string ETag);

public sealed record CreateItemRequest(string Key, ItemType ItemType, DataType DataType, Guid? SchemaId, string Description, JsonElement Content);

public sealed record UpdateItemRequest(string Key, string Description, JsonElement Content);

public sealed record LineResponse(
    Guid Id, Guid ItemId, int SortOrder, Guid? SchemaId, bool InheritSchema, DateOnly? ValidFrom, DateOnly? ValidTo,
    string Remarks, JsonElement Content, bool IsDeleted, string ETag);

public sealed record LineRequest(Guid? SchemaId, bool InheritSchema, DateOnly? ValidFrom, DateOnly? ValidTo, string Remarks, JsonElement Content);

public sealed record ReplaceLinesRequest(IReadOnlyList<LineRequest> Lines);

public sealed record ReorderLinesRequest(IReadOnlyList<Guid> LineIds);

public sealed record TestCaseResponse(Guid Id, string ItemKey, JsonElement InputData, JsonElement OutputValue, string Remarks, bool IsDeleted, string ETag);

public sealed record TestCaseRequest(string ItemKey, JsonElement InputData, JsonElement OutputValue, string Remarks);

public sealed record PublishRequest(string Description);

public sealed record VersionResponse(int VersionId, int FromVersionId, string Description, DateTimeOffset? PublishedAt);

/// <summary>Item/line/test-case/diff/publish/version endpoints (SPEC §10.1), scoped to one profile.
/// Route segments "project"/"profile" are Codes; each handler resolves them to the Profile row.</summary>
public static class VersioningEndpoints
{
    public static void MapVersioningEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects/{project}/profiles/{profile}");

        MapItemEndpoints(group);
        MapLineEndpoints(group);
        MapTestCaseEndpoints(group);

        group.MapGet("/diff", async (string project, string profile, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                return Results.Ok(await store.DiffDraftsAsync(profileId!.Value, ct));
            })
            .RequireAccess(AccessLevel.Read, AccessScope.Profile);

        group.MapPost("/publish", async (
                string project, string profile, PublishRequest request, HttpRequest httpRequest,
                ICurrentUser currentUser, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var expected = ParseIfMatch(httpRequest);
                if (expected is null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status412PreconditionFailed, title: "If-Match header is required.");
                }

                var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
                var versionId = await store.PublishAsync(profileId!.Value, request.Description, expected.Value, userId, ct);
                return Results.Ok(new { versionId });
            })
            .RequireAccess(AccessLevel.Publish, AccessScope.Profile);

        group.MapPost("/draft/discard", async (string project, string profile, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                await store.DiscardDraftAsync(profileId!.Value, ct);
                return Results.NoContent();
            })
            .RequireAccess(AccessLevel.Edit, AccessScope.Profile);

        group.MapGet("/versions", async (string project, string profile, RoobyDbContext db, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var versions = await db.Versions
                    .Where(v => v.ProfileId == profileId && v.VersionId > 0)
                    .OrderByDescending(v => v.VersionId)
                    .Select(v => new VersionResponse(v.VersionId, v.FromVersionId, v.Description, v.Published != null ? v.Published.At : null))
                    .ToListAsync(ct);
                return Results.Ok(versions);
            })
            .RequireAccess(AccessLevel.Read, AccessScope.Profile);

        group.MapGet("/versions/{n:int}", async (string project, string profile, int n, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var snapshot = await store.GetSnapshotAsync(profileId!.Value, n, ct);
                return Results.Ok(ToSnapshotResponse(snapshot, db));
            })
            .RequireAccess(AccessLevel.Read, AccessScope.Profile);
    }

    private static void MapItemEndpoints(RouteGroupBuilder group)
    {
        var items = group.MapGroup("/items");

        items.MapGet("/", async (string project, string profile, string? view, int? version, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var snapshot = view == "published"
                    ? await store.GetSnapshotAsync(profileId!.Value, version ?? await store.LatestPublishedIdAsync(profileId.Value, ct), ct)
                    : await store.GetWorkingSetAsync(profileId!.Value, ct);
                return Results.Ok(snapshot.Items.Select(i => ToResponse(i, db)));
            })
            .RequireAccess(AccessLevel.Read, AccessScope.Profile);

        items.MapPost("/", async (
                string project, string profile, CreateItemRequest request, ICurrentUser currentUser,
                RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
                var item = await store.CreateItemAsync(
                    profileId!.Value, request.Key, request.ItemType, request.DataType, request.SchemaId, request.Description,
                    ToDocument(request.Content), userId, ct);
                return Results.Created($"items/{item.Key}", ToResponse(item, db));
            })
            .RequireAccess(AccessLevel.Edit, AccessScope.Profile);

        var item = items.MapGroup("/{key}");

        item.MapGet("/", async (string project, string profile, string key, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var workingSet = await store.GetWorkingSetAsync(profileId!.Value, ct);
                var found = workingSet.Items.SingleOrDefault(i => i.Key == key);
                return found is null ? Results.NotFound() : Results.Ok(ToResponse(found, db));
            })
            .RequireAccess(AccessLevel.Read, AccessScope.Profile);

        item.MapPut("/", async (
                string project, string profile, string key, UpdateItemRequest request, HttpRequest httpRequest,
                ICurrentUser currentUser, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, existing, precondition) = await FindItemOrPreconditionAsync(db, store, project, profile, key, httpRequest, ct);
                if (precondition is not null)
                {
                    return precondition;
                }

                var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
                var updated = await store.EditItemAsync(
                    profileId, existing!.Id, request.Key, request.Description, ToDocument(request.Content), ParseIfMatch(httpRequest)!.Value, userId, ct);
                return Results.Ok(ToResponse(updated, db));
            })
            .RequireAccess(AccessLevel.Edit, AccessScope.Profile);

        item.MapDelete("/", async (
                string project, string profile, string key, HttpRequest httpRequest,
                ICurrentUser currentUser, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, existing, precondition) = await FindItemOrPreconditionAsync(db, store, project, profile, key, httpRequest, ct);
                if (precondition is not null)
                {
                    return precondition;
                }

                var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
                await store.DeleteItemAsync(profileId, existing!.Id, ParseIfMatch(httpRequest)!.Value, userId, ct);
                return Results.NoContent();
            })
            .RequireAccess(AccessLevel.Edit, AccessScope.Profile);

        item.MapPost("/restore", async (
                string project, string profile, string key, HttpRequest httpRequest,
                ICurrentUser currentUser, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, existing, precondition) = await FindItemOrPreconditionAsync(db, store, project, profile, key, httpRequest, ct);
                if (precondition is not null)
                {
                    return precondition;
                }

                var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
                var restored = await store.RestoreItemAsync(profileId, existing!.Id, ParseIfMatch(httpRequest)!.Value, userId, ct);
                return Results.Ok(ToResponse(restored, db));
            })
            .RequireAccess(AccessLevel.Edit, AccessScope.Profile);
    }

    private static void MapLineEndpoints(RouteGroupBuilder group)
    {
        var lines = group.MapGroup("/items/{key}/lines");

        lines.MapGet("/", async (string project, string profile, string key, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var workingSet = await store.GetWorkingSetAsync(profileId!.Value, ct);
                var found = workingSet.Items.SingleOrDefault(i => i.Key == key);
                if (found is null)
                {
                    return Results.NotFound();
                }

                var itemLines = workingSet.Lines.Where(l => l.ItemId == found.Id).OrderBy(l => l.SortOrder).Select(l => ToResponse(l, db));
                return Results.Ok(itemLines);
            })
            .RequireAccess(AccessLevel.Read, AccessScope.Profile);

        lines.MapPut("/", async (
                string project, string profile, string key, ReplaceLinesRequest request,
                ICurrentUser currentUser, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var workingSet = await store.GetWorkingSetAsync(profileId!.Value, ct);
                var found = workingSet.Items.SingleOrDefault(i => i.Key == key);
                if (found is null)
                {
                    return Results.NotFound();
                }

                var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
                var inputs = request.Lines
                    .Select(l => new LineInput(null, l.SchemaId, l.InheritSchema, ToRange(l.ValidFrom, l.ValidTo), l.Remarks, ToDocument(l.Content)))
                    .ToList();
                var result = await store.ReplaceLinesAsync(profileId.Value, found.Id, inputs, userId, ct);
                return Results.Ok(result.Select(l => ToResponse(l, db)));
            })
            .RequireAccess(AccessLevel.Edit, AccessScope.Profile);

        lines.MapPost("/reorder", async (
                string project, string profile, string key, ReorderLinesRequest request,
                ICurrentUser currentUser, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var workingSet = await store.GetWorkingSetAsync(profileId!.Value, ct);
                var found = workingSet.Items.SingleOrDefault(i => i.Key == key);
                if (found is null)
                {
                    return Results.NotFound();
                }

                var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
                await store.ReorderLinesAsync(profileId.Value, found.Id, request.LineIds, userId, ct);
                return Results.NoContent();
            })
            .RequireAccess(AccessLevel.Edit, AccessScope.Profile);
    }

    private static void MapTestCaseEndpoints(RouteGroupBuilder group)
    {
        var tests = group.MapGroup("/tests");

        tests.MapGet("/", async (string project, string profile, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var workingSet = await store.GetWorkingSetAsync(profileId!.Value, ct);
                return Results.Ok(workingSet.TestCases.Select(t => ToResponse(t, db)));
            })
            .RequireAccess(AccessLevel.Read, AccessScope.Profile);

        tests.MapPost("/", async (
                string project, string profile, TestCaseRequest request, ICurrentUser currentUser,
                RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
                var created = await store.CreateTestCaseAsync(
                    profileId!.Value, request.ItemKey, ToDocument(request.InputData), ToDocument(request.OutputValue), request.Remarks, userId, ct);
                return Results.Created($"tests/{created.Id}", ToResponse(created, db));
            })
            .RequireAccess(AccessLevel.Edit, AccessScope.Profile);

        var test = tests.MapGroup("/{testId:guid}");

        test.MapPut("/", async (
                string project, string profile, Guid testId, TestCaseRequest request, HttpRequest httpRequest,
                ICurrentUser currentUser, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var expected = ParseIfMatch(httpRequest);
                if (expected is null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status412PreconditionFailed, title: "If-Match header is required.");
                }

                var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
                var updated = await store.EditTestCaseAsync(
                    profileId!.Value, testId, ToDocument(request.InputData), ToDocument(request.OutputValue), request.Remarks, expected.Value, userId, ct);
                return Results.Ok(ToResponse(updated, db));
            })
            .RequireAccess(AccessLevel.Edit, AccessScope.Profile);

        test.MapDelete("/", async (
                string project, string profile, Guid testId, HttpRequest httpRequest,
                ICurrentUser currentUser, RoobyDbContext db, IVersionStore store, CancellationToken ct) =>
            {
                var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
                if (notFound is not null)
                {
                    return notFound;
                }

                var expected = ParseIfMatch(httpRequest);
                if (expected is null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status412PreconditionFailed, title: "If-Match header is required.");
                }

                var userId = (await currentUser.GetUserIdAsync(ct))!.Value;
                await store.DeleteTestCaseAsync(profileId!.Value, testId, expected.Value, userId, ct);
                return Results.NoContent();
            })
            .RequireAccess(AccessLevel.Edit, AccessScope.Profile);
    }

    private static async Task<(Guid? ProfileId, IResult? NotFound)> ResolveProfileIdAsync(
        RoobyDbContext db, string project, string profile, CancellationToken ct)
    {
        var profileId = await db.Profiles
            .Where(p => p.Code == profile && p.Project.Code == project)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(ct);
        return profileId is null ? (null, Results.NotFound()) : (profileId, null);
    }

    private static async Task<(Guid ProfileId, Item? Item, IResult? Precondition)> FindItemOrPreconditionAsync(
        RoobyDbContext db, IVersionStore store, string project, string profile, string key, HttpRequest httpRequest, CancellationToken ct)
    {
        var (profileId, notFound) = await ResolveProfileIdAsync(db, project, profile, ct);
        if (notFound is not null)
        {
            return (default, null, notFound);
        }

        var expected = ParseIfMatch(httpRequest);
        if (expected is null)
        {
            return (profileId!.Value, null, Results.Problem(statusCode: StatusCodes.Status412PreconditionFailed, title: "If-Match header is required."));
        }

        var workingSet = await store.GetWorkingSetAsync(profileId!.Value, ct);
        var found = workingSet.Items.SingleOrDefault(i => i.Key == key);
        return found is null ? (profileId.Value, null, Results.NotFound()) : (profileId.Value, found, null);
    }

    private static uint? ParseIfMatch(HttpRequest request)
    {
        var value = request.Headers.IfMatch.ToString().Trim().Trim('"');
        return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static JsonDocument ToDocument(JsonElement element) => JsonDocument.Parse(element.GetRawText());

    private static NpgsqlRange<DateOnly>? ToRange(DateOnly? from, DateOnly? to) =>
        from is null ? null : new NpgsqlRange<DateOnly>(from.Value, true, false, to ?? default, false, to is null);

    private static object ToSnapshotResponse(VersionSnapshot snapshot, RoobyDbContext db) => new
    {
        items = snapshot.Items.Select(i => ToResponse(i, db)),
        lines = snapshot.Lines.Select(l => ToResponse(l, db)),
        testCases = snapshot.TestCases.Select(t => ToResponse(t, db)),
    };

    private static ItemResponse ToResponse(Item item, RoobyDbContext db) => new(
        item.Id, item.Key, item.ItemType, item.DataType, item.SchemaId, item.Description,
        item.Content.RootElement.Clone(), item.IsDeleted, item.VersionId, TryETag(db, item));

    private static LineResponse ToResponse(ItemLine line, RoobyDbContext db) => new(
        line.Id, line.ItemId, line.SortOrder, line.SchemaId, line.InheritSchema,
        line.Validity?.LowerBound, line.Validity is { UpperBoundInfinite: false } v ? v.UpperBound : null,
        line.Remarks, line.Content.RootElement.Clone(), line.IsDeleted, TryETag(db, line));

    private static TestCaseResponse ToResponse(TestCase testCase, RoobyDbContext db) => new(
        testCase.Id, testCase.ItemKey, testCase.InputData.RootElement.Clone(), testCase.OutputValue.RootElement.Clone(),
        testCase.Remarks, testCase.IsDeleted, TryETag(db, testCase));

    private static string TryETag<T>(RoobyDbContext db, T entity)
        where T : class
    {
        var entry = db.ChangeTracker.Entries<T>().FirstOrDefault(e => ReferenceEquals(e.Entity, entity));
        return entry is null ? string.Empty : entry.ToETag();
    }
}

