using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Rooby.Api.Data.Entities;

namespace Rooby.Api.Auth;

public sealed record LocalLoginRequest(string LoginName, string Password);

/// <summary>Dev-only login endpoints; only mapped when Authentication:Local:Enabled and non-Production (SPEC §14).</summary>
public static class LocalAuthEndpoints
{
    public static void MapLocalAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/local-login", (LocalLoginRequest request, IOptions<LocalAuthOptions> options, HttpContext http) =>
        {
            var opts = options.Value;
            if (!opts.Enabled || !IsMatch(request.LoginName, opts.LoginName) || !IsMatch(request.Password, opts.Password))
            {
                return Results.Unauthorized();
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(RoobyClaimTypes.LoginName, opts.LoginName),
                    new Claim(RoobyClaimTypes.Provider, nameof(LoginProvider.Local)),
                    new Claim(ClaimTypes.Name, opts.DisplayName),
                ],
                CookieAuthenticationDefaults.AuthenticationScheme);

            return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: CookieAuthenticationDefaults.AuthenticationScheme);
        });

        app.MapPost("/api/auth/logout", () =>
            Results.SignOut(authenticationSchemes: [CookieAuthenticationDefaults.AuthenticationScheme]));
    }

    private static bool IsMatch(string actual, string expected)
    {
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return actualBytes.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
