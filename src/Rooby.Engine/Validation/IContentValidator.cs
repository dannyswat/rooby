using System.Text.Json;
using Rooby.Engine.Cel;

namespace Rooby.Engine.Validation;

/// <summary>One field-level validation failure; mirrors Rooby.Api's {path, code, detail} convention.</summary>
public sealed record ValidationProblem(string Path, string Code, string Detail);

/// <summary>The item metadata + content needed for save-time validation (SPEC §8.1).</summary>
public sealed record ValidatedItem
{
    public required string Key { get; init; }

    public required ItemType ItemType { get; init; }

    public required DataType DataType { get; init; }

    public required JsonElement Content { get; init; }
}

/// <summary>One line's content, identified by a caller-supplied id used only for problem paths.</summary>
public sealed record ValidatedLine
{
    public required string Id { get; init; }

    public required JsonElement Content { get; init; }
}

/// <summary>
/// Validates an item's Content/lines against its ItemType shape, value-slot rules, matrix/decision
/// table structure and CEL compilability (SPEC §4, §4.0, §4.8, §8.1). Does not check cross-item
/// concerns (key uniqueness, dangling refs, cycles) — those are publish-time validation (§8.2).
/// </summary>
public interface IContentValidator
{
    IReadOnlyList<ValidationProblem> Validate(
        ValidatedItem item,
        IReadOnlyList<ValidatedLine> lines,
        Schema.RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode);
}
