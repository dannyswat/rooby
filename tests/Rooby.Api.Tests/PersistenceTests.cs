using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;
using Version = Rooby.Api.Data.Entities.Version;

namespace Rooby.Api.Tests;

public sealed class PersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly JsonDocument EmptyContent = JsonDocument.Parse("{}");

    [Fact]
    public void Model_has_no_pending_changes_after_initial_migration()
    {
        using var db = fixture.CreateContext();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Overlapping_schema_validity_for_same_project_and_code_is_rejected()
    {
        using var db = fixture.CreateContext();

        var project = new Project { Id = Guid.CreateVersion7(), Code = $"P{Guid.NewGuid():N}"[..15], Name = "Test" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        db.Schemas.Add(new Schema
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Code = "order",
            Name = "Order",
            Validity = new NpgsqlRange<DateOnly>(new DateOnly(2024, 1, 1), true, new DateOnly(2024, 6, 1), false),
            Definition = EmptyContent,
        });
        await db.SaveChangesAsync();

        db.Schemas.Add(new Schema
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Code = "order",
            Name = "Order v2",
            // Overlaps the first definition's [2024-01-01, 2024-06-01) validity.
            Validity = new NpgsqlRange<DateOnly>(new DateOnly(2024, 3, 1), true, new DateOnly(2024, 9, 1), false),
            Definition = EmptyContent,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Updating_a_published_item_row_throws()
    {
        using var db = fixture.CreateContext();
        var (project, profile) = await CreateProjectAndProfileAsync(db);

        db.Versions.Add(new Version { ProjectId = project.Id, ProfileId = profile.Id, VersionId = 1, FromVersionId = 0 });
        await db.SaveChangesAsync();

        var item = new Item
        {
            Id = Guid.CreateVersion7(),
            ProfileId = profile.Id,
            Key = "Rate",
            VersionId = 1,
            ItemType = ItemType.SingleValue,
            DataType = DataType.Number,
            Content = EmptyContent,
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        item.Description = "changed after publish";

        await Assert.ThrowsAsync<PublishedRowImmutableException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Xmin_changes_on_update_and_is_surfaced_as_row_version()
    {
        using var db = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(db);

        var entry = db.Entry(profile);
        var originalRowVersion = (uint)entry.Property("RowVersion").CurrentValue!;

        profile.Name = "Renamed";
        await db.SaveChangesAsync();

        var updatedRowVersion = (uint)entry.Property("RowVersion").CurrentValue!;
        Assert.NotEqual(originalRowVersion, updatedRowVersion);
    }

    private static async Task<(Project Project, Profile Profile)> CreateProjectAndProfileAsync(RoobyDbContext db)
    {
        var project = new Project { Id = Guid.CreateVersion7(), Code = $"P{Guid.NewGuid():N}"[..15], Name = "Test" };
        var profile = new Profile { Id = Guid.CreateVersion7(), ProjectId = project.Id, Code = "Main", Name = "Main" };
        db.Projects.Add(project);
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        return (project, profile);
    }
}
