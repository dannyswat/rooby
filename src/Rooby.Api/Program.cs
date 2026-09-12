using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Rooby.Api.Auth;
using Rooby.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<RoobyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(new PublishedRowImmutabilityInterceptor()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAccessService, AccessService>();
builder.Services.Configure<LocalAuthOptions>(builder.Configuration.GetSection(LocalAuthOptions.SectionName));

var oidcAuthority = builder.Configuration["Authentication:Oidc:Authority"];
var jwtAuthority = builder.Configuration["Authentication:Jwt:Authority"];

var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "Rooby";
        options.DefaultChallengeScheme = "Rooby";
    })
    .AddPolicyScheme("Rooby", "Cookie or Bearer", policy =>
    {
        policy.ForwardDefaultSelector = ctx =>
            !string.IsNullOrEmpty(jwtAuthority)
            && ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? JwtBearerDefaults.AuthenticationScheme
                : CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "Rooby.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });

if (!string.IsNullOrEmpty(oidcAuthority))
{
    authBuilder.AddOpenIdConnect(options =>
    {
        builder.Configuration.GetSection("Authentication:Oidc").Bind(options);
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Events.OnTicketReceived = ctx =>
        {
            var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
            var loginName = identity.FindFirst("preferred_username")?.Value
                ?? identity.FindFirst(ClaimTypes.Email)?.Value
                ?? identity.Name;
            if (loginName is not null)
            {
                identity.AddClaim(new Claim(RoobyClaimTypes.LoginName, loginName));
            }

            identity.AddClaim(new Claim(RoobyClaimTypes.Provider, nameof(Rooby.Api.Data.Entities.LoginProvider.Oidc)));
            return Task.CompletedTask;
        };
    });
}

if (!string.IsNullOrEmpty(jwtAuthority))
{
    authBuilder.AddJwtBearer(options =>
    {
        builder.Configuration.GetSection("Authentication:Jwt").Bind(options);
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
                var loginName = identity.FindFirst("preferred_username")?.Value ?? identity.FindFirst(ClaimTypes.Email)?.Value;
                if (loginName is not null)
                {
                    identity.AddClaim(new Claim(RoobyClaimTypes.LoginName, loginName));
                }

                identity.AddClaim(new Claim(RoobyClaimTypes.Provider, nameof(Rooby.Api.Data.Entities.LoginProvider.Oidc)));
                return Task.CompletedTask;
            },
        };
    });
}

builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();

app.MapAccessEndpoints();
if (!app.Environment.IsProduction() && builder.Configuration.GetValue("Authentication:Local:Enabled", false))
{
    app.MapLocalAuthEndpoints();
}

app.Run();

// Entry point marker for WebApplicationFactory<Program> in integration tests.
public partial class Program;
