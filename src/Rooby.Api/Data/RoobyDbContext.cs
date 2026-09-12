using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data.Entities;
using Version = Rooby.Api.Data.Entities.Version;

namespace Rooby.Api.Data;

public sealed class RoobyDbContext(DbContextOptions<RoobyDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<User> Users => Set<User>();

    public DbSet<UserAccess> UserAccesses => Set<UserAccess>();

    public DbSet<Version> Versions => Set<Version>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<ItemLine> ItemLines => Set<ItemLine>();

    public DbSet<Schema> Schemas => Set<Schema>();

    public DbSet<VersionSchema> VersionSchemas => Set<VersionSchema>();

    public DbSet<TestCase> TestCases => Set<TestCase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("btree_gist");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoobyDbContext).Assembly);
    }
}
