using Rooby.Engine.Content;
using Rooby.Engine.Tests.TestSupport;

namespace Rooby.Engine.Tests.Content;

public sealed class ContentRoundTripTests
{
    [Fact]
    public void SingleValue_content_round_trips()
    {
        const string json = """{"$v":1,"value":5000000,"unit":"USD"}""";
        var content = ItemContentSerializer.Deserialize<SingleValueContent>(Parse(json));

        Assert.Equal(5000000, content.Value.GetInt32());
        Assert.Equal("USD", content.Unit);
        JsonAssert.Equivalent(json, ItemContentSerializer.Serialize(content));
    }

    [Fact]
    public void Lookup_exact_content_and_line_round_trip()
    {
        const string contentJson = """
            {"$v":1,"keys":[{"name":"productType","type":"String"},{"name":"tenor","type":"String"}],
             "match":"exact","value":{"type":"Number"}}
            """;
        const string lineJson = """{"$v":1,"key":{"productType":"ELN","tenor":"1Y"},"value":35}""";

        var content = ItemContentSerializer.Deserialize<LookupContent>(Parse(contentJson));
        Assert.Equal(2, content.Keys.Count);
        Assert.Equal(LookupMatch.Exact, content.Match);
        Assert.Equal(DataType.Number, content.Value.Type);
        JsonAssert.Equivalent(contentJson, ItemContentSerializer.Serialize(content));

        var line = ItemContentSerializer.Deserialize<LookupLine>(Parse(lineJson));
        Assert.Equal("ELN", line.Key["productType"].GetString());
        Assert.Equal(35, line.Value.GetInt32());
        JsonAssert.Equivalent(lineJson, ItemContentSerializer.Serialize(line));
    }

    [Fact]
    public void Lookup_range_line_round_trips()
    {
        const string lineJson = """{"$v":1,"key":{"tier":"Gold","from":1000000,"to":5000000},"value":0.9}""";
        var line = ItemContentSerializer.Deserialize<LookupLine>(Parse(lineJson));

        Assert.Equal("Gold", line.Key["tier"].GetString());
        Assert.Equal(1000000, line.Key["from"].GetInt32());
        JsonAssert.Equivalent(lineJson, ItemContentSerializer.Serialize(line));
    }

    [Fact]
    public void Basket_content_and_line_round_trip()
    {
        const string contentJson = """{"$v":1,"elementType":"String","unique":true}""";
        const string lineJson = """{"$v":1,"value":"TSLA.US"}""";

        var content = ItemContentSerializer.Deserialize<BasketContent>(Parse(contentJson));
        Assert.Equal(DataType.String, content.ElementType);
        Assert.True(content.Unique);
        JsonAssert.Equivalent(contentJson, ItemContentSerializer.Serialize(content));

        var line = ItemContentSerializer.Deserialize<BasketLine>(Parse(lineJson));
        Assert.Equal("TSLA.US", line.Value.GetString());
        JsonAssert.Equivalent(lineJson, ItemContentSerializer.Serialize(line));
    }

    [Fact]
    public void ExpressionRule_content_round_trips()
    {
        const string json = """
            {"$v":1,
             "bindings":[{"name":"spread","lookup":"TenorSpreadBps","keys":["input.product.type","input.product.tenor"]}],
             "expression":"vars.spread + (input.client.tier == 'Gold' ? -5 : 0)"}
            """;
        var content = ItemContentSerializer.Deserialize<ExpressionRuleContent>(Parse(json));

        Assert.Single(content.Bindings!);
        Assert.Equal("TenorSpreadBps", content.Bindings![0].Lookup);
        JsonAssert.Equivalent(json, ItemContentSerializer.Serialize(content));
    }

    [Fact]
    public void DecisionTable_content_and_row_round_trip()
    {
        const string contentJson = """
            {"$v":1,"hitPolicy":"First",
             "columns":[
               {"name":"tier","kind":"condition","expression":"input.client.tier"},
               {"name":"notional","kind":"condition","expression":"input.trade.notional"},
               {"name":"markup","kind":"output","type":"Number"}],
             "default":0}
            """;
        const string rowJson = """{"$v":1,"cells":{"tier":"'Gold'","notional":">= 1000000"},"output":{"markup":10},"priority":0}""";

        var content = ItemContentSerializer.Deserialize<DecisionTableContent>(Parse(contentJson));
        Assert.Equal(DecisionTableHitPolicy.First, content.HitPolicy);
        Assert.Equal(3, content.Columns.Count);
        JsonAssert.Equivalent(contentJson, ItemContentSerializer.Serialize(content));

        var row = ItemContentSerializer.Deserialize<DecisionTableRow>(Parse(rowJson));
        Assert.Equal("'Gold'", row.Cells!["tier"]);
        Assert.Equal(ValueSlotKind.Literal, row.Output["markup"].Kind);
        Assert.Equal(0, row.Priority);
        JsonAssert.Equivalent(rowJson, ItemContentSerializer.Serialize(row));
    }

    [Fact]
    public void DecisionTree_content_round_trips_if_switch_leaf()
    {
        const string json = """
            {"$v":1,
             "root":{"if":"input.product.type == 'ELN'",
                     "then":{"switch":"input.client.region",
                             "cases":{"'APAC'":{"result":30},"'EMEA'":{"result":28}},
                             "default":{"result":35}},
                     "else":{"result":{"ref":"DefaultSpreadBps"}}}}
            """;
        var content = ItemContentSerializer.Deserialize<DecisionTreeContent>(Parse(json));

        var ifNode = Assert.IsType<DecisionTreeIfNode>(content.Root);
        var switchNode = Assert.IsType<DecisionTreeSwitchNode>(ifNode.Then);
        Assert.Equal(2, switchNode.Cases.Count);
        var elseLeaf = Assert.IsType<DecisionTreeLeafNode>(ifNode.Else);
        Assert.Equal(ValueSlotKind.Ref, elseLeaf.Result.Kind);
        Assert.Equal("DefaultSpreadBps", elseLeaf.Result.RefKey);

        JsonAssert.Equivalent(json, ItemContentSerializer.Serialize(content));
    }

    [Fact]
    public void RuleList_content_and_all_step_forms_round_trip()
    {
        const string contentJson = """{"$v":1,"strategy":"Chain","seed":0,"output":{"type":"Number"}}""";
        const string referenceStepJson = """{"$v":1,"ref":"ClientTierMarkup","when":"input.client.tier != ''","priority":10,"enabled":true}""";
        const string inlineStepJson = """
            {"$v":1,"when":"prev > 100","enabled":true,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"prev * 0.9"}}}
            """;
        const string bindStepJson = """
            {"$v":1,"bind":"spread","lookup":"TenorSpreadBps","keys":["input.product.type","input.product.tenor"],"enabled":true}
            """;

        var content = ItemContentSerializer.Deserialize<RuleListContent>(Parse(contentJson));
        Assert.Equal(RuleListStrategy.Chain, content.Strategy);
        JsonAssert.Equivalent(contentJson, ItemContentSerializer.Serialize(content));

        var referenceStep = Assert.IsType<RuleListReferenceStep>(ItemContentSerializer.Deserialize<RuleListStep>(Parse(referenceStepJson)));
        Assert.Equal("ClientTierMarkup", referenceStep.Ref);
        Assert.Equal(10, referenceStep.Priority);
        JsonAssert.Equivalent(referenceStepJson, ItemContentSerializer.Serialize<RuleListStep>(referenceStep));

        var inlineStep = Assert.IsType<RuleListInlineStep>(ItemContentSerializer.Deserialize<RuleListStep>(Parse(inlineStepJson)));
        Assert.Equal(ItemType.ExpressionRule, inlineStep.Rule.ItemType);
        JsonAssert.Equivalent(inlineStepJson, ItemContentSerializer.Serialize<RuleListStep>(inlineStep));

        var bindStep = Assert.IsType<RuleListBindStep>(ItemContentSerializer.Deserialize<RuleListStep>(Parse(bindStepJson)));
        Assert.Equal("spread", bindStep.Bind);
        Assert.Equal(2, bindStep.Keys.Count);
        JsonAssert.Equivalent(bindStepJson, ItemContentSerializer.Serialize<RuleListStep>(bindStep));
    }

    [Fact]
    public void Matrix_content_and_row_round_trip()
    {
        const string contentJson = """
            {"$v":1,
             "rows":{"name":"notional","type":"Number","match":"range","probe":"input.trade.notional"},
             "cols":{"name":"tenor","type":"String","match":"exact","probe":"input.product.tenor",
                     "headers":[{"key":"1Y"},{"key":"2Y"},{"key":"3Y"}]},
             "cell":{"type":"Number"}}
            """;
        const string rowJson = """
            {"$v":1,"header":{"from":0,"to":1000000},
             "cells":{"1Y":30,"2Y":{"ref":"SmallTicket2Y"},"3Y":{"expression":"vars.base + 5"}}}
            """;

        var content = ItemContentSerializer.Deserialize<MatrixContent>(Parse(contentJson));
        Assert.Equal(MatrixAxisMatch.Range, content.Rows.Match);
        Assert.Equal(MatrixAxisMatch.Exact, content.Cols.Match);
        Assert.Equal(3, content.Cols.Headers!.Count);
        JsonAssert.Equivalent(contentJson, ItemContentSerializer.Serialize(content));

        var row = ItemContentSerializer.Deserialize<MatrixRow>(Parse(rowJson));
        Assert.Equal(0, row.Header.From!.Value.GetInt32());
        Assert.Equal(ValueSlotKind.Literal, row.Cells["1Y"].Kind);
        Assert.Equal(ValueSlotKind.Ref, row.Cells["2Y"].Kind);
        Assert.Equal(ValueSlotKind.Expression, row.Cells["3Y"].Kind);
        JsonAssert.Equivalent(rowJson, ItemContentSerializer.Serialize(row));
    }

    [Theory]
    [InlineData("42")]
    [InlineData("\"literal text\"")]
    [InlineData("""{"a":1,"b":[1,2,3]}""")]
    public void ValueSlot_literal_forms_round_trip(string literalJson)
    {
        var slot = ItemContentSerializer.Deserialize<ValueSlot>(Parse(literalJson));
        Assert.Equal(ValueSlotKind.Literal, slot.Kind);
        JsonAssert.Equivalent(literalJson, ItemContentSerializer.Serialize(slot));
    }

    [Fact]
    public void ValueSlot_expression_ref_and_ref_with_keys_round_trip()
    {
        var expressionSlot = ItemContentSerializer.Deserialize<ValueSlot>(Parse("""{"expression":"input.x + 1"}"""));
        Assert.Equal(ValueSlotKind.Expression, expressionSlot.Kind);
        Assert.Equal("input.x + 1", expressionSlot.Expression);

        var refSlot = ItemContentSerializer.Deserialize<ValueSlot>(Parse("""{"ref":"SomeKey"}"""));
        Assert.Equal(ValueSlotKind.Ref, refSlot.Kind);
        Assert.Null(refSlot.RefKeys);

        const string refWithKeysJson = """{"ref":"Matrix1","keys":["input.a","input.b"]}""";
        var refWithKeys = ItemContentSerializer.Deserialize<ValueSlot>(Parse(refWithKeysJson));
        Assert.Equal(2, refWithKeys.RefKeys!.Count);
        JsonAssert.Equivalent(refWithKeysJson, ItemContentSerializer.Serialize(refWithKeys));
    }

    [Fact]
    public void ValueSlot_inline_rule_round_trips_with_nested_lines()
    {
        const string json = """
            {"rule":{"itemType":"Matrix","content":{"$v":1},"lines":[{"$v":1,"header":{"key":"A"},"cells":{}}]}}
            """;
        var slot = ItemContentSerializer.Deserialize<ValueSlot>(Parse(json));

        Assert.Equal(ValueSlotKind.InlineRule, slot.Kind);
        Assert.Equal(ItemType.Matrix, slot.Rule!.ItemType);
        Assert.Single(slot.Rule.Lines!);
        JsonAssert.Equivalent(json, ItemContentSerializer.Serialize(slot));
    }

    private static System.Text.Json.JsonElement Parse(string json) => System.Text.Json.JsonDocument.Parse(json).RootElement.Clone();
}
