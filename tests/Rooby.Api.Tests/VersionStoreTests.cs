using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;
using Rooby.Api.Versioning;

namespace Rooby.Api.Tests;

public sealed class VersionStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly JsonDocument EmptyContent = JsonDocument.Parse("{}");

    // 1. first publish → VersionId = 1; second → 2
    [Fact]
    public async Task First_publish_is_version_1_and_second_publish_is_version_2()
    {
        using var db = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(db);
        var store = new VersionStore(db);

        await store.CreateItemAsync(profile.Id, "Rate", ItemType.SingleValue, DataType.Number, null, "d", EmptyContent, 1);
        var v1 = await store.PublishAsync(profile.Id, "v1", await GetProfileRowVersionAsync(db, profile.Id), 1);
        Assert.Equal(1, v1);

        await store.EditItemAsync(profile.Id, (await GetItemAsync(db, profile.Id, "Rate")).Id, "Rate", "d2", EmptyContent, await GetItemRowVersionAsync(db, profile.Id, "Rate"), 1);
        var v2 = await store.PublishAsync(profile.Id, "v2", await GetProfileRowVersionAsync(db, profile.Id), 1);
        Assert.Equal(2, v2);
    }

    // 2. unchanged item: no new row; same (Id, VersionId) visible in v1 and v2
    [Fact]
    public async Task Unchanged_item_is_not_copied_into_the_next_version()
    {
        using var db = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(db);
        var store = new VersionStore(db);

        var item = await store.CreateItemAsync(profile.Id, "Rate", ItemType.SingleValue, DataType.Number, null, "d", EmptyContent, 1);
        await store.PublishAsync(profile.Id, "v1", await GetProfileRowVersionAsync(db, profile.Id), 1);

        // Publish v2 with an unrelated new item; "Rate" itself is never touched again.
        await store.CreateItemAsync(profile.Id, "Other", ItemType.SingleValue, DataType.Number, null, "d", EmptyContent, 1);
        await store.PublishAsync(profile.Id, "v2", await GetProfileRowVersionAsync(db, profile.Id), 1);

        using var readDb = fixture.CreateContext();
        var rateRows = await readDb.Items.Where(i => i.ProfileId == profile.Id && i.Id == item.Id).ToListAsync();
        Assert.Single(rateRows);
        Assert.Equal(1, rateRows[0].VersionId);
    }

    // 3. Snapshot(1) after v2 returns v1 content
    [Fact]
    public async Task Snapshot_at_an_old_version_returns_that_versions_content_after_a_later_publish()
    {
        using var db = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(db);
        var store = new VersionStore(db);

        var item = await store.CreateItemAsync(profile.Id, "Rate", ItemType.SingleValue, DataType.Number, null, "v1-desc", EmptyContent, 1);
        await store.PublishAsync(profile.Id, "v1", await GetProfileRowVersionAsync(db, profile.Id), 1);

        await store.EditItemAsync(profile.Id, item.Id, "Rate", "v2-desc", EmptyContent, await GetItemRowVersionAsync(db, profile.Id, "Rate"), 1);
        await store.PublishAsync(profile.Id, "v2", await GetProfileRowVersionAsync(db, profile.Id), 1);

        var v1Snapshot = await store.GetSnapshotAsync(profile.Id, 1);
        Assert.Equal("v1-desc", Assert.Single(v1Snapshot.Items).Description);

        var v2Snapshot = await store.GetSnapshotAsync(profile.Id, 2);
        Assert.Equal("v2-desc", Assert.Single(v2Snapshot.Items).Description);
    }

    // 4. delete then publish hides item in latest, keeps it in previous
    [Fact]
    public async Task Deleting_an_item_hides_it_in_the_latest_snapshot_but_not_in_the_previous_one()
    {
        using var db = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(db);
        var store = new VersionStore(db);

        var item = await store.CreateItemAsync(profile.Id, "Rate", ItemType.SingleValue, DataType.Number, null, "d", EmptyContent, 1);
        await store.PublishAsync(profile.Id, "v1", await GetProfileRowVersionAsync(db, profile.Id), 1);

        await store.DeleteItemAsync(profile.Id, item.Id, await GetItemRowVersionAsync(db, profile.Id, "Rate"), 1);
        await store.PublishAsync(profile.Id, "v2", await GetProfileRowVersionAsync(db, profile.Id), 1);

        var v1Snapshot = await store.GetSnapshotAsync(profile.Id, 1);
        var v2Snapshot = await store.GetSnapshotAsync(profile.Id, 2);

        Assert.Single(v1Snapshot.Items);
        Assert.Empty(v2Snapshot.Items);
    }

    // 5. concurrent publish: one success, one 409 (two parallel transactions in the test)
    [Fact]
    public async Task Concurrent_publish_lets_one_succeed_and_the_other_gets_a_conflict()
    {
        using var setupDb = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(setupDb);
        var setupStore = new VersionStore(setupDb);
        await setupStore.CreateItemAsync(profile.Id, "Rate", ItemType.SingleValue, DataType.Number, null, "d", EmptyContent, 1);
        var initialProfileRowVersion = await GetProfileRowVersionAsync(setupDb, profile.Id);

        using var dbA = fixture.CreateContext();
        using var dbB = fixture.CreateContext();
        var storeA = new VersionStore(dbA);
        var storeB = new VersionStore(dbB);

        var taskA = storeA.PublishAsync(profile.Id, "from A", initialProfileRowVersion, 1);
        var taskB = storeB.PublishAsync(profile.Id, "from B", initialProfileRowVersion, 1);

        var results = await Task.WhenAll(taskA.ContinueWith(t => (ok: !t.IsFaulted, error: t.Exception?.InnerException)), taskB.ContinueWith(t => (ok: !t.IsFaulted, error: t.Exception?.InnerException)));

        Assert.Single(results, r => r.ok);
        var failed = Assert.Single(results, r => !r.ok);
        Assert.IsType<ConcurrencyConflictException>(failed.error);
    }

    // 6. mid-publish draft change → 409
    [Fact]
    public async Task A_draft_edit_that_lands_mid_publish_aborts_the_publish_with_a_conflict()
    {
        using var db = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(db);
        var store = new VersionStore(db);

        var item = await store.CreateItemAsync(profile.Id, "Rate", ItemType.SingleValue, DataType.Number, null, "d", EmptyContent, 1);
        var profileRowVersion = await GetProfileRowVersionAsync(db, profile.Id);

        using var editDb = fixture.CreateContext();
        var editStore = new VersionStore(editDb);
        store.OnDraftVersionsCapturedForTesting = async ct =>
        {
            var rowVersion = await GetItemRowVersionAsync(editDb, profile.Id, "Rate");
            await editStore.EditItemAsync(profile.Id, item.Id, "Rate", "changed mid-publish", EmptyContent, rowVersion, 1, ct);
        };

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => store.PublishAsync(profile.Id, "v1", profileRowVersion, 1));
    }

    // 7. project schema change appears in profile vN only after that profile publishes
    [Fact]
    public async Task Project_schema_change_only_appears_in_the_profile_snapshot_after_that_profile_publishes()
    {
        using var db = fixture.CreateContext();
        var (project, profile) = await CreateProjectAndProfileAsync(db);
        var schema = await CreateSchemaAsync(db, project.Id, """{ "$v": 1, "type": "object" }""");
        var store = new VersionStore(db);

        var item = await store.CreateItemAsync(profile.Id, "Rule", ItemType.ExpressionRule, DataType.Number, schema.Id, "d", EmptyContent, 1);
        await store.PublishAsync(profile.Id, "v1", await GetProfileRowVersionAsync(db, profile.Id), 1);

        var capturedAtV1 = await db.VersionSchemas.SingleAsync(vs => vs.ProfileId == profile.Id && vs.SchemaId == schema.Id);
        Assert.Equal(1, capturedAtV1.VersionId);

        // Edit the project-level schema; the profile has not published again yet.
        schema.Definition = JsonDocument.Parse("""{ "$v": 2, "type": "object" }""");
        await db.SaveChangesAsync();

        var stillV1Capture = await db.VersionSchemas.Where(vs => vs.ProfileId == profile.Id && vs.SchemaId == schema.Id).ToListAsync();
        Assert.Single(stillV1Capture);
        Assert.Equal(1, stillV1Capture[0].Definition.RootElement.GetProperty("$v").GetInt32());

        // Now the profile publishes again: the new schema definition is captured at v2.
        await store.EditItemAsync(profile.Id, item.Id, "Rule", "d2", EmptyContent, await GetItemRowVersionAsync(db, profile.Id, "Rule"), 1);
        await store.PublishAsync(profile.Id, "v2", await GetProfileRowVersionAsync(db, profile.Id), 1);

        var capturedAtV2 = await db.VersionSchemas.SingleAsync(vs => vs.ProfileId == profile.Id && vs.SchemaId == schema.Id && vs.VersionId == 2);
        Assert.Equal(2, capturedAtV2.Definition.RootElement.GetProperty("$v").GetInt32());
    }

    // 8. editing one lookup line writes one item_line row and zero item rows
    [Fact]
    public async Task Editing_one_line_writes_exactly_one_line_row_and_no_item_row()
    {
        using var db = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(db);
        var store = new VersionStore(db);

        var item = await store.CreateItemAsync(profile.Id, "Lookup1", ItemType.Lookup, DataType.Number, null, "d", EmptyContent, 1);
        var line = await store.CreateLineAsync(profile.Id, item.Id, null, false, null, 1000, string.Empty, EmptyContent, 1);
        await store.PublishAsync(profile.Id, "v1", await GetProfileRowVersionAsync(db, profile.Id), 1);

        var lineRowVersion = await GetLineRowVersionAsync(db, profile.Id, line.Id);
        await store.EditLineAsync(profile.Id, line.Id, null, false, null, "updated", JsonDocument.Parse("""{"value":1}"""), lineRowVersion, 1);
        await store.PublishAsync(profile.Id, "v2", await GetProfileRowVersionAsync(db, profile.Id), 1);

        using var readDb = fixture.CreateContext();
        var itemRows = await readDb.Items.Where(i => i.ProfileId == profile.Id && i.Id == item.Id).ToListAsync();
        var lineRows = await readDb.ItemLines.Where(l => l.ProfileId == profile.Id && l.Id == line.Id).ToListAsync();

        Assert.Single(itemRows); // still only the original v1 revision
        Assert.Equal(2, lineRows.Count); // v1 revision + v2 revision
    }

    // 9. deleting an item hides its lines in latest, shows them in latest-1, no line tombstones
    [Fact]
    public async Task Deleting_an_item_hides_its_lines_without_writing_line_tombstones()
    {
        using var db = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(db);
        var store = new VersionStore(db);

        var item = await store.CreateItemAsync(profile.Id, "Lookup1", ItemType.Lookup, DataType.Number, null, "d", EmptyContent, 1);
        var line = await store.CreateLineAsync(profile.Id, item.Id, null, false, null, 1000, string.Empty, EmptyContent, 1);
        await store.PublishAsync(profile.Id, "v1", await GetProfileRowVersionAsync(db, profile.Id), 1);

        await store.DeleteItemAsync(profile.Id, item.Id, await GetItemRowVersionAsync(db, profile.Id, "Lookup1"), 1);
        await store.PublishAsync(profile.Id, "v2", await GetProfileRowVersionAsync(db, profile.Id), 1);

        var v1Snapshot = await store.GetSnapshotAsync(profile.Id, 1);
        var v2Snapshot = await store.GetSnapshotAsync(profile.Id, 2);
        Assert.Single(v1Snapshot.Lines);
        Assert.Empty(v2Snapshot.Lines);

        using var readDb = fixture.CreateContext();
        var lineRows = await readDb.ItemLines.Where(l => l.ProfileId == profile.Id && l.Id == line.Id).ToListAsync();
        Assert.Single(lineRows); // no separate tombstone row was written for the line
        Assert.False(lineRows[0].IsDeleted);
    }

    // 10. schema edited after v1 → readSchema(profile, 1) returns the v1 capture
    [Fact]
    public async Task Schema_read_at_an_old_version_returns_the_capture_from_that_version()
    {
        using var db = fixture.CreateContext();
        var (project, profile) = await CreateProjectAndProfileAsync(db);
        var schema = await CreateSchemaAsync(db, project.Id, """{ "$v": 1, "type": "object" }""");
        var store = new VersionStore(db);

        await store.CreateItemAsync(profile.Id, "Rule", ItemType.ExpressionRule, DataType.Number, schema.Id, "d", EmptyContent, 1);
        await store.PublishAsync(profile.Id, "v1", await GetProfileRowVersionAsync(db, profile.Id), 1);

        schema.Definition = JsonDocument.Parse("""{ "$v": 2, "type": "object" }""");
        await db.SaveChangesAsync();

        var readAtV1 = await db.VersionSchemas
            .Where(vs => vs.ProfileId == profile.Id && vs.SchemaId == schema.Id && vs.VersionId <= 1)
            .OrderByDescending(vs => vs.VersionId)
            .FirstAsync();
        Assert.Equal(1, readAtV1.Definition.RootElement.GetProperty("$v").GetInt32());
    }

    // 11. never-published item deleted → hard delete of item and its draft lines
    [Fact]
    public async Task Deleting_a_never_published_item_hard_deletes_it_and_its_draft_lines()
    {
        using var db = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(db);
        var store = new VersionStore(db);

        var item = await store.CreateItemAsync(profile.Id, "Lookup1", ItemType.Lookup, DataType.Number, null, "d", EmptyContent, 1);
        var line = await store.CreateLineAsync(profile.Id, item.Id, null, false, null, 1000, string.Empty, EmptyContent, 1);

        await store.DeleteItemAsync(profile.Id, item.Id, await GetItemRowVersionAsync(db, profile.Id, "Lookup1"), 1);

        using var readDb = fixture.CreateContext();
        Assert.Empty(await readDb.Items.Where(i => i.Id == item.Id).ToListAsync());
        Assert.Empty(await readDb.ItemLines.Where(l => l.Id == line.Id).ToListAsync());
    }

    // 12. key change on a published item → 400 (InvalidOperationException)
    [Fact]
    public async Task Changing_the_key_of_a_published_item_is_rejected()
    {
        using var db = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(db);
        var store = new VersionStore(db);

        var item = await store.CreateItemAsync(profile.Id, "Rate", ItemType.SingleValue, DataType.Number, null, "d", EmptyContent, 1);
        await store.PublishAsync(profile.Id, "v1", await GetProfileRowVersionAsync(db, profile.Id), 1);

        var rowVersion = await GetItemRowVersionAsync(db, profile.Id, "Rate");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.EditItemAsync(profile.Id, item.Id, "RenamedRate", "d2", EmptyContent, rowVersion, 1));
    }

    // 13. ReplaceLines with one changed row produces exactly one new line row
    [Fact]
    public async Task ReplaceLines_with_one_changed_row_writes_exactly_one_new_line_row()
    {
        using var db = fixture.CreateContext();
        var (_, profile) = await CreateProjectAndProfileAsync(db);
        var store = new VersionStore(db);

        var item = await store.CreateItemAsync(profile.Id, "Lookup1", ItemType.Lookup, DataType.Number, null, "d", EmptyContent, 1);
        var lines = new List<ItemLine>();
        for (var i = 0; i < 3; i++)
        {
            lines.Add(await store.CreateLineAsync(profile.Id, item.Id, null, false, null, (i + 1) * 1000, string.Empty, JsonDocument.Parse($$"""{"value":{{i}}}"""), 1));
        }

        await store.PublishAsync(profile.Id, "v1", await GetProfileRowVersionAsync(db, profile.Id), 1);

        var inputs = lines
            .Select((l, i) => new LineInput(l.Id, null, false, null, string.Empty, i == 1 ? JsonDocument.Parse("""{"value":999}""") : l.Content))
            .ToList();

        await store.ReplaceLinesAsync(profile.Id, item.Id, inputs, 1);

        using var readDb = fixture.CreateContext();
        foreach (var line in lines)
        {
            var revisionCount = await readDb.ItemLines.CountAsync(l => l.Id == line.Id);
            Assert.Equal(revisionCount, line.Id == lines[1].Id ? 2 : 1);
        }
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

    private static async Task<Schema> CreateSchemaAsync(RoobyDbContext db, Guid projectId, string definitionJson)
    {
        var schema = new Schema
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            Code = "order",
            Name = "Order",
            Validity = new NpgsqlTypes.NpgsqlRange<DateOnly>(new DateOnly(2020, 1, 1), true, false, default, false, true),
            Definition = JsonDocument.Parse(definitionJson),
        };
        db.Schemas.Add(schema);
        await db.SaveChangesAsync();
        return schema;
    }

    private static async Task<uint> GetProfileRowVersionAsync(RoobyDbContext db, Guid profileId)
    {
        var profile = await db.Profiles.SingleAsync(p => p.Id == profileId);
        return (uint)db.Entry(profile).Property("RowVersion").CurrentValue!;
    }

    private static async Task<Item> GetItemAsync(RoobyDbContext db, Guid profileId, string key) =>
        await db.Items.Where(i => i.ProfileId == profileId && i.Key == key).OrderByDescending(i => i.VersionId == -1).ThenByDescending(i => i.VersionId).FirstAsync();

    private static async Task<uint> GetItemRowVersionAsync(RoobyDbContext db, Guid profileId, string key)
    {
        var item = await GetItemAsync(db, profileId, key);
        return (uint)db.Entry(item).Property("RowVersion").CurrentValue!;
    }

    private static async Task<uint> GetLineRowVersionAsync(RoobyDbContext db, Guid profileId, Guid lineId)
    {
        var line = await db.ItemLines
            .Where(l => l.ProfileId == profileId && l.Id == lineId)
            .OrderByDescending(l => l.VersionId == -1)
            .ThenByDescending(l => l.VersionId)
            .FirstAsync();
        return (uint)db.Entry(line).Property("RowVersion").CurrentValue!;
    }
}
