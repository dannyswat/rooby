using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Versioning;

public sealed partial class VersionStore
{
    public async Task<TestCase> CreateTestCaseAsync(
        Guid profileId, string itemKey, JsonDocument inputData, JsonDocument outputValue, string remarks,
        int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);

        var testCase = new TestCase
        {
            Id = Guid.CreateVersion7(),
            ProfileId = profileId,
            VersionId = -1,
            ItemKey = itemKey,
            InputData = inputData,
            OutputValue = outputValue,
            Remarks = remarks,
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId },
        };
        db.TestCases.Add(testCase);
        await db.SaveChangesAsync(cancellationToken);
        return testCase;
    }

    public async Task<TestCase> EditTestCaseAsync(
        Guid profileId, Guid testCaseId, JsonDocument inputData, JsonDocument outputValue, string remarks,
        uint expectedRowVersion, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);
        return await UpsertDraftTestCaseAsync(profileId, testCaseId, expectedRowVersion, userId, t =>
        {
            t.InputData = inputData;
            t.OutputValue = outputValue;
            t.Remarks = remarks;
        }, cancellationToken);
    }

    public async Task DeleteTestCaseAsync(Guid profileId, Guid testCaseId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);

        var draft = await db.TestCases.SingleOrDefaultAsync(t => t.ProfileId == profileId && t.Id == testCaseId && t.VersionId == -1, cancellationToken);
        if (draft is not null)
        {
            var everPublished = await db.TestCases.AnyAsync(t => t.ProfileId == profileId && t.Id == testCaseId && t.VersionId > 0, cancellationToken);
            if (!everPublished)
            {
                db.TestCases.Remove(draft);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        await UpsertDraftTestCaseAsync(profileId, testCaseId, expectedRowVersion, userId, t =>
        {
            t.IsDeleted = true;
            t.Deleted = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId };
        }, cancellationToken);
    }

    public async Task<TestCase> RestoreTestCaseAsync(Guid profileId, Guid testCaseId, uint expectedRowVersion, int userId, CancellationToken cancellationToken = default)
    {
        await EnsureDraftAsync(profileId, userId, cancellationToken);
        return await UpsertDraftTestCaseAsync(profileId, testCaseId, expectedRowVersion, userId, t =>
        {
            t.IsDeleted = false;
            t.Deleted = null;
        }, cancellationToken);
    }

    private async Task<TestCase> UpsertDraftTestCaseAsync(
        Guid profileId, Guid testCaseId, uint expectedRowVersion, int userId, Action<TestCase> mutate, CancellationToken cancellationToken)
    {
        var draft = await db.TestCases.SingleOrDefaultAsync(t => t.ProfileId == profileId && t.Id == testCaseId && t.VersionId == -1, cancellationToken);
        if (draft is not null)
        {
            db.Entry(draft).Property("RowVersion").OriginalValue = expectedRowVersion;
            mutate(draft);
            draft.LastModified = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId };
            await SaveWithConcurrencyCheckAsync(cancellationToken);
            return draft;
        }

        var latest = await db.TestCases
            .Where(t => t.ProfileId == profileId && t.Id == testCaseId && t.VersionId > 0)
            .OrderByDescending(t => t.VersionId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Test case {testCaseId} not found.");

        var latestRowVersion = (uint)db.Entry(latest).Property("RowVersion").CurrentValue!;
        if (latestRowVersion != expectedRowVersion)
        {
            throw new ConcurrencyConflictException("Test case was modified since it was read.");
        }

        var copy = new TestCase
        {
            Id = latest.Id,
            ProfileId = profileId,
            VersionId = -1,
            ItemKey = latest.ItemKey,
            InputData = latest.InputData,
            OutputValue = latest.OutputValue,
            Remarks = latest.Remarks,
            IsDeleted = latest.IsDeleted,
            Created = new UserLog { At = DateTimeOffset.UtcNow, ByUserId = userId },
        };
        mutate(copy);
        db.TestCases.Add(copy);
        await db.SaveChangesAsync(cancellationToken);
        return copy;
    }
}
