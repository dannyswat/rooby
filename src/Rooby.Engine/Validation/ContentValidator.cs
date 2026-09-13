using System.Text.Json;
using Celly.Checking;
using Celly.Types;
using Rooby.Engine.Cel;
using Rooby.Engine.Content;
using Rooby.Engine.Schema;

namespace Rooby.Engine.Validation;

/// <inheritdoc cref="IContentValidator"/>
public sealed class ContentValidator(CelCompiler compiler) : IContentValidator
{
    private const int MaxInlineDepth = 8;
    private const int MaxTreeDepth = 32;

    private static readonly IReadOnlyList<VariableDecl> CellDeclarations = [new VariableDecl(DecisionTableCellCompiler.CellIdentifier, CelType.Dyn)];

    public IReadOnlyList<ValidationProblem> Validate(
        ValidatedItem item,
        IReadOnlyList<ValidatedLine> lines,
        RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode)
    {
        var problems = new List<ValidationProblem>();
        try
        {
            ValidateTypedContent(item.ItemType, item.Content, lines, item.DataType, "content", schema, schemaCacheKey, mode, depth: 0, problems);
        }
        catch (JsonException ex)
        {
            return [new ValidationProblem("content", "content_shape", ex.Message)];
        }

        return problems;
    }

    private void ValidateTypedContent(
        ItemType itemType,
        JsonElement content,
        IReadOnlyList<ValidatedLine> lines,
        DataType dataType,
        string path,
        RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode,
        int depth,
        List<ValidationProblem> problems)
    {
        switch (itemType)
        {
            case ItemType.SingleValue:
                ValidateSingleValue(ItemContentSerializer.Deserialize<SingleValueContent>(content), dataType, path, problems);
                break;
            case ItemType.Lookup:
                ValidateLookup(ItemContentSerializer.Deserialize<LookupContent>(content), lines, path, problems);
                break;
            case ItemType.Basket:
                ValidateBasket(ItemContentSerializer.Deserialize<BasketContent>(content), lines, problems);
                break;
            case ItemType.ExpressionRule:
                ValidateExpressionRule(ItemContentSerializer.Deserialize<ExpressionRuleContent>(content), path, schema, schemaCacheKey, mode, problems);
                break;
            case ItemType.DecisionTable:
                ValidateDecisionTable(ItemContentSerializer.Deserialize<DecisionTableContent>(content), lines, path, schema, schemaCacheKey, mode, depth, problems);
                break;
            case ItemType.DecisionTree:
                ValidateDecisionTree(ItemContentSerializer.Deserialize<DecisionTreeContent>(content), dataType, path, schema, schemaCacheKey, mode, depth, problems);
                break;
            case ItemType.RuleList:
                ValidateRuleList(ItemContentSerializer.Deserialize<RuleListContent>(content), lines, dataType, path, schema, schemaCacheKey, mode, depth, problems);
                break;
            case ItemType.Matrix:
                ValidateMatrix(ItemContentSerializer.Deserialize<MatrixContent>(content), lines, path, schema, schemaCacheKey, mode, depth, problems);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(itemType));
        }
    }

    private static void ValidateSingleValue(SingleValueContent content, DataType dataType, string path, List<ValidationProblem> problems)
    {
        var problem = ValidateLiteralType(content.Value, dataType, $"{path}/value");
        if (problem is not null)
        {
            problems.Add(problem);
        }
    }

    private static void ValidateLookup(LookupContent content, IReadOnlyList<ValidatedLine> lines, string path, List<ValidationProblem> problems)
    {
        if (content.Keys.Count == 0)
        {
            problems.Add(new ValidationProblem($"{path}/keys", "lookup_keys", "at least one key column is required"));
        }

        if (content.Value.Type == DataType.Object && content.Value.Fields is not { Count: > 0 })
        {
            problems.Add(new ValidationProblem($"{path}/value", "lookup_value_fields", "an Object value must declare its fields"));
        }

        if (content.Default is { } defaultValue)
        {
            var problem = ValidateLiteralType(defaultValue, content.Value.Type, $"{path}/default");
            if (problem is not null)
            {
                problems.Add(problem);
            }
        }

        var expectedKeyNames = content.Match == LookupMatch.Range && content.Keys.Count > 0
            ? content.Keys.Take(content.Keys.Count - 1).Select(k => k.Name).Append("from").Append("to").ToHashSet(StringComparer.Ordinal)
            : content.Keys.Select(k => k.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            var row = ItemContentSerializer.Deserialize<LookupLine>(line.Content);
            var linePath = $"line:{line.Id}";
            if (!row.Key.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedKeyNames))
            {
                problems.Add(new ValidationProblem(
                    linePath,
                    "lookup_line_key",
                    $"line key columns must be exactly [{string.Join(", ", expectedKeyNames)}]"));
            }

            var valueProblem = ValidateLiteralType(row.Value, content.Value.Type, $"{linePath}/value");
            if (valueProblem is not null)
            {
                problems.Add(valueProblem);
            }
        }
    }

    private static void ValidateBasket(BasketContent content, IReadOnlyList<ValidatedLine> lines, List<ValidationProblem> problems)
    {
        foreach (var line in lines)
        {
            var row = ItemContentSerializer.Deserialize<BasketLine>(line.Content);
            var problem = ValidateLiteralType(row.Value, content.ElementType, $"line:{line.Id}/value");
            if (problem is not null)
            {
                problems.Add(problem);
            }
        }
    }

    private void ValidateExpressionRule(
        ExpressionRuleContent content,
        string path,
        RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode,
        List<ValidationProblem> problems)
    {
        foreach (var binding in content.Bindings ?? [])
        {
            if (string.IsNullOrWhiteSpace(binding.Name))
            {
                problems.Add(new ValidationProblem($"{path}/bindings", "binding_name", "binding name must not be empty"));
            }

            if (string.IsNullOrWhiteSpace(binding.Lookup))
            {
                problems.Add(new ValidationProblem($"{path}/bindings", "binding_lookup", "binding lookup key must not be empty"));
            }

            foreach (var keyExpression in binding.Keys)
            {
                AddCompileProblems(keyExpression, schema, schemaCacheKey, mode, $"{path}/bindings/{binding.Name}", problems);
            }
        }

        AddCompileProblems(content.Expression, schema, schemaCacheKey, mode, $"{path}/expression", problems);
    }

    private void ValidateDecisionTable(
        DecisionTableContent content,
        IReadOnlyList<ValidatedLine> lines,
        string path,
        RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode,
        int depth,
        List<ValidationProblem> problems)
    {
        if (content.Columns.Count == 0)
        {
            problems.Add(new ValidationProblem(path, "table_columns", "at least one column is required"));
        }

        var conditionColumns = new HashSet<string>(StringComparer.Ordinal);
        var outputColumnTypes = new Dictionary<string, DataType>(StringComparer.Ordinal);
        foreach (var column in content.Columns)
        {
            if (column.Kind == DecisionTableColumnKind.Condition)
            {
                conditionColumns.Add(column.Name);
                if (string.IsNullOrWhiteSpace(column.Expression))
                {
                    problems.Add(new ValidationProblem($"{path}/columns/{column.Name}", "table_column_expression", "condition columns require an expression"));
                }
                else
                {
                    AddCompileProblems(column.Expression, schema, schemaCacheKey, mode, $"{path}/columns/{column.Name}", problems);
                }
            }
            else
            {
                if (column.Type is null)
                {
                    problems.Add(new ValidationProblem($"{path}/columns/{column.Name}", "table_column_type", "output columns require a type"));
                }
                else
                {
                    outputColumnTypes[column.Name] = column.Type.Value;
                }
            }
        }

        foreach (var line in lines)
        {
            var row = ItemContentSerializer.Deserialize<DecisionTableRow>(line.Content);
            var linePath = $"line:{line.Id}";
            foreach (var (columnName, cellText) in row.Cells ?? new Dictionary<string, string>(StringComparer.Ordinal))
            {
                if (!conditionColumns.Contains(columnName))
                {
                    problems.Add(new ValidationProblem(linePath, "table_cell_column", $"cell references unknown condition column '{columnName}'"));
                    continue;
                }

                var compiled = DecisionTableCellCompiler.Compile(cellText);
                if (compiled is not null)
                {
                    AddCompileProblems(compiled, schema, schemaCacheKey + ":cell", mode, $"{linePath}/cell:{columnName}", problems, CellDeclarations);
                }
            }

            foreach (var (columnName, slot) in row.Output)
            {
                if (!outputColumnTypes.TryGetValue(columnName, out var columnType))
                {
                    problems.Add(new ValidationProblem(linePath, "table_output_column", $"output references unknown output column '{columnName}'"));
                    continue;
                }

                ValidateSlot(slot, columnType, $"{linePath}/cell:{columnName}", schema, schemaCacheKey, mode, depth, problems);
            }
        }
    }

    private void ValidateDecisionTree(
        DecisionTreeContent content,
        DataType outputType,
        string path,
        RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode,
        int depth,
        List<ValidationProblem> problems) =>
        ValidateTreeNode(content.Root, outputType, path, schema, schemaCacheKey, mode, depth, treeDepth: 1, problems);

    private void ValidateTreeNode(
        DecisionTreeNode node,
        DataType outputType,
        string path,
        RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode,
        int depth,
        int treeDepth,
        List<ValidationProblem> problems)
    {
        if (treeDepth > MaxTreeDepth)
        {
            problems.Add(new ValidationProblem(path, "tree_depth", $"decision tree depth exceeds the maximum of {MaxTreeDepth}"));
            return;
        }

        switch (node)
        {
            case DecisionTreeIfNode ifNode:
                AddCompileProblems(ifNode.If, schema, schemaCacheKey, mode, $"{path}/if", problems);
                ValidateTreeNode(ifNode.Then, outputType, $"{path}/then", schema, schemaCacheKey, mode, depth, treeDepth + 1, problems);
                ValidateTreeNode(ifNode.Else, outputType, $"{path}/else", schema, schemaCacheKey, mode, depth, treeDepth + 1, problems);
                break;
            case DecisionTreeSwitchNode switchNode:
                AddCompileProblems(switchNode.Switch, schema, schemaCacheKey, mode, $"{path}/switch", problems);
                foreach (var (caseValue, caseNode) in switchNode.Cases)
                {
                    ValidateTreeNode(caseNode, outputType, $"{path}/case:{caseValue}", schema, schemaCacheKey, mode, depth, treeDepth + 1, problems);
                }

                if (switchNode.Default is not null)
                {
                    ValidateTreeNode(switchNode.Default, outputType, $"{path}/default", schema, schemaCacheKey, mode, depth, treeDepth + 1, problems);
                }

                break;
            case DecisionTreeLeafNode leaf:
                ValidateSlot(leaf.Result, outputType, path, schema, schemaCacheKey, mode, depth, problems);
                break;
        }
    }

    private void ValidateRuleList(
        RuleListContent content,
        IReadOnlyList<ValidatedLine> lines,
        DataType outputType,
        string path,
        RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode,
        int depth,
        List<ValidationProblem> problems)
    {
        if (content.Strategy == RuleListStrategy.Aggregate && content.Aggregate is null)
        {
            problems.Add(new ValidationProblem(path, "rule_list_aggregate", "the Aggregate strategy requires an aggregate function"));
        }

        foreach (var line in lines)
        {
            var step = ItemContentSerializer.Deserialize<RuleListStep>(line.Content);
            var linePath = $"line:{line.Id}";
            if (!string.IsNullOrWhiteSpace(step.When))
            {
                AddCompileProblems(step.When, schema, schemaCacheKey, mode, $"{linePath}/when", problems);
            }

            switch (step)
            {
                case RuleListReferenceStep reference:
                    if (string.IsNullOrWhiteSpace(reference.Ref))
                    {
                        problems.Add(new ValidationProblem(linePath, "rule_list_ref", "ref must not be empty"));
                    }

                    break;
                case RuleListBindStep bind:
                    if (string.IsNullOrWhiteSpace(bind.Bind) || string.IsNullOrWhiteSpace(bind.Lookup))
                    {
                        problems.Add(new ValidationProblem(linePath, "rule_list_bind", "bind steps require both 'bind' and 'lookup'"));
                    }

                    foreach (var keyExpression in bind.Keys)
                    {
                        AddCompileProblems(keyExpression, schema, schemaCacheKey, mode, $"{linePath}/keys", problems);
                    }

                    break;
                case RuleListInlineStep inline:
                    ValidateInlineRule(inline.Rule, outputType, linePath, schema, schemaCacheKey, mode, depth, problems);
                    break;
            }
        }
    }

    private void ValidateMatrix(
        MatrixContent content,
        IReadOnlyList<ValidatedLine> lines,
        string path,
        RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode,
        int depth,
        List<ValidationProblem> problems)
    {
        ValidateColumnAxis(content.Cols, $"{path}/cols", problems);

        if (!string.IsNullOrEmpty(content.Rows.Probe))
        {
            AddCompileProblems(content.Rows.Probe, schema, schemaCacheKey, mode, $"{path}/rows/probe", problems);
        }

        if (!string.IsNullOrEmpty(content.Cols.Probe))
        {
            AddCompileProblems(content.Cols.Probe, schema, schemaCacheKey, mode, $"{path}/cols/probe", problems);
        }

        var columnKeys = (content.Cols.Headers ?? []).Select(HeaderKeyText).ToHashSet(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            var row = ItemContentSerializer.Deserialize<MatrixRow>(line.Content);
            var linePath = $"line:{line.Id}";
            ValidateHeader(row.Header, content.Rows.Match, $"{linePath}/header", problems);

            foreach (var (columnKey, slot) in row.Cells)
            {
                if (content.Cols.Headers is { Count: > 0 } && !columnKeys.Contains(columnKey))
                {
                    problems.Add(new ValidationProblem(linePath, "matrix_cell_column", $"cell references unknown column '{columnKey}'"));
                    continue;
                }

                ValidateSlot(slot, content.Cell.Type, $"{linePath}/cell:{columnKey}", schema, schemaCacheKey, mode, depth, problems);
            }
        }
    }

    private static void ValidateColumnAxis(MatrixAxis axis, string path, List<ValidationProblem> problems)
    {
        if (axis.Headers is not { Count: > 0 })
        {
            problems.Add(new ValidationProblem(path, "matrix_axis_headers", "the column axis requires at least one header"));
            return;
        }

        foreach (var header in axis.Headers)
        {
            ValidateHeader(header, axis.Match, $"{path}/headers", problems);
        }

        var headerTexts = axis.Headers.Select(HeaderKeyText).ToList();
        if (headerTexts.Distinct(StringComparer.Ordinal).Count() != headerTexts.Count)
        {
            problems.Add(new ValidationProblem(path, "matrix_axis_headers", "column headers must be unique"));
        }
    }

    private static void ValidateHeader(MatrixHeader header, MatrixAxisMatch match, string path, List<ValidationProblem> problems)
    {
        if (match == MatrixAxisMatch.Exact)
        {
            if (header.Key is null)
            {
                problems.Add(new ValidationProblem(path, "matrix_header_shape", "exact-match axis headers require a 'key'"));
            }
        }
        else if (header.From is null && header.To is null)
        {
            problems.Add(new ValidationProblem(path, "matrix_header_shape", "range-match axis headers require 'from' and/or 'to'"));
        }
    }

    private static string HeaderKeyText(MatrixHeader header) =>
        header.Key is { } key ? KeyText(key) : $"{header.From?.GetRawText()}..{header.To?.GetRawText()}";

    /// <summary>Renders a header key the way it appears as a JSON object property name (e.g. cell column keys).</summary>
    private static string KeyText(JsonElement key) =>
        key.ValueKind == JsonValueKind.String ? key.GetString() ?? string.Empty : key.GetRawText();

    private void ValidateSlot(
        ValueSlot slot,
        DataType expectedType,
        string path,
        RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode,
        int depth,
        List<ValidationProblem> problems)
    {
        switch (slot.Kind)
        {
            case ValueSlotKind.Literal:
                var problem = ValidateLiteralType(slot.Literal, expectedType, path);
                if (problem is not null)
                {
                    problems.Add(problem);
                }

                break;
            case ValueSlotKind.Expression:
                AddCompileProblems(slot.Expression!, schema, schemaCacheKey, mode, path, problems);
                break;
            case ValueSlotKind.Ref:
                if (string.IsNullOrWhiteSpace(slot.RefKey))
                {
                    problems.Add(new ValidationProblem(path, "slot_ref", "ref key must not be empty"));
                }

                foreach (var keyExpression in slot.RefKeys ?? [])
                {
                    AddCompileProblems(keyExpression, schema, schemaCacheKey, mode, path, problems);
                }

                break;
            case ValueSlotKind.InlineRule:
                ValidateInlineRule(slot.Rule!, expectedType, path, schema, schemaCacheKey, mode, depth, problems);
                break;
        }
    }

    private void ValidateInlineRule(
        InlineRuleSpec rule,
        DataType outputType,
        string path,
        RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode,
        int depth,
        List<ValidationProblem> problems)
    {
        if (depth + 1 > MaxInlineDepth)
        {
            problems.Add(new ValidationProblem(path, "inline_nesting", $"inline rule nesting exceeds the maximum depth of {MaxInlineDepth}"));
            return;
        }

        if (rule.ItemType is ItemType.RuleList or ItemType.Lookup or ItemType.Basket or ItemType.SingleValue)
        {
            problems.Add(new ValidationProblem(
                path,
                "inline_item_type",
                $"{rule.ItemType} may not be used as an inline rule; reference it by key instead"));
            return;
        }

        var innerLines = (rule.Lines ?? [])
            .Select((line, index) => new ValidatedLine { Id = $"{path}#{index}", Content = line })
            .ToList();
        ValidateTypedContent(rule.ItemType, rule.Content, innerLines, outputType, path, schema, schemaCacheKey, mode, depth + 1, problems);
    }

    private void AddCompileProblems(
        string expression,
        RoobySchema? schema,
        string schemaCacheKey,
        CelCompileMode mode,
        string path,
        List<ValidationProblem> problems,
        IReadOnlyList<VariableDecl>? extraDeclarations = null)
    {
        var result = mode == CelCompileMode.Checked
            ? compiler.CompileChecked(expression, schema, schemaCacheKey, extraDeclarations)
            : compiler.CompileDynamic(expression, extraDeclarations, extraDeclarations is null ? string.Empty : ":extra");
        if (!result.Success)
        {
            problems.AddRange(result.Errors.Select(error => new ValidationProblem(path, "cel_compile", error)));
        }
    }

    private static ValidationProblem? ValidateLiteralType(JsonElement literal, DataType type, string path)
    {
        var ok = type switch
        {
            DataType.Boolean => literal.ValueKind is JsonValueKind.True or JsonValueKind.False,
            DataType.Number => literal.ValueKind == JsonValueKind.Number,
            DataType.String => literal.ValueKind == JsonValueKind.String,
            DataType.Date => literal.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(literal.GetString(), out _),
            DataType.List => literal.ValueKind == JsonValueKind.Array,
            DataType.Object => literal.ValueKind == JsonValueKind.Object,
            _ => true,
        };
        return ok ? null : new ValidationProblem(path, "slot_type", $"expected a {type} value");
    }
}
