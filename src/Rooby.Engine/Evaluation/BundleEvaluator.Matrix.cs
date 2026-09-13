using System.Globalization;
using Rooby.Engine.Content;

namespace Rooby.Engine.Evaluation;

/// <summary>Matrix evaluation (SPEC §4.8): exact/range axis matching, probes, explicit keys, sub-rule cells.</summary>
public sealed partial class BundleEvaluator
{
    /// <summary>Queries a Matrix with explicit row/col keys (either may be null to fall back to that axis's probe against <paramref name="input"/>).</summary>
    public object? MatrixValue(string itemKey, object? row, object? col, object? input = null, DateTimeOffset? now = null)
    {
        var context = EvaluationContext.Create(input, now ?? DateTimeOffset.UtcNow, TimeZone);
        return MatrixValueCore(GetItem(itemKey), row, col, context);
    }

    internal object? MatrixValueByKeys(string itemKey, object? row, object? col, EvaluationContext context) =>
        MatrixValueCore(GetItem(itemKey), row, col, context);

    /// <summary>A matrix with both axes probed is directly evaluable as a rule (SPEC §4.8).</summary>
    private object? EvaluateProbedMatrix(BundleItem item, EvaluationContext context) =>
        MatrixValueCore(item, row: null, col: null, context);

    private object? MatrixValueCore(BundleItem item, object? row, object? col, EvaluationContext context)
    {
        var itemKey = item.Key;
        var content = ItemContentSerializer.Deserialize<MatrixContent>(item.Content);

        var rowKey = row ?? (content.Rows.Probe is { Length: > 0 } rowProbe ? RunExpression(rowProbe, context, itemKey) : null);
        var colKey = col ?? (content.Cols.Probe is { Length: > 0 } colProbe ? RunExpression(colProbe, context, itemKey) : null);
        if (rowKey is null)
        {
            throw new RoobyEvaluationException(itemKey, "rows", "matrix row key is required (no explicit key and no probe)");
        }

        if (colKey is null)
        {
            throw new RoobyEvaluationException(itemKey, "cols", "matrix column key is required (no explicit key and no probe)");
        }

        var rowLine = FindMatrixRow(item, content.Rows, rowKey, context);
        if (rowLine is null)
        {
            return content.Default is { } noRowDefault ? JsonNativeConverter.ToNative(noRowDefault) : null;
        }

        var rowContent = ItemContentSerializer.Deserialize<MatrixRow>(rowLine.Content);
        var colKeyText = ResolveColumnCellKey(content.Cols, colKey);
        if (colKeyText is null || !rowContent.Cells.TryGetValue(colKeyText, out var slot))
        {
            return content.Default is { } noCellDefault ? JsonNativeConverter.ToNative(noCellDefault) : null;
        }

        return ValueSlotEvaluator.Evaluate(slot, content.Cell.Type, itemKey, $"line:{rowLine.Id}/cell:{colKeyText}", context, this);
    }

    private static BundleLine? FindMatrixRow(BundleItem item, MatrixAxis rowsAxis, object rowKey, EvaluationContext context)
    {
        foreach (var line in item.Lines)
        {
            if (!IsLineActive(line.Validity, context))
            {
                continue;
            }

            var row = ItemContentSerializer.Deserialize<MatrixRow>(line.Content);
            if (HeaderMatches(rowsAxis.Match, row.Header, rowKey))
            {
                return line;
            }
        }

        return null;
    }

    private static bool HeaderMatches(MatrixAxisMatch match, MatrixHeader header, object? key)
    {
        if (match == MatrixAxisMatch.Exact)
        {
            return header.Key is { } exactKey && KeyText(JsonNativeConverter.ToNative(exactKey)) == KeyText(key);
        }

        var from = header.From is { } fromEl ? JsonNativeConverter.ToNative(fromEl) : null;
        var to = header.To is { } toEl ? JsonNativeConverter.ToNative(toEl) : null;
        if (from is not null && CompareRangeValue(key, from) < 0)
        {
            return false;
        }

        if (to is not null && CompareRangeValue(key, to) >= 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves which <see cref="MatrixRow.Cells"/> key a matched column corresponds to. Only <c>exact</c>
    /// column axes have a natural key text (the header's own <c>key</c>, matching the authored cell
    /// dictionary key verbatim per SPEC §4.8's example); a <c>range</c> column axis has no such label in
    /// the schema, so as a documented fallback (untested by SPEC's examples) the matched header's index
    /// is used as the cell key text.
    /// </summary>
    private static string? ResolveColumnCellKey(MatrixAxis colsAxis, object? colKey)
    {
        if (colsAxis.Headers is not { Count: > 0 } headers)
        {
            return KeyText(colKey);
        }

        if (colsAxis.Match == MatrixAxisMatch.Exact)
        {
            foreach (var header in headers)
            {
                if (header.Key is { } key && KeyText(JsonNativeConverter.ToNative(key)) == KeyText(colKey))
                {
                    return KeyText(JsonNativeConverter.ToNative(key));
                }
            }

            return null;
        }

        for (var i = 0; i < headers.Count; i++)
        {
            if (HeaderMatches(MatrixAxisMatch.Range, headers[i], colKey))
            {
                return i.ToString(CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    /// <summary>
    /// Builds the <c>ref.X</c> entry for a Matrix (SPEC §5.2): an exact×exact matrix becomes a nested
    /// map <c>[row][col]</c>; a matrix with both axes probed is itself "any rule" and is evaluated
    /// against the ambient <c>input</c>. Any other axis combination has no defined <c>ref.X</c> shape
    /// (publish validation, §8.2, rejects that usage) — treated here as null rather than throwing.
    /// </summary>
    private object? BuildMatrixRefEntry(BundleItem item, EvaluationContext context)
    {
        var content = ItemContentSerializer.Deserialize<MatrixContent>(item.Content);
        if (content.Rows.Match == MatrixAxisMatch.Exact && content.Cols.Match == MatrixAxisMatch.Exact)
        {
            return BuildExactMatrixRefMap(item, context);
        }

        if (!string.IsNullOrEmpty(content.Rows.Probe) && !string.IsNullOrEmpty(content.Cols.Probe))
        {
            return MatrixValueCore(item, row: null, col: null, context);
        }

        return null;
    }

    /// <summary>SPEC §5.2: an exact×exact Matrix exposed as a nested map <c>ref.X[row][col]</c>.</summary>
    private Dictionary<string, object?> BuildExactMatrixRefMap(BundleItem item, EvaluationContext context)
    {
        var content = ItemContentSerializer.Deserialize<MatrixContent>(item.Content);
        var root = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (content.Rows.Match != MatrixAxisMatch.Exact || content.Cols.Match != MatrixAxisMatch.Exact)
        {
            // ref.X[row][col] is only defined for exact×exact axes (§4.8); publish validation (§8.2)
            // rejects this usage on other matrices, so an empty map here is never actually read.
            return root;
        }

        foreach (var line in item.Lines)
        {
            if (!IsLineActive(line.Validity, context))
            {
                continue;
            }

            var row = ItemContentSerializer.Deserialize<MatrixRow>(line.Content);
            var rowKeyText = row.Header.Key is { } key ? KeyText(JsonNativeConverter.ToNative(key)) : string.Empty;
            var cols = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (colKey, slot) in row.Cells)
            {
                cols[colKey] = ValueSlotEvaluator.Evaluate(slot, content.Cell.Type, item.Key, $"line:{line.Id}/cell:{colKey}", context, this);
            }

            root[rowKeyText] = cols;
        }

        return root;
    }
}
