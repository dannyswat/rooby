using System.Text.RegularExpressions;

namespace Rooby.Engine.Cel;

/// <summary>
/// Compiles a DecisionTable cell's shorthand grammar (SPEC §4.5) into a full CEL boolean expression
/// that uses <see cref="CellIdentifier"/> as the column value placeholder (bound by the caller at
/// evaluation time). The SPEC's grammar is authored with <c>$</c> as that placeholder; since <c>$</c>
/// is not a valid CEL identifier character, author-facing <c>$</c> occurrences are substituted with
/// <see cref="CellIdentifier"/> before compiling. Grammar: empty/<c>-</c> = any (returns null); a bare
/// value → equality; a leading relational operator (<c>&lt;, &lt;=, &gt;, &gt;=, !=</c>) or
/// <c>in [...]</c>/<c>in ref.X</c> → prefixed with the placeholder; an interval <c>[a, b)</c>
/// (brackets choose inclusive/exclusive bounds) → a range check; otherwise, text already referencing
/// <c>$</c> is a full CEL boolean, substituted and passed through.
/// </summary>
public static partial class DecisionTableCellCompiler
{
    /// <summary>The CEL identifier bound to the cell's column value; declare it as a variable at evaluation/compile time.</summary>
    public const string CellIdentifier = "__cell";

    private static readonly string[] RelationalOperators = ["<=", ">=", "!=", "<", ">"];

    /// <summary>Returns the compiled CEL boolean text, or null when the cell means "any" (empty/<c>-</c>).</summary>
    public static string? Compile(string cellText)
    {
        var text = cellText.Trim();
        if (text.Length == 0 || text == "-")
        {
            return null;
        }

        if (text.Contains('$', StringComparison.Ordinal))
        {
            return text.Replace("$", CellIdentifier, StringComparison.Ordinal);
        }

        foreach (var op in RelationalOperators)
        {
            if (text.StartsWith(op, StringComparison.Ordinal))
            {
                return $"{CellIdentifier} {text}";
            }
        }

        if (text.StartsWith("in ", StringComparison.Ordinal) || text.StartsWith("in[", StringComparison.Ordinal))
        {
            return $"{CellIdentifier} {text}";
        }

        var match = IntervalPattern().Match(text);
        if (match.Success)
        {
            var lowInclusive = match.Groups[1].Value == "[";
            var lowExpression = match.Groups[2].Value;
            var highExpression = match.Groups[3].Value;
            var highInclusive = match.Groups[4].Value == "]";
            var lowOperator = lowInclusive ? ">=" : ">";
            var highOperator = highInclusive ? "<=" : "<";
            return $"{CellIdentifier} {lowOperator} {lowExpression} && {CellIdentifier} {highOperator} {highExpression}";
        }

        return $"{CellIdentifier} == ({text})";
    }

    [GeneratedRegex(@"^([\[\(])\s*(.+?)\s*,\s*(.+?)\s*([\]\)])$")]
    private static partial Regex IntervalPattern();
}
