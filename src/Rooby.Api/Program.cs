using Microsoft.EntityFrameworkCore;
using Rooby.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<RoobyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(new PublishedRowImmutabilityInterceptor()));

var app = builder.Build();

if (args.Contains("--migrate"))
{
    using var migrationScope = app.Services.CreateScope();
    var db = migrationScope.ServiceProvider.GetRequiredService<RoobyDbContext>();
    await db.Database.MigrateAsync();
    return;
}

if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    var db = seedScope.ServiceProvider.GetRequiredService<RoobyDbContext>();
    await DbInitializer.SeedLocalAdminAsync(db, app.Configuration);

    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();

// Entry point marker for WebApplicationFactory<Program> in integration tests.
public partial class Program;
