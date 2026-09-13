using System.Text.Json;

namespace Rooby.Engine.Content;

/// <summary>Discriminates the four <see cref="ValueSlot"/> forms (SPEC §4.0).</summary>
public enum ValueSlotKind
{
    Literal,
    Expression,
    Ref,
    InlineRule,
}

/// <summary>
/// A value slot per SPEC §4.0: a JSON literal, a <c>{ "expression" }</c>, a <c>{ "ref"[, "keys"] }</c>
/// sub-rule/probe, or an inline ad-hoc <c>{ "rule" }</c>. Used wherever an item type produces a value
/// from a fixed position (matrix cell, decision table output, decision tree leaf, rule-list step).
/// </summary>
public sealed record ValueSlot
{
    public required ValueSlotKind Kind { get; init; }

    /// <summary>Set when <see cref="Kind"/> is <see cref="ValueSlotKind.Literal"/>.</summary>
    public JsonElement Literal { get; init; }

    /// <summary>Set when <see cref="Kind"/> is <see cref="ValueSlotKind.Expression"/>.</summary>
    public string? Expression { get; init; }

    /// <summary>Set when <see cref="Kind"/> is <see cref="ValueSlotKind.Ref"/>.</summary>
    public string? RefKey { get; init; }

    /// <summary>Optional explicit probe keys, only meaningful alongside <see cref="RefKey"/>.</summary>
    public IReadOnlyList<string>? RefKeys { get; init; }

    /// <summary>Set when <see cref="Kind"/> is <see cref="ValueSlotKind.InlineRule"/>.</summary>
    public InlineRuleSpec? Rule { get; init; }

    public static ValueSlot FromLiteral(JsonElement literal) => new() { Kind = ValueSlotKind.Literal, Literal = literal };

    public static ValueSlot FromExpression(string expression) => new() { Kind = ValueSlotKind.Expression, Expression = expression };

    public static ValueSlot FromRef(string key, IReadOnlyList<string>? keys = null) =>
        new() { Kind = ValueSlotKind.Ref, RefKey = key, RefKeys = keys };

    public static ValueSlot FromRule(InlineRuleSpec rule) => new() { Kind = ValueSlotKind.InlineRule, Rule = rule };
}

/// <summary>
/// The <c>{ "itemType", "content", "lines"? }</c> shape shared by inline ad-hoc sub-rules (SPEC §4.0)
/// and <see cref="Rooby.Engine.Content.RuleListInlineStep"/>. Allowed item types: ExpressionRule,
/// DecisionTree, DecisionTable, Matrix.
/// </summary>
public sealed record InlineRuleSpec
{
    public required ItemType ItemType { get; init; }

    public required JsonElement Content { get; init; }

    public IReadOnlyList<JsonElement>? Lines { get; init; }
}
