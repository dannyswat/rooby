using System.Text.Json;
using Rooby.Engine.Cel;
using Rooby.Engine.Content;
using Rooby.Engine.Validation;

namespace Rooby.Engine.Tests.Validation;

public sealed class ContentValidatorTests
{
    private readonly ContentValidator _validator = new(new CelCompiler());

    [Fact]
    public void SingleValue_passes_when_value_matches_data_type()
    {
        var item = Item(ItemType.SingleValue, DataType.Number, Json("""{"$v":1,"value":5000000}"""));
        Assert.Empty(_validator.Validate(item, [], null, "k", CelCompileMode.Dynamic));
    }

    [Fact]
    public void SingleValue_fails_when_value_does_not_match_data_type()
    {
        var item = Item(ItemType.SingleValue, DataType.Number, Json("""{"$v":1,"value":"not a number"}"""));
        var problems = _validator.Validate(item, [], null, "k", CelCompileMode.Dynamic);
        Assert.Contains(problems, p => p.Code == "slot_type");
    }

    [Fact]
    public void Lookup_passes_with_matching_keys_and_value()
    {
        var content = Json("""
            {"$v":1,"keys":[{"name":"productType","type":"String"}],"match":"exact","value":{"type":"Number"}}
            """);
        var line = Line("l1", """{"$v":1,"key":{"productType":"ELN"},"value":35}""");
        var item = Item(ItemType.Lookup, DataType.Number, content);

        Assert.Empty(_validator.Validate(item, [line], null, "k", CelCompileMode.Dynamic));
    }

    [Fact]
    public void Lookup_fails_when_object_value_has_no_declared_fields()
    {
        var content = Json("""{"$v":1,"keys":[{"name":"k","type":"String"}],"match":"exact","value":{"type":"Object"}}""");
        var item = Item(ItemType.Lookup, DataType.Object, content);

        var problems = _validator.Validate(item, [], null, "k", CelCompileMode.Dynamic);
        Assert.Contains(problems, p => p.Code == "lookup_value_fields");
    }

    [Fact]
    public void Lookup_fails_when_line_key_columns_do_not_match()
    {
        var content = Json("""{"$v":1,"keys":[{"name":"productType","type":"String"}],"match":"exact","value":{"type":"Number"}}""");
        var line = Line("l1", """{"$v":1,"key":{"wrongColumn":"ELN"},"value":35}""");
        var item = Item(ItemType.Lookup, DataType.Number, content);

        var problems = _validator.Validate(item, [line], null, "k", CelCompileMode.Dynamic);
        Assert.Contains(problems, p => p.Code == "lookup_line_key");
    }

    [Fact]
    public void Basket_fails_when_element_type_mismatches()
    {
        var content = Json("""{"$v":1,"elementType":"String","unique":true}""");
        var line = Line("l1", """{"$v":1,"value":123}""");
        var item = Item(ItemType.Basket, DataType.List, content);

        var problems = _validator.Validate(item, [line], null, "k", CelCompileMode.Dynamic);
        Assert.Contains(problems, p => p.Code == "slot_type");
    }

    [Fact]
    public void ExpressionRule_fails_on_malformed_cel_expression()
    {
        var content = Json("""{"$v":1,"expression":"input.x +"}""");
        var item = Item(ItemType.ExpressionRule, DataType.Number, content);

        var problems = _validator.Validate(item, [], null, "k", CelCompileMode.Dynamic);
        Assert.Contains(problems, p => p.Code == "cel_compile");
    }

    [Fact]
    public void ExpressionRule_passes_with_valid_expression_and_binding()
    {
        var content = Json("""
            {"$v":1,"bindings":[{"name":"s","lookup":"Spread","keys":["input.x"]}],"expression":"vars.s + 1"}
            """);
        var item = Item(ItemType.ExpressionRule, DataType.Number, content);

        Assert.Empty(_validator.Validate(item, [], null, "k", CelCompileMode.Dynamic));
    }

    [Fact]
    public void DecisionTable_fails_when_cell_references_unknown_column()
    {
        var content = Json("""
            {"$v":1,"columns":[
              {"name":"tier","kind":"condition","expression":"input.client.tier"},
              {"name":"markup","kind":"output","type":"Number"}]}
            """);
        var line = Line("l1", """{"$v":1,"cells":{"unknownColumn":"'Gold'"},"output":{"markup":10}}""");
        var item = Item(ItemType.DecisionTable, DataType.Number, content);

        var problems = _validator.Validate(item, [line], null, "k", CelCompileMode.Dynamic);
        Assert.Contains(problems, p => p.Code == "table_cell_column");
    }

    [Fact]
    public void DecisionTable_passes_with_valid_row()
    {
        var content = Json("""
            {"$v":1,"columns":[
              {"name":"tier","kind":"condition","expression":"input.client.tier"},
              {"name":"markup","kind":"output","type":"Number"}]}
            """);
        var line = Line("l1", """{"$v":1,"cells":{"tier":"'Gold'"},"output":{"markup":10}}""");
        var item = Item(ItemType.DecisionTable, DataType.Number, content);

        Assert.Empty(_validator.Validate(item, [line], null, "k", CelCompileMode.Dynamic));
    }

    [Fact]
    public void RuleList_fails_when_aggregate_strategy_has_no_function()
    {
        var content = Json("""{"$v":1,"strategy":"Aggregate","output":{"type":"Number"}}""");
        var item = Item(ItemType.RuleList, DataType.Number, content);

        var problems = _validator.Validate(item, [], null, "k", CelCompileMode.Dynamic);
        Assert.Contains(problems, p => p.Code == "rule_list_aggregate");
    }

    [Fact]
    public void RuleList_fails_when_bind_step_missing_lookup()
    {
        var content = Json("""{"$v":1,"strategy":"FirstMatch","output":{"type":"Number"}}""");
        var line = Line("l1", """{"$v":1,"bind":"x","lookup":"","keys":[]}""");
        var item = Item(ItemType.RuleList, DataType.Number, content);

        var problems = _validator.Validate(item, [line], null, "k", CelCompileMode.Dynamic);
        Assert.Contains(problems, p => p.Code == "rule_list_bind");
    }

    [Fact]
    public void Matrix_fails_when_column_axis_has_no_headers()
    {
        var content = Json("""
            {"$v":1,"rows":{"name":"r","type":"String","match":"exact"},
             "cols":{"name":"c","type":"String","match":"exact"},"cell":{"type":"Number"}}
            """);
        var item = Item(ItemType.Matrix, DataType.Number, content);

        var problems = _validator.Validate(item, [], null, "k", CelCompileMode.Dynamic);
        Assert.Contains(problems, p => p.Code == "matrix_axis_headers");
    }

    [Fact]
    public void Matrix_fails_when_exact_axis_row_header_uses_range_shape()
    {
        var content = Json("""
            {"$v":1,"rows":{"name":"r","type":"String","match":"exact"},
             "cols":{"name":"c","type":"String","match":"exact","headers":[{"key":"A"}]},"cell":{"type":"Number"}}
            """);
        var line = Line("l1", """{"$v":1,"header":{"from":0,"to":1},"cells":{"A":1}}""");
        var item = Item(ItemType.Matrix, DataType.Number, content);

        var problems = _validator.Validate(item, [line], null, "k", CelCompileMode.Dynamic);
        Assert.Contains(problems, p => p.Code == "matrix_header_shape");
    }

    [Fact]
    public void Matrix_passes_with_valid_exact_exact_row()
    {
        var content = Json("""
            {"$v":1,"rows":{"name":"r","type":"String","match":"exact"},
             "cols":{"name":"c","type":"String","match":"exact","headers":[{"key":"A"}]},"cell":{"type":"Number"}}
            """);
        var line = Line("l1", """{"$v":1,"header":{"key":"row1"},"cells":{"A":1}}""");
        var item = Item(ItemType.Matrix, DataType.Number, content);

        Assert.Empty(_validator.Validate(item, [line], null, "k", CelCompileMode.Dynamic));
    }

    [Fact]
    public void Inline_rule_nesting_beyond_eight_levels_is_rejected()
    {
        var item = Item(ItemType.Matrix, DataType.Number, MatrixContentWithSingleCell(BuildNestedMatrixSlot(12)));
        var line = new ValidatedLine { Id = "row1", Content = MatrixLine("row1", "A", BuildNestedMatrixSlot(12)) };
        var problems = _validator.Validate(item, [line], null, "k", CelCompileMode.Dynamic);

        Assert.Contains(problems, p => p.Code == "inline_nesting");
    }

    [Fact]
    public void Inline_rule_nesting_within_eight_levels_is_accepted()
    {
        var item = Item(ItemType.Matrix, DataType.Number, MatrixContentWithSingleCell(BuildNestedMatrixSlot(3)));
        var line = new ValidatedLine { Id = "row1", Content = MatrixLine("row1", "A", BuildNestedMatrixSlot(3)) };
        var problems = _validator.Validate(item, [line], null, "k", CelCompileMode.Dynamic);

        Assert.DoesNotContain(problems, p => p.Code == "inline_nesting");
    }

    [Fact]
    public void DecisionTree_depth_beyond_32_is_rejected()
    {
        var tree = new DecisionTreeContent { Root = BuildNestedIfNode(35) };
        var item = Item(ItemType.DecisionTree, DataType.Number, ItemContentSerializer.Serialize(tree));

        var problems = _validator.Validate(item, [], null, "k", CelCompileMode.Dynamic);
        Assert.Contains(problems, p => p.Code == "tree_depth");
    }

    [Fact]
    public void DecisionTree_depth_within_32_is_accepted()
    {
        var tree = new DecisionTreeContent { Root = BuildNestedIfNode(5) };
        var item = Item(ItemType.DecisionTree, DataType.Number, ItemContentSerializer.Serialize(tree));

        var problems = _validator.Validate(item, [], null, "k", CelCompileMode.Dynamic);
        Assert.DoesNotContain(problems, p => p.Code == "tree_depth");
    }

    private static DecisionTreeNode BuildNestedIfNode(int depth) =>
        depth <= 0
            ? new DecisionTreeLeafNode { Result = ValueSlot.FromLiteral(Json("1")) }
            : new DecisionTreeIfNode { If = "true", Then = BuildNestedIfNode(depth - 1), Else = new DecisionTreeLeafNode { Result = ValueSlot.FromLiteral(Json("0")) } };

    private static ValueSlot BuildNestedMatrixSlot(int remainingDepth)
    {
        if (remainingDepth <= 0)
        {
            return ValueSlot.FromLiteral(Json("1"));
        }

        var innerContent = MatrixContentWithSingleCell(BuildNestedMatrixSlot(remainingDepth - 1));
        var innerLine = MatrixLine("row", "A", BuildNestedMatrixSlot(remainingDepth - 1));
        return ValueSlot.FromRule(new InlineRuleSpec { ItemType = ItemType.Matrix, Content = innerContent, Lines = [innerLine] });
    }

    private static JsonElement MatrixContentWithSingleCell(ValueSlot _) => ItemContentSerializer.Serialize(new MatrixContent
    {
        Rows = new MatrixAxis { Name = "r", Type = DataType.String, Match = MatrixAxisMatch.Exact },
        Cols = new MatrixAxis { Name = "c", Type = DataType.String, Match = MatrixAxisMatch.Exact, Headers = [new MatrixHeader { Key = Json("\"A\"") }] },
        Cell = new MatrixCellSpec { Type = DataType.Number },
    });

    private static JsonElement MatrixLine(string headerKey, string columnKey, ValueSlot cell) => ItemContentSerializer.Serialize(new MatrixRow
    {
        Header = new MatrixHeader { Key = Json($"\"{headerKey}\"") },
        Cells = new Dictionary<string, ValueSlot> { [columnKey] = cell },
    });

    private static ValidatedItem Item(ItemType itemType, DataType dataType, JsonElement content) =>
        new() { Key = "TestItem", ItemType = itemType, DataType = dataType, Content = content };

    private static ValidatedLine Line(string id, string json) => new() { Id = id, Content = Json(json) };

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
