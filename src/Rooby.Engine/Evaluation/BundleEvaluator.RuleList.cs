using System.Globalization;
using Rooby.Engine.Content;

namespace Rooby.Engine.Evaluation;

/// <summary>RuleList evaluation (SPEC §4.7): all five strategies, <c>bind</c> steps, connected sub-rules.</summary>
public sealed partial class BundleEvaluator
{
    private object? EvaluateRuleList(BundleItem item, EvaluationContext context)
    {
        var itemKey = item.Key;
        var content = ItemContentSerializer.Deserialize<RuleListContent>(item.Content);
        var runningVars = new Dictionary<string, object?>(context.Vars, StringComparer.Ordinal);
        object? prev = content.Seed is { } seed ? JsonNativeConverter.ToNative(seed) : null;
        var collected = new List<object?>();
        var priorityCandidates = new List<(object? Result, int Priority)>();

        foreach (var line in item.Lines.OrderBy(l => l.SortOrder))
        {
            if (!IsLineActive(line.Validity, context))
            {
                continue;
            }

            var step = ItemContentSerializer.Deserialize<RuleListStep>(line.Content);
            if (!step.Enabled)
            {
                continue;
            }

            var stepContext = context with { Vars = runningVars, Prev = prev };
            var whenMatches = string.IsNullOrEmpty(step.When) || RunExpression(step.When, stepContext, itemKey) is true;

            if (step is RuleListBindStep bind)
            {
                if (whenMatches)
                {
                    var keys = bind.Keys.Select(k => RunExpression(k, stepContext, itemKey)).ToList();
                    runningVars[bind.Bind] = LookupValueByKeys(bind.Lookup, keys, stepContext);
                }

                continue;
            }

            if (!whenMatches)
            {
                continue; // Chain: non-matching steps pass `prev` through unchanged (already untouched).
            }

            var stepResult = step switch
            {
                RuleListReferenceStep reference => EvaluateForReference(reference.Ref, stepContext),
                RuleListInlineStep inline => EvaluateInlineStepResult(inline.Rule, item.DataType, itemKey, $"line:{line.Id}", stepContext),
                _ => throw new ArgumentOutOfRangeException(nameof(item)),
            };

            switch (content.Strategy)
            {
                case RuleListStrategy.FirstMatch when stepResult is not null:
                    return stepResult;
                case RuleListStrategy.All or RuleListStrategy.Aggregate when stepResult is not null:
                    collected.Add(stepResult);
                    break;
                case RuleListStrategy.Chain:
                    prev = stepResult;
                    break;
                case RuleListStrategy.Priority when stepResult is not null:
                    priorityCandidates.Add((stepResult, step.Priority ?? 0));
                    break;
            }
        }

        return content.Strategy switch
        {
            RuleListStrategy.FirstMatch => null,
            RuleListStrategy.All => collected,
            RuleListStrategy.Chain => prev,
            RuleListStrategy.Priority => priorityCandidates.Count == 0 ? null : priorityCandidates.OrderByDescending(c => c.Priority).First().Result,
            RuleListStrategy.Aggregate => AggregateRuleListResults(content.Aggregate, collected, itemKey),
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };
    }

    private object? EvaluateInlineStepResult(InlineRuleSpec rule, DataType expectedType, string itemKey, string path, EvaluationContext context)
    {
        if (context.InlineDepth >= 8)
        {
            throw new RoobyEvaluationException(itemKey, path, "inline rule nesting exceeds the maximum depth of 8");
        }

        var inlineContext = context with { InlineDepth = context.InlineDepth + 1 };
        return EvaluateInlineContent(rule.ItemType, rule.Content, rule.Lines ?? [], expectedType, itemKey, path, inlineContext);
    }

    private static double AggregateRuleListResults(AggregateFunction? function, List<object?> results, string itemKey)
    {
        if (function is null)
        {
            throw new RoobyEvaluationException(itemKey, null, "the Aggregate strategy requires an aggregate function");
        }

        var numbers = results.Select(r => Convert.ToDouble(r ?? 0.0, CultureInfo.InvariantCulture)).ToList();
        return function switch
        {
            AggregateFunction.Sum => numbers.Sum(),
            AggregateFunction.Min => numbers.Count == 0 ? 0.0 : numbers.Min(),
            AggregateFunction.Max => numbers.Count == 0 ? 0.0 : numbers.Max(),
            AggregateFunction.Count => (double)results.Count,
            AggregateFunction.Product => numbers.Aggregate(1.0, (a, b) => a * b),
            _ => throw new ArgumentOutOfRangeException(nameof(function)),
        };
    }
}
