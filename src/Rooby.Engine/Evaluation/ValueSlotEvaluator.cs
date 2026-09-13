using Rooby.Engine.Content;

namespace Rooby.Engine.Evaluation;

/// <summary>
/// Evaluates a <see cref="ValueSlot"/> (SPEC §4.0): literal, expression, ref (+ optional explicit
/// probe keys), or an inline ad-hoc sub rule (depth-capped at 8, coerced to the slot's declared type).
/// </summary>
public static class ValueSlotEvaluator
{
    public static object? Evaluate(ValueSlot slot, DataType expectedType, string itemKey, string slotPath, EvaluationContext context, BundleEvaluator bundle)
    {
        object? raw = slot.Kind switch
        {
            ValueSlotKind.Literal => JsonNativeConverter.ToNative(slot.Literal),
            ValueSlotKind.Expression => bundle.RunExpression(slot.Expression!, context, itemKey),
            ValueSlotKind.Ref => EvaluateRef(slot, itemKey, slotPath, context, bundle),
            ValueSlotKind.InlineRule => EvaluateInlineRule(slot.Rule!, expectedType, itemKey, slotPath, context, bundle),
            _ => throw new ArgumentOutOfRangeException(nameof(slot)),
        };
        return OutputCoercion.Coerce(raw, expectedType, itemKey, slotPath);
    }

    private static object? EvaluateRef(ValueSlot slot, string itemKey, string slotPath, EvaluationContext context, BundleEvaluator bundle)
    {
        var refKey = slot.RefKey!;
        if (slot.RefKeys is { Count: > 0 } explicitKeyExpressions)
        {
            var keys = explicitKeyExpressions.Select(expr => bundle.RunExpression(expr, context, itemKey)).ToList();
            var subContext = context.ForReferencedItem();
            return bundle.GetItemType(refKey) switch
            {
                ItemType.Lookup => bundle.LookupValueByKeys(refKey, keys, subContext),
                ItemType.Matrix => bundle.MatrixValueByKeys(refKey, keys.ElementAtOrDefault(0), keys.ElementAtOrDefault(1), subContext),
                var other => throw new RoobyEvaluationException(itemKey, slotPath, $"'{refKey}' ({other}) does not support explicit probe keys"),
            };
        }

        return bundle.EvaluateForReference(refKey, context);
    }

    private static object? EvaluateInlineRule(InlineRuleSpec rule, DataType expectedType, string itemKey, string slotPath, EvaluationContext context, BundleEvaluator bundle)
    {
        if (context.InlineDepth >= 8)
        {
            throw new RoobyEvaluationException(itemKey, slotPath, "inline rule nesting exceeds the maximum depth of 8");
        }

        var inlineContext = context with { InlineDepth = context.InlineDepth + 1 };
        return bundle.EvaluateInlineContent(rule.ItemType, rule.Content, rule.Lines ?? [], expectedType, itemKey, slotPath, inlineContext);
    }
}
