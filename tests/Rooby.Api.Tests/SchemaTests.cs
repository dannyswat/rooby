using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Rooby.Api.Data.Entities;
using Rooby.Api.Schemas;

namespace Rooby.Api.Tests;

public sealed class SchemaTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Valid_schema_definition_round_trips_through_the_database()
    {
        using var db = fixture.CreateContext();
        var project = new Project { Id = Guid.CreateVersion7(), Code = $"P{Guid.NewGuid():N}"[..15], Name = "Test" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var definitionJson = """{ "$v": 1, "type": "object", "properties": { "notional": { "type": "number" } } }""";
        var schema = new Schema
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Code = "order",
            Name = "Order",
            Validity = new NpgsqlRange<DateOnly>(new DateOnly(2024, 1, 1), true, false, default, false, true),
            Definition = JsonDocument.Parse(definitionJson),
        };
        db.Schemas.Add(schema);
        await db.SaveChangesAsync();

        using var reloadedDb = fixture.CreateContext();
        var reloaded = await reloadedDb.Schemas.AsNoTracking().SingleAsync(s => s.Id == schema.Id);

        Assert.Equal("order", reloaded.Code);
        Assert.True(JsonElementsAreEqual(JsonDocument.Parse(definitionJson).RootElement, reloaded.Definition.RootElement));
    }

    [Fact]
    public async Task Schema_resolver_returns_the_definition_current_on_the_given_date()
    {
        using var db = fixture.CreateContext();
        var project = new Project { Id = Guid.CreateVersion7(), Code = $"P{Guid.NewGuid():N}"[..15], Name = "Test" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var v1 = new Schema
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Code = "order",
            Name = "Order v1",
            Validity = new NpgsqlRange<DateOnly>(new DateOnly(2024, 1, 1), true, new DateOnly(2024, 6, 1), false),
            Definition = JsonDocument.Parse("""{ "$v": 1, "type": "object" }"""),
        };
        var v2 = new Schema
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Code = "order",
            Name = "Order v2",
            Validity = new NpgsqlRange<DateOnly>(new DateOnly(2024, 6, 1), true, false, default, false, true),
            Definition = JsonDocument.Parse("""{ "$v": 2, "type": "object" }"""),
        };
        db.Schemas.AddRange(v1, v2);
        await db.SaveChangesAsync();

        var resolver = new SchemaResolver(db);

        var beforeSwitch = await resolver.CurrentAsync(project.Id, "order", new DateOnly(2024, 3, 1));
        var afterSwitch = await resolver.CurrentAsync(project.Id, "order", new DateOnly(2024, 7, 1));

        Assert.Equal("Order v1", beforeSwitch!.Name);
        Assert.Equal("Order v2", afterSwitch!.Name);
    }

    private static bool JsonElementsAreEqual(JsonElement a, JsonElement b) =>
        a.GetRawText().ReplaceLineEndings(string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal)
            == b.GetRawText().ReplaceLineEndings(string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);
}
