using Celly;

namespace Rooby.Engine.Cel;

/// <summary>Result of a <see cref="CelCompiler"/> compile: either a ready <see cref="CelProgram"/>, or error messages.</summary>
public sealed record CelCompileResult
{
    public required bool Success { get; init; }

    public CelProgram? Program { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public static CelCompileResult Ok(CelProgram program) => new() { Success = true, Program = program };

    public static CelCompileResult Fail(IReadOnlyList<string> errors) => new() { Success = false, Errors = errors };

    public static CelCompileResult Fail(string error) => Fail([error]);
}
