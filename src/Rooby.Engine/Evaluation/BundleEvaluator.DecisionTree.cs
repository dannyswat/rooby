using Rooby.Engine.Content;

namespace Rooby.Engine.Evaluation;

/// <summary>DecisionTree evaluation (SPEC §4.6): if/switch traversal, leaf value slots, depth capped at 32.</summary>
public sealed partial class BundleEvaluator
{
    private object? EvaluateDecisionTree(BundleItem item, EvaluationContext context)
    {
        var content = ItemContentSerializer.Deserialize<DecisionTreeContent>(item.Content);
        return EvaluateTreeNode(content.Root, item.Key, item.DataType, "root", context, depth: 1);
    }

    private object? EvaluateTreeNode(DecisionTreeNode node, string itemKey, DataType outputType, string path, EvaluationContext context, int depth)
    {
        if (depth > 32)
        {
            throw new RoobyEvaluationException(itemKey, path, "decision tree depth exceeds the maximum of 32");
        }

        switch (node)
        {
            case DecisionTreeIfNode ifNode:
                var condition = RunExpression(ifNode.If, context, itemKey);
                return condition is true
                    ? EvaluateTreeNode(ifNode.Then, itemKey, outputType, $"{path}/then", context, depth + 1)
                    : EvaluateTreeNode(ifNode.Else, itemKey, outputType, $"{path}/else", context, depth + 1);

            case DecisionTreeSwitchNode switchNode:
                var switchValue = KeyText(RunExpression(switchNode.Switch, context, itemKey));
                foreach (var (caseText, caseNode) in switchNode.Cases)
                {
                    var caseValue = KeyText(RunExpression(caseText, context, itemKey));
                    if (string.Equals(caseValue, switchValue, StringComparison.Ordinal))
                    {
                        return EvaluateTreeNode(caseNode, itemKey, outputType, $"{path}/case:{caseText}", context, depth + 1);
                    }
                }

                return switchNode.Default is { } defaultNode
                    ? EvaluateTreeNode(defaultNode, itemKey, outputType, $"{path}/default", context, depth + 1)
                    : null;

            case DecisionTreeLeafNode leaf:
                return ValueSlotEvaluator.Evaluate(leaf.Result, outputType, itemKey, path, context, this);

            default:
                throw new ArgumentOutOfRangeException(nameof(node));
        }
    }
}
