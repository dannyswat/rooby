namespace Rooby.Api.Validation;

/// <summary>One field-level validation failure; cross-cutting convention: problems carry path + code.</summary>
public sealed record FieldProblem(string Path, string Code, string Detail);

public static class ValidationResults
{
    public static IResult Problem(IEnumerable<FieldProblem> problems) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation failed",
            extensions: new Dictionary<string, object?>
            {
                ["errors"] = problems.Select(p => new { path = p.Path, code = p.Code, detail = p.Detail }),
            });
}
