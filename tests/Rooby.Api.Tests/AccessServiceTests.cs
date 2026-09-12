using Microsoft.EntityFrameworkCore;
using Rooby.Api.Auth;
using Rooby.Api.Data;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Tests;

public sealed class AccessServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Effective_access_composes_all_scopes_and_ignores_revoked_rows()
    {
        using var db = fixture.CreateContext();
        var access = new AccessService(db);

        var user = await CreateUserAsync(db);
        var project = await CreateProjectAsync(db);
        var profile1 = await CreateProfileAsync(db, project.Id);
        var profile2 = await CreateProfileAsync(db, project.Id);

        await access.GrantAsync(user.Id, null, null, AccessLevel.Read, grantedByUserId: 0);
        await access.GrantAsync(user.Id, project.Id, null, AccessLevel.Edit, grantedByUserId: 0);
        await access.GrantAsync(user.Id, project.Id, profile1.Id, AccessLevel.Publish, grantedByUserId: 0);

        var revoked = await access.GrantAsync(user.Id, project.Id, profile1.Id, AccessLevel.ManageSchema, grantedByUserId: 0);
        await access.RevokeAsync(revoked.Id, revokedByUserId: 0);

        var systemWide = await access.GetEffectiveAccessAsync(user.Id, null, null);
        Assert.Equal(AccessLevel.Read, systemWide);

        var onProfile1 = await access.GetEffectiveAccessAsync(user.Id, project.Id, profile1.Id);
        Assert.Equal(AccessLevel.Read | AccessLevel.Edit | AccessLevel.Publish, onProfile1);

        var onProfile2 = await access.GetEffectiveAccessAsync(user.Id, project.Id, profile2.Id);
        Assert.Equal(AccessLevel.Read | AccessLevel.Edit, onProfile2);
    }

    [Fact]
    public async Task Profile_scoped_grant_does_not_reach_another_profile_but_project_scoped_grant_reaches_all()
    {
        using var db = fixture.CreateContext();
        var access = new AccessService(db);

        var project = await CreateProjectAsync(db);
        var profile1 = await CreateProfileAsync(db, project.Id);
        var profile2 = await CreateProfileAsync(db, project.Id);

        var profileUser = await CreateUserAsync(db);
        await access.GrantAsync(profileUser.Id, project.Id, profile1.Id, AccessLevel.Read, grantedByUserId: 0);

        var projectUser = await CreateUserAsync(db);
        await access.GrantAsync(projectUser.Id, project.Id, null, AccessLevel.Read, grantedByUserId: 0);

        Assert.True((await access.GetEffectiveAccessAsync(profileUser.Id, project.Id, profile1.Id)).HasFlag(AccessLevel.Read));
        Assert.False((await access.GetEffectiveAccessAsync(profileUser.Id, project.Id, profile2.Id)).HasFlag(AccessLevel.Read));

        Assert.True((await access.GetEffectiveAccessAsync(projectUser.Id, project.Id, profile1.Id)).HasFlag(AccessLevel.Read));
        Assert.True((await access.GetEffectiveAccessAsync(projectUser.Id, project.Id, profile2.Id)).HasFlag(AccessLevel.Read));
    }

    [Fact]
    public async Task Revoke_sets_is_revoked_and_revoked_userlog_without_deleting_the_row()
    {
        using var db = fixture.CreateContext();
        var access = new AccessService(db);
        var user = await CreateUserAsync(db);

        var granted = await access.GrantAsync(user.Id, null, null, AccessLevel.Read, grantedByUserId: 0);
        await access.RevokeAsync(granted.Id, revokedByUserId: 0);

        var reloaded = await db.UserAccesses.AsNoTracking().SingleAsync(a => a.Id == granted.Id);
        Assert.True(reloaded.IsRevoked);
        Assert.NotNull(reloaded.Revoked);
        Assert.Equal(1, await db.UserAccesses.CountAsync(a => a.Id == granted.Id));
    }

    private static async Task<User> CreateUserAsync(RoobyDbContext db)
    {
        var user = new User { LoginName = $"user-{Guid.NewGuid():N}", DisplayName = "Test User" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Project> CreateProjectAsync(RoobyDbContext db)
    {
        var project = new Project { Id = Guid.CreateVersion7(), Code = $"P{Guid.NewGuid():N}"[..15], Name = "Test" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    private static async Task<Profile> CreateProfileAsync(RoobyDbContext db, Guid projectId)
    {
        var profile = new Profile { Id = Guid.CreateVersion7(), ProjectId = projectId, Code = $"C{Guid.NewGuid():N}"[..15], Name = "Test" };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }
}
