using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data;
using Testcontainers.PostgreSql;

namespace Rooby.Api.Tests;

/// <summary>One PostgreSQL container per test class (per project cross-cutting convention).</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public RoobyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RoobyDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new PublishedRowImmutabilityInterceptor())
            .Options;
        return new RoobyDbContext(options);
    }
}
