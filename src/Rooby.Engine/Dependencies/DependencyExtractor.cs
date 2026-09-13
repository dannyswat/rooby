using System.Text.Json;
using Celly;
using Celly.Ast;
using Rooby.Engine.Cel;
using Rooby.Engine.Content;

namespace Rooby.Engine.Dependencies;

/// <inheritdoc cref="IDependencyExtractor"/>
public sealed class DependencyExtractor : IDependencyExtractor
{
    private const int MaxInlineDepth = 8;

    // Parsing only (no declarations/type checking needed) to walk the AST for `ref.X` accesses.
    private static readonly CelEnv ParseOnlyEnv = CelEnv.Create(new CelEnvSettings());

    public IReadOnlySet<string> Extract(ItemType itemType, JsonElement content, IReadOnlyList<JsonElement> lines)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        WalkItem(itemType, content, lines, keys, depth: 0);
        return keys;
    }

    private static void WalkItem(ItemType itemType, JsonElement content, IReadOnlyList<JsonElement> lines, HashSet<string> keys, int depth)
    {
        if (depth > MaxInlineDepth)
        {
            return;
        }

        switch (itemType)
        {
            case ItemType.SingleValue:
            case ItemType.Basket:
            case ItemType.Lookup:
                // No expressions or refs in these shapes.
                break;

            case ItemType.ExpressionRule:
                WalkExpressionRule(content, keys);
                break;

            case ItemType.DecisionTable:
                WalkDecisionTable(content, lines, keys, depth);
                break;

            case ItemType.DecisionTree:
                var tree = ItemContentSerializer.Deserialize<DecisionTreeContent>(content);
                WalkTreeNode(tree.Root, keys, depth);
                break;

            case ItemType.RuleList:
                WalkRuleListLines(lines, keys, depth);
                break;

            case ItemType.Matrix:
                WalkMatrix(content, lines, keys, depth);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(itemType));
        }
    }

    private static void WalkExpressionRule(JsonElement content, HashSet<string> keys)
    {
        var expressionRule = ItemContentSerializer.Deserialize<ExpressionRuleContent>(content);
        foreach (var binding in expressionRule.Bindings ?? [])
        {
            keys.Add(binding.Lookup);
            foreach (var keyExpression in binding.Keys)
            {
                AddExpressionRefs(keyExpression, keys);
            }
        }

        AddExpressionRefs(expressionRule.Expression, keys);
    }

    private static void WalkDecisionTable(JsonElement content, IReadOnlyList<JsonElement> lines, HashSet<string> keys, int depth)
    {
        var table = ItemContentSerializer.Deserialize<DecisionTableContent>(content);
        foreach (var column in table.Columns)
        {
            if (column.Kind == DecisionTableColumnKind.Condition && !string.IsNullOrEmpty(column.Expression))
            {
                AddExpressionRefs(column.Expression, keys);
            }
        }

        foreach (var lineElement in lines)
        {
            var row = ItemContentSerializer.Deserialize<DecisionTableRow>(lineElement);
            foreach (var cellText in row.Cells?.Values ?? [])
            {
                var compiled = DecisionTableCellCompiler.Compile(cellText);
                if (compiled is not null)
                {
                    AddExpressionRefs(compiled, keys);
                }
            }

            foreach (var slot in row.Output.Values)
            {
                WalkSlot(slot, keys, depth);
            }
        }
    }

    private static void WalkRuleListLines(IReadOnlyList<JsonElement> lines, HashSet<string> keys, int depth)
    {
        foreach (var lineElement in lines)
        {
            var step = ItemContentSerializer.Deserialize<RuleListStep>(lineElement);
            if (!string.IsNullOrEmpty(step.When))
            {
                AddExpressionRefs(step.When, keys);
            }

            switch (step)
            {
                case RuleListReferenceStep reference:
                    keys.Add(reference.Ref);
                    break;
                case RuleListBindStep bind:
                    keys.Add(bind.Lookup);
                    foreach (var keyExpression in bind.Keys)
                    {
                        AddExpressionRefs(keyExpression, keys);
                    }

                    break;
                case RuleListInlineStep inline:
                    WalkInlineRule(inline.Rule, keys, depth);
                    break;
            }
        }
    }

    private static void WalkMatrix(JsonElement content, IReadOnlyList<JsonElement> lines, HashSet<string> keys, int depth)
    {
        var matrix = ItemContentSerializer.Deserialize<MatrixContent>(content);
        if (!string.IsNullOrEmpty(matrix.Rows.Probe))
        {
            AddExpressionRefs(matrix.Rows.Probe, keys);
        }

        if (!string.IsNullOrEmpty(matrix.Cols.Probe))
        {
            AddExpressionRefs(matrix.Cols.Probe, keys);
        }

        foreach (var lineElement in lines)
        {
            var row = ItemContentSerializer.Deserialize<MatrixRow>(lineElement);
            foreach (var slot in row.Cells.Values)
            {
                WalkSlot(slot, keys, depth);
            }
        }
    }

    private static void WalkSlot(ValueSlot slot, HashSet<string> keys, int depth)
    {
        switch (slot.Kind)
        {
            case ValueSlotKind.Expression:
                AddExpressionRefs(slot.Expression!, keys);
                break;
            case ValueSlotKind.Ref:
                keys.Add(slot.RefKey!);
                foreach (var keyExpression in slot.RefKeys ?? [])
                {
                    AddExpressionRefs(keyExpression, keys);
                }

                break;
            case ValueSlotKind.InlineRule:
                WalkInlineRule(slot.Rule!, keys, depth);
                break;
            case ValueSlotKind.Literal:
            default:
                break;
        }
    }

    private static void WalkInlineRule(InlineRuleSpec rule, HashSet<string> keys, int depth) =>
        WalkItem(rule.ItemType, rule.Content, rule.Lines ?? [], keys, depth + 1);

    private static void WalkTreeNode(DecisionTreeNode node, HashSet<string> keys, int depth)
    {
        switch (node)
        {
            case DecisionTreeIfNode ifNode:
                AddExpressionRefs(ifNode.If, keys);
                WalkTreeNode(ifNode.Then, keys, depth);
                WalkTreeNode(ifNode.Else, keys, depth);
                break;
            case DecisionTreeSwitchNode switchNode:
                AddExpressionRefs(switchNode.Switch, keys);
                foreach (var caseNode in switchNode.Cases.Values)
                {
                    WalkTreeNode(caseNode, keys, depth);
                }

                if (switchNode.Default is not null)
                {
                    WalkTreeNode(switchNode.Default, keys, depth);
                }

                break;
            case DecisionTreeLeafNode leaf:
                WalkSlot(leaf.Result, keys, depth);
                break;
        }
    }

    private static void AddExpressionRefs(string expression, HashSet<string> keys)
    {
        var parsed = ParseOnlyEnv.Parse(expression);
        if (parsed.HasErrors || parsed.Ast is null)
        {
            // Malformed expressions are reported by save-time validation, not dependency extraction.
            return;
        }

        foreach (var node in AstTools.DescendantsAndSelf(parsed.Ast.Expr))
        {
            if (node is SelectExpr { Operand: IdentExpr { Name: "ref" } } select)
            {
                keys.Add(select.Field);
            }
        }
    }
}
