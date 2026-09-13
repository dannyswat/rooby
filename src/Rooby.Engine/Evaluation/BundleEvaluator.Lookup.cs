using Rooby.Engine.Content;

namespace Rooby.Engine.Evaluation;

/// <summary>Lookup evaluation (SPEC §4.2, §5.2): exact key-tuple match, or range on the last key column.</summary>
public sealed partial class BundleEvaluator
{
    /// <summary>Queries a Lookup item with explicit keys (last key is the range probe when <c>match: range</c>).</summary>
    public object? LookupValue(string itemKey, IReadOnlyList<object?> keys, DateTimeOffset? now = null)
    {
        var context = EvaluationContext.Create(input: null, now ?? DateTimeOffset.UtcNow, TimeZone);
        return LookupValueByKeys(itemKey, keys, context);
    }

    internal object? LookupValueByKeys(string itemKey, IReadOnlyList<object?> keys, EvaluationContext context)
    {
        var item = GetItem(itemKey);
        if (item.ItemType != ItemType.Lookup)
        {
            throw new RoobyEvaluationException(itemKey, null, "not a Lookup item");
        }

        var content = ItemContentSerializer.Deserialize<LookupContent>(item.Content);
        var match = content.Match == LookupMatch.Exact
            ? FindExactLookupLine(item, content, keys, context)
            : FindRangeLookupLine(item, content, keys, context);
        if (match is null)
        {
            return content.Default is { } def ? JsonNativeConverter.ToNative(def) : null;
        }

        var lineContent = ItemContentSerializer.Deserialize<LookupLine>(match.Content);
        return JsonNativeConverter.ToNative(lineContent.Value);
    }

    private static BundleLine? FindExactLookupLine(BundleItem item, LookupContent content, IReadOnlyList<object?> keys, EvaluationContext context)
    {
        var keyTexts = keys.Select(KeyText).ToList();
        foreach (var line in item.Lines)
        {
            if (!IsLineActive(line.Validity, context))
            {
                continue;
            }

            var lineContent = ItemContentSerializer.Deserialize<LookupLine>(line.Content);
            if (MatchesLeadingKeys(lineContent, content.Keys, keyTexts, content.Keys.Count))
            {
                return line;
            }
        }

        return null;
    }

    private static BundleLine? FindRangeLookupLine(BundleItem item, LookupContent content, IReadOnlyList<object?> keys, EvaluationContext context)
    {
        var leadingCount = content.Keys.Count - 1;
        var leadingTexts = keys.Take(leadingCount).Select(KeyText).ToList();
        var rangeKey = keys[leadingCount];
        foreach (var line in item.Lines)
        {
            if (!IsLineActive(line.Validity, context))
            {
                continue;
            }

            var lineContent = ItemContentSerializer.Deserialize<LookupLine>(line.Content);
            if (!MatchesLeadingKeys(lineContent, content.Keys, leadingTexts, leadingCount))
            {
                continue;
            }

            if (!lineContent.Key.TryGetValue("from", out var fromElement))
            {
                continue;
            }

            var from = JsonNativeConverter.ToNative(fromElement);
            object? to = lineContent.Key.TryGetValue("to", out var toElement) ? JsonNativeConverter.ToNative(toElement) : null;
            if (CompareRangeValue(rangeKey, from) >= 0 && (to is null || CompareRangeValue(rangeKey, to) < 0))
            {
                return line;
            }
        }

        return null;
    }

    private static bool MatchesLeadingKeys(LookupLine line, IReadOnlyList<LookupKeyColumn> columns, List<string> keyTexts, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (!line.Key.TryGetValue(columns[i].Name, out var value) || KeyText(JsonNativeConverter.ToNative(value)) != keyTexts[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>SPEC §5.2: an exact-match Lookup exposed as a nested map keyed by stringified key columns.</summary>
    private static Dictionary<string, object?> BuildExactLookupRefMap(BundleItem item, EvaluationContext context)
    {
        var content = ItemContentSerializer.Deserialize<LookupContent>(item.Content);
        var root = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var line in item.Lines)
        {
            if (!IsLineActive(line.Validity, context))
            {
                continue;
            }

            var lineContent = ItemContentSerializer.Deserialize<LookupLine>(line.Content);
            var node = root;
            for (var i = 0; i < content.Keys.Count; i++)
            {
                var keyText = lineContent.Key.TryGetValue(content.Keys[i].Name, out var v) ? KeyText(JsonNativeConverter.ToNative(v)) : string.Empty;
                if (i == content.Keys.Count - 1)
                {
                    node[keyText] = JsonNativeConverter.ToNative(lineContent.Value);
                }
                else
                {
                    if (node.TryGetValue(keyText, out var existing) && existing is Dictionary<string, object?> existingNode)
                    {
                        node = existingNode;
                    }
                    else
                    {
                        var next = new Dictionary<string, object?>(StringComparer.Ordinal);
                        node[keyText] = next;
                        node = next;
                    }
                }
            }
        }

        return root;
    }
}
