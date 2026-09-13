using Microsoft.AspNetCore.Diagnostics;

namespace Rooby.Api.Versioning;

/// <summary>Maps version-store domain exceptions to RFC 9457 problem responses.</summary>
public sealed class VersioningExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            ConcurrencyConflictException => StatusCodes.Status409Conflict,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => 0,
        };

        if (statusCode == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = statusCode;
        await Results.Problem(statusCode: statusCode, title: exception.Message).ExecuteAsync(httpContext);
        return true;
    }
}
