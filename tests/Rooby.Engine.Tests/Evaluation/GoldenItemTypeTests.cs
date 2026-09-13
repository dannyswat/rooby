using Rooby.Engine.Evaluation;
using static Rooby.Engine.Tests.Evaluation.TestBundle;

namespace Rooby.Engine.Tests.Evaluation;

public sealed class GoldenItemTypeTests
{
    [Fact]
    public void SingleValue_returns_its_constant()
    {
        var bundle = Bundle(Item("MinNotional", ItemType.SingleValue, DataType.Number, """{"$v":1,"value":500000}"""));
        var evaluator = new BundleEvaluator(bundle);

        Assert.Equal(500000d, evaluator.GetValue("MinNotional"));
    }

    [Fact]
    public void ExpressionRule_evaluates_against_input()
    {
        var bundle = Bundle(Item(
            "Markup",
            ItemType.ExpressionRule,
            DataType.Number,
            """{"$v":1,"expression":"input.tier == 'Gold' ? 10.0 : 0.0"}"""));
        var evaluator = new BundleEvaluator(bundle);

        var input = new Dictionary<string, object?> { ["tier"] = "Gold" };
        Assert.Equal(10d, evaluator.Evaluate("Markup", input));
    }

    [Fact]
    public void Basket_returns_its_active_lines_as_a_list()
    {
        var bundle = Bundle(Item(
            "RestrictedUnderlyings",
            ItemType.Basket,
            DataType.List,
            """{"$v":1,"elementType":"String","unique":true}""",
            Line("""{"$v":1,"value":"TSLA.US"}"""),
            Line("""{"$v":1,"value":"0700.HK"}""")));
        var evaluator = new BundleEvaluator(bundle);

        var result = Assert.IsAssignableFrom<List<object?>>(evaluator.GetValue("RestrictedUnderlyings"));
        Assert.Equal(["TSLA.US", "0700.HK"], result);
    }

    [Fact]
    public void Lookup_exact_match_finds_the_value()
    {
        var bundle = Bundle(Item(
            "TenorSpreadBps",
            ItemType.Lookup,
            DataType.Number,
            """{"$v":1,"keys":[{"name":"productType","type":"String"},{"name":"tenor","type":"String"}],"match":"exact","value":{"type":"Number"}}""",
            Line("""{"$v":1,"key":{"productType":"ELN","tenor":"1Y"},"value":35}""")));
        var evaluator = new BundleEvaluator(bundle);

        Assert.Equal(35d, evaluator.LookupValue("TenorSpreadBps", ["ELN", "1Y"]));
        Assert.Null(evaluator.LookupValue("TenorSpreadBps", ["ELN", "2Y"]));
    }

    [Fact]
    public void DecisionTable_first_hit_policy_returns_first_matching_row()
    {
        var bundle = Bundle(Item(
            "ClientTierMarkup",
            ItemType.DecisionTable,
            DataType.Number,
            """
            {"$v":1,"hitPolicy":"First","columns":[
              {"name":"tier","kind":"condition","expression":"input.tier"},
              {"name":"markup","kind":"output","type":"Number"}],
             "default":0}
            """,
            Line("""{"$v":1,"cells":{"tier":"'Gold'"},"output":{"markup":10}}"""),
            Line("""{"$v":1,"cells":{"tier":"-"},"output":{"markup":0}}""")));
        var evaluator = new BundleEvaluator(bundle);

        Assert.Equal(10d, evaluator.Evaluate("ClientTierMarkup", new Dictionary<string, object?> { ["tier"] = "Gold" }));
        Assert.Equal(0d, evaluator.Evaluate("ClientTierMarkup", new Dictionary<string, object?> { ["tier"] = "Silver" }));
    }

    [Fact]
    public void DecisionTree_traverses_if_and_switch_nodes()
    {
        var bundle = Bundle(Item(
            "DeskAdjustment",
            ItemType.DecisionTree,
            DataType.Number,
            """
            {"$v":1,"root":{"if":"input.type == 'ELN'",
                            "then":{"switch":"input.region","cases":{"'APAC'":{"result":30},"'EMEA'":{"result":28}},"default":{"result":35}},
                            "else":{"result":0}}}
            """));
        var evaluator = new BundleEvaluator(bundle);

        Assert.Equal(30d, evaluator.Evaluate("DeskAdjustment", new Dictionary<string, object?> { ["type"] = "ELN", ["region"] = "APAC" }));
        Assert.Equal(35d, evaluator.Evaluate("DeskAdjustment", new Dictionary<string, object?> { ["type"] = "ELN", ["region"] = "LATAM" }));
        Assert.Equal(0d, evaluator.Evaluate("DeskAdjustment", new Dictionary<string, object?> { ["type"] = "FCN", ["region"] = "APAC" }));
    }

    [Fact]
    public void RuleList_chain_accumulates_prev_through_steps()
    {
        var bundle = Bundle(Item(
            "FinalPriceBps",
            ItemType.RuleList,
            DataType.Number,
            """{"$v":1,"strategy":"Chain","seed":0,"output":{"type":"Number"}}""",
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"prev + 5.0"}}}""", sortOrder: 1000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"prev * 2.0"}}}""", sortOrder: 2000)));
        var evaluator = new BundleEvaluator(bundle);

        Assert.Equal(10d, evaluator.Evaluate("FinalPriceBps", input: null));
    }

    [Fact]
    public void Matrix_exact_axes_returns_cell_literal_via_explicit_keys()
    {
        var bundle = Bundle(Item(
            "NotionalTenorAdjBps",
            ItemType.Matrix,
            DataType.Number,
            """
            {"$v":1,"rows":{"name":"tier","type":"String","match":"exact"},
             "cols":{"name":"tenor","type":"String","match":"exact","headers":[{"key":"1Y"},{"key":"2Y"}]},
             "cell":{"type":"Number"}}
            """,
            Line("""{"$v":1,"header":{"key":"Gold"},"cells":{"1Y":30,"2Y":25}}""")));
        var evaluator = new BundleEvaluator(bundle);

        Assert.Equal(30d, evaluator.MatrixValue("NotionalTenorAdjBps", "Gold", "1Y"));
        Assert.Equal(25d, evaluator.MatrixValue("NotionalTenorAdjBps", "Gold", "2Y"));
    }
}
