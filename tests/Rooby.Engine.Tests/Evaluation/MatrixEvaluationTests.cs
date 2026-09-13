using Rooby.Engine.Evaluation;
using static Rooby.Engine.Tests.Evaluation.TestBundle;

namespace Rooby.Engine.Tests.Evaluation;

public sealed class MatrixEvaluationTests
{
    [Fact]
    public void Range_row_times_exact_col_matches_via_probes()
    {
        var bundle = Bundle(Item(
            "NotionalTenorAdjBps",
            ItemType.Matrix,
            DataType.Number,
            """
            {"$v":1,"rows":{"name":"notional","type":"Number","match":"range","probe":"input.notional"},
             "cols":{"name":"tenor","type":"String","match":"exact","probe":"input.tenor","headers":[{"key":"1Y"},{"key":"2Y"},{"key":"3Y"}]},
             "cell":{"type":"Number"},"default":null}
            """,
            Line("""{"$v":1,"header":{"from":0,"to":1000000},"cells":{"1Y":10,"2Y":8,"3Y":6}}"""),
            Line("""{"$v":1,"header":{"from":1000000,"to":5000000},"cells":{"1Y":15,"2Y":12,"3Y":9}}"""),
            Line("""{"$v":1,"header":{"from":5000000,"to":null},"cells":{"1Y":20,"2Y":18,"3Y":{"ref":"JumboLongTenorAdj"}}}""")));
        var jumbo = Item("JumboLongTenorAdj", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"-4.0"}""");
        var evaluator = new BundleEvaluator(Bundle([.. bundle.Items, jumbo]));

        Assert.Equal(10d, evaluator.Evaluate("NotionalTenorAdjBps", new Dictionary<string, object?> { ["notional"] = 500_000d, ["tenor"] = "1Y" }));
        Assert.Equal(9d, evaluator.Evaluate("NotionalTenorAdjBps", new Dictionary<string, object?> { ["notional"] = 2_000_000d, ["tenor"] = "3Y" }));
        Assert.Equal(-4d, evaluator.Evaluate("NotionalTenorAdjBps", new Dictionary<string, object?> { ["notional"] = 6_000_000d, ["tenor"] = "3Y" }));
    }

    [Fact]
    public void Exact_times_exact_is_reachable_via_ref_row_col_from_another_item()
    {
        var matrix = Item(
            "Grid",
            ItemType.Matrix,
            DataType.Number,
            """
            {"$v":1,"rows":{"name":"tier","type":"String","match":"exact"},
             "cols":{"name":"tenor","type":"String","match":"exact","headers":[{"key":"1Y"}]},"cell":{"type":"Number"}}
            """,
            Line("""{"$v":1,"header":{"key":"Gold"},"cells":{"1Y":42}}"""));
        var caller = Item("Caller", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"ref.Grid['Gold']['1Y']"}""");
        var evaluator = new BundleEvaluator(Bundle(matrix, caller));

        Assert.Equal(42d, evaluator.Evaluate("Caller", input: null));
    }

    [Fact]
    public void Explicit_keys_take_precedence_over_probes()
    {
        var bundle = Bundle(Item(
            "Grid",
            ItemType.Matrix,
            DataType.Number,
            """
            {"$v":1,"rows":{"name":"tier","type":"String","match":"exact","probe":"input.tier"},
             "cols":{"name":"tenor","type":"String","match":"exact","probe":"input.tenor","headers":[{"key":"1Y"},{"key":"2Y"}]},
             "cell":{"type":"Number"}}
            """,
            Line("""{"$v":1,"header":{"key":"Gold"},"cells":{"1Y":1,"2Y":2}}"""),
            Line("""{"$v":1,"header":{"key":"Silver"},"cells":{"1Y":10,"2Y":20}}""")));
        var evaluator = new BundleEvaluator(bundle);

        var input = new Dictionary<string, object?> { ["tier"] = "Gold", ["tenor"] = "1Y" };
        Assert.Equal(20d, evaluator.MatrixValue("Grid", "Silver", "2Y", input)); // explicit keys override the probed input
    }

    [Fact]
    public void Missing_cell_returns_the_matrix_default()
    {
        var bundle = Bundle(Item(
            "Grid",
            ItemType.Matrix,
            DataType.Number,
            """
            {"$v":1,"rows":{"name":"tier","type":"String","match":"exact"},
             "cols":{"name":"tenor","type":"String","match":"exact","headers":[{"key":"1Y"},{"key":"2Y"}]},
             "cell":{"type":"Number"},"default":-1}
            """,
            Line("""{"$v":1,"header":{"key":"Gold"},"cells":{"1Y":30}}""")));
        var evaluator = new BundleEvaluator(bundle);

        Assert.Equal(-1d, evaluator.MatrixValue("Grid", "Gold", "2Y"));
        Assert.Equal(-1d, evaluator.MatrixValue("Grid", "Bronze", "1Y"));
    }

    [Fact]
    public void Row_validity_switches_the_matched_row_at_the_boundary()
    {
        var bundle = Bundle(Item(
            "Grid",
            ItemType.Matrix,
            DataType.Number,
            """
            {"$v":1,"rows":{"name":"tier","type":"String","match":"exact"},
             "cols":{"name":"tenor","type":"String","match":"exact","headers":[{"key":"1Y"}]},"cell":{"type":"Number"}}
            """,
            Line("""{"$v":1,"header":{"key":"Gold"},"cells":{"1Y":10}}""", validity: new LineValidity(null, new DateOnly(2026, 10, 1))),
            Line("""{"$v":1,"header":{"key":"Gold"},"cells":{"1Y":20}}""", validity: new LineValidity(new DateOnly(2026, 10, 1), null))));
        var evaluator = new BundleEvaluator(bundle);

        var beforeCutover = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        var afterCutover = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(10d, evaluator.MatrixValue("Grid", "Gold", "1Y", now: beforeCutover));
        Assert.Equal(20d, evaluator.MatrixValue("Grid", "Gold", "1Y", now: afterCutover));
    }
}
