using Rooby.Engine.Evaluation;
using static Rooby.Engine.Tests.Evaluation.TestBundle;

namespace Rooby.Engine.Tests.Evaluation;

/// <summary>
/// End-to-end scenario in the spirit of SPEC Appendix A (structured product pricing): a RuleList
/// `Chain` composing a Lookup (via `bind`), a range×exact Matrix (via `ref`), a DecisionTable, an
/// ExpressionRule, a DecisionTree and a Basket guard. Bps values are this test's own choices (the
/// appendix leaves several intermediate numbers illustrative rather than fully specified), so the
/// expected total is hand-computed here rather than copied from the doc's "42" example.
/// </summary>
public sealed class AppendixAScenarioTests
{
    private static BundleEvaluator BuildEvaluator() => new(Bundle(
        Item("MinNotional", ItemType.SingleValue, DataType.Number, """{"$v":1,"value":500000}"""),
        Item(
            "RestrictedUnderlyings",
            ItemType.Basket,
            DataType.List,
            """{"$v":1,"elementType":"String","unique":true}""",
            Line("""{"$v":1,"value":"TSLA.US"}"""),
            Line("""{"$v":1,"value":"0700.HK"}""")),
        Item(
            "TenorSpreadBps",
            ItemType.Lookup,
            DataType.Number,
            """{"$v":1,"keys":[{"name":"productType","type":"String"},{"name":"tenor","type":"String"}],"match":"exact","value":{"type":"Number"}}""",
            Line("""{"$v":1,"key":{"productType":"ELN","tenor":"1Y"},"value":20}""")),
        Item(
            "JumboLongTenorAdj",
            ItemType.ExpressionRule,
            DataType.Number,
            """{"$v":1,"expression":"input.region == 'APAC' ? -4.0 : -2.0"}"""),
        Item(
            "NotionalTenorAdjBps",
            ItemType.Matrix,
            DataType.Number,
            """
            {"$v":1,"rows":{"name":"notional","type":"Number","match":"range","probe":"input.notional"},
             "cols":{"name":"tenor","type":"String","match":"exact","probe":"input.tenor","headers":[{"key":"1Y"},{"key":"2Y"},{"key":"3Y"}]},
             "cell":{"type":"Number"}}
            """,
            Line("""{"$v":1,"header":{"from":0,"to":1000000},"cells":{"1Y":2,"2Y":1,"3Y":0}}"""),
            Line("""{"$v":1,"header":{"from":1000000,"to":5000000},"cells":{"1Y":5,"2Y":4,"3Y":3}}"""),
            Line("""{"$v":1,"header":{"from":5000000,"to":null},"cells":{"1Y":8,"2Y":7,"3Y":{"ref":"JumboLongTenorAdj"}}}""")),
        Item(
            "ClientTierMarkup",
            ItemType.DecisionTable,
            DataType.Number,
            """
            {"$v":1,"hitPolicy":"First","columns":[
              {"name":"tier","kind":"condition","expression":"input.tier"},
              {"name":"notional","kind":"condition","expression":"input.notional"},
              {"name":"markup","kind":"output","type":"Number"}]}
            """,
            Line("""{"$v":1,"cells":{"tier":"'Gold'","notional":">= 5000000"},"output":{"markup":15}}""", sortOrder: 1000),
            Line("""{"$v":1,"cells":{"tier":"'Gold'"},"output":{"markup":10}}""", sortOrder: 2000),
            Line("""{"$v":1,"cells":{},"output":{"markup":0}}""", sortOrder: 3000)),
        Item("LargeTicketDiscount", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"input.notional >= 5000000.0 ? -3.0 : 0.0"}"""),
        Item(
            "DeskAdjustment",
            ItemType.DecisionTree,
            DataType.Number,
            """{"$v":1,"root":{"switch":"input.desk","cases":{"'EQD_HK'":{"result":5}},"default":{"result":0}}}"""),
        Item(
            "FinalPriceBps",
            ItemType.RuleList,
            DataType.Number,
            """{"$v":1,"strategy":"Chain","seed":0,"output":{"type":"Number"}}""",
            Line("""{"$v":1,"bind":"spread","lookup":"TenorSpreadBps","keys":["input.productType","input.tenor"]}""", sortOrder: 1000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"prev + vars.spread"}}}""", sortOrder: 2000),
            Line(
                """
                {"$v":1,"when":"!(input.underlying in ref.RestrictedUnderlyings)",
                 "rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"prev + ref.NotionalTenorAdjBps"}}}
                """,
                sortOrder: 3000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"prev + ref.ClientTierMarkup"}}}""", sortOrder: 4000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"prev + ref.LargeTicketDiscount"}}}""", sortOrder: 5000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"prev + ref.DeskAdjustment"}}}""", sortOrder: 6000))));

    [Fact]
    public void Final_price_composes_every_item_type_via_a_chain()
    {
        var evaluator = BuildEvaluator();
        var input = new Dictionary<string, object?>
        {
            ["productType"] = "ELN",
            ["tenor"] = "1Y",
            ["notional"] = 6_000_000d,
            ["tier"] = "Gold",
            ["desk"] = "EQD_HK",
            ["underlying"] = "AAPL.US",
            ["region"] = "APAC",
        };

        // spread(20) + matrix(8) + tierMarkup(15) + largeTicketDiscount(-3) + deskAdjustment(5) = 45
        Assert.Equal(45d, evaluator.Evaluate("FinalPriceBps", input));
    }

    [Fact]
    public void Restricted_underlying_skips_the_matrix_adjustment()
    {
        var evaluator = BuildEvaluator();
        var input = new Dictionary<string, object?>
        {
            ["productType"] = "ELN",
            ["tenor"] = "1Y",
            ["notional"] = 6_000_000d,
            ["tier"] = "Gold",
            ["desk"] = "EQD_HK",
            ["underlying"] = "TSLA.US",
            ["region"] = "APAC",
        };

        // spread(20) + tierMarkup(15) + largeTicketDiscount(-3) + deskAdjustment(5) = 37 (matrix step skipped)
        Assert.Equal(37d, evaluator.Evaluate("FinalPriceBps", input));
    }
}
