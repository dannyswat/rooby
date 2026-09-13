using System.Globalization;
using Rooby.Engine.Cel;
using Rooby.Engine.Content;

namespace Rooby.Engine.Evaluation;

/// <summary>DecisionTable evaluation (SPEC §4.5): all nine hit policies.</summary>
public sealed partial class BundleEvaluator
{
    private object? EvaluateDecisionTable(BundleItem item, EvaluationContext context)
    {
        var itemKey = item.Key;
        var table = ItemContentSerializer.Deserialize<DecisionTableContent>(item.Content);
        var conditionColumns = table.Columns.Where(c => c.Kind == DecisionTableColumnKind.Condition).ToList();
        var columnValues = conditionColumns.ToDictionary(
            c => c.Name,
            c => RunExpression(c.Expression!, context, itemKey),
            StringComparer.Ordinal);

        var matches = new List<(BundleLine Line, DecisionTableRow Row)>();
        foreach (var line in item.Lines.OrderBy(l => l.SortOrder))
        {
            if (!IsLineActive(line.Validity, context))
            {
                continue;
            }

            var row = ItemContentSerializer.Deserialize<DecisionTableRow>(line.Content);
            if (RowMatches(row, columnValues, itemKey, context))
            {
                matches.Add((line, row));
            }
        }

        if (matches.Count == 0)
        {
            return table.Default is { } def ? JsonNativeConverter.ToNative(def) : null;
        }

        return table.HitPolicy switch
        {
            DecisionTableHitPolicy.First => EvaluateRowOutput(matches[0].Row, table, matches[0].Line, itemKey, context),
            DecisionTableHitPolicy.Unique => EvaluateUniqueHit(matches, table, itemKey, context),
            DecisionTableHitPolicy.Priority => EvaluatePriorityHit(matches, table, itemKey, context),
            DecisionTableHitPolicy.Any => EvaluateAnyHit(matches, table, itemKey, context),
            DecisionTableHitPolicy.Collect => matches.Select(m => EvaluateRowOutput(m.Row, table, m.Line, itemKey, context)).ToList(),
            DecisionTableHitPolicy.CollectSum => AggregateOutputs(matches, table, itemKey, context, values => values.Sum()),
            DecisionTableHitPolicy.CollectMin => AggregateOutputs(matches, table, itemKey, context, values => values.Count == 0 ? 0.0 : values.Min()),
            DecisionTableHitPolicy.CollectMax => AggregateOutputs(matches, table, itemKey, context, values => values.Count == 0 ? 0.0 : values.Max()),
            DecisionTableHitPolicy.CollectCount => (double)matches.Count,
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };
    }

    private bool RowMatches(DecisionTableRow row, Dictionary<string, object?> columnValues, string itemKey, EvaluationContext context)
    {
        foreach (var (columnName, columnValue) in columnValues)
        {
            if (row.Cells is null || !row.Cells.TryGetValue(columnName, out var cellText))
            {
                continue; // absent cell = "any" (§4.5 empty/"-")
            }

            var compiled = DecisionTableCellCompiler.Compile(cellText);
            if (compiled is null)
            {
                continue;
            }

            var result = RunExpression(compiled, context, itemKey, DecisionTableCellCompiler.CellIdentifier, columnValue);
            if (result is not true)
            {
                return false;
            }
        }

        return true;
    }

    private object? EvaluateRowOutput(DecisionTableRow row, DecisionTableContent table, BundleLine line, string itemKey, EvaluationContext context)
    {
        var outputColumns = table.Columns.Where(c => c.Kind == DecisionTableColumnKind.Output).ToList();
        if (outputColumns.Count == 1)
        {
            var column = outputColumns[0];
            return row.Output.TryGetValue(column.Name, out var slot)
                ? ValueSlotEvaluator.Evaluate(slot, column.Type ?? DataType.Object, itemKey, $"line:{line.Id}/cell:{column.Name}", context, this)
                : null;
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var column in outputColumns)
        {
            if (row.Output.TryGetValue(column.Name, out var slot))
            {
                result[column.Name] = ValueSlotEvaluator.Evaluate(slot, column.Type ?? DataType.Object, itemKey, $"line:{line.Id}/cell:{column.Name}", context, this);
            }
        }

        return result;
    }

    private object? EvaluateUniqueHit(List<(BundleLine Line, DecisionTableRow Row)> matches, DecisionTableContent table, string itemKey, EvaluationContext context)
    {
        if (matches.Count != 1)
        {
            throw new RoobyEvaluationException(itemKey, null, $"Unique hit policy violated: {matches.Count} rows matched");
        }

        return EvaluateRowOutput(matches[0].Row, table, matches[0].Line, itemKey, context);
    }

    private object? EvaluatePriorityHit(List<(BundleLine Line, DecisionTableRow Row)> matches, DecisionTableContent table, string itemKey, EvaluationContext context) =>
        EvaluateRowOutput(
            matches.OrderByDescending(m => m.Row.Priority ?? 0).First().Row,
            table,
            matches.OrderByDescending(m => m.Row.Priority ?? 0).First().Line,
            itemKey,
            context);

    private object? EvaluateAnyHit(List<(BundleLine Line, DecisionTableRow Row)> matches, DecisionTableContent table, string itemKey, EvaluationContext context)
    {
        var values = matches.Select(m => EvaluateRowOutput(m.Row, table, m.Line, itemKey, context)).ToList();
        var first = values[0];
        if (values.Skip(1).Any(v => !Equals(v, first)))
        {
            throw new RoobyEvaluationException(itemKey, null, "Any hit policy violated: matching rows produced different outputs");
        }

        return first;
    }

    private double AggregateOutputs(
        List<(BundleLine Line, DecisionTableRow Row)> matches,
        DecisionTableContent table,
        string itemKey,
        EvaluationContext context,
        Func<List<double>, double> aggregate)
    {
        var values = matches
            .Select(m => EvaluateRowOutput(m.Row, table, m.Line, itemKey, context))
            .Select(v => Convert.ToDouble(v ?? 0.0, CultureInfo.InvariantCulture))
            .ToList();
        return aggregate(values);
    }
}
