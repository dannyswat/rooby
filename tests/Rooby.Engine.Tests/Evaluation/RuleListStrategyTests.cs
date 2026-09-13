using Rooby.Engine.Evaluation;
using static Rooby.Engine.Tests.Evaluation.TestBundle;

namespace Rooby.Engine.Tests.Evaluation;

public sealed class RuleListStrategyTests
{
    [Fact]
    public void FirstMatch_returns_the_first_non_null_step()
    {
        var item = Item(
            "R",
            ItemType.RuleList,
            DataType.Number,
            """{"$v":1,"strategy":"FirstMatch","output":{"type":"Number"}}""",
            Line("""{"$v":1,"when":"false","rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"1.0"}}}""", sortOrder: 1000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"2.0"}}}""", sortOrder: 2000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"3.0"}}}""", sortOrder: 3000));
        var evaluator = new BundleEvaluator(Bundle(item));

        Assert.Equal(2d, evaluator.Evaluate("R", input: null));
    }

    [Fact]
    public void All_collects_every_matching_non_null_step()
    {
        var item = Item(
            "R",
            ItemType.RuleList,
            DataType.List,
            """{"$v":1,"strategy":"All","output":{"type":"List"}}""",
            Line("""{"$v":1,"when":"false","rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"1.0"}}}""", sortOrder: 1000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"2.0"}}}""", sortOrder: 2000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"3.0"}}}""", sortOrder: 3000));
        var evaluator = new BundleEvaluator(Bundle(item));

        Assert.Equal(new List<object?> { 2.0, 3.0 }, evaluator.Evaluate("R", input: null));
    }

    [Fact]
    public void Chain_passes_prev_through_non_matching_steps()
    {
        var item = Item(
            "R",
            ItemType.RuleList,
            DataType.Number,
            """{"$v":1,"strategy":"Chain","seed":10,"output":{"type":"Number"}}""",
            Line("""{"$v":1,"when":"false","rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"prev + 1000.0"}}}""", sortOrder: 1000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"prev + 5.0"}}}""", sortOrder: 2000));
        var evaluator = new BundleEvaluator(Bundle(item));

        Assert.Equal(15d, evaluator.Evaluate("R", input: null));
    }

    [Fact]
    public void Aggregate_sum_folds_all_matching_step_results()
    {
        var item = Item(
            "R",
            ItemType.RuleList,
            DataType.Number,
            """{"$v":1,"strategy":"Aggregate","aggregate":"Sum","output":{"type":"Number"}}""",
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"1.0"}}}""", sortOrder: 1000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"2.0"}}}""", sortOrder: 2000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"3.0"}}}""", sortOrder: 3000));
        var evaluator = new BundleEvaluator(Bundle(item));

        Assert.Equal(6d, evaluator.Evaluate("R", input: null));
    }

    [Fact]
    public void Priority_picks_highest_priority_breaking_ties_by_sort_order()
    {
        var item = Item(
            "R",
            ItemType.RuleList,
            DataType.Number,
            """{"$v":1,"strategy":"Priority","output":{"type":"Number"}}""",
            Line("""{"$v":1,"priority":5,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"1.0"}}}""", sortOrder: 1000),
            Line("""{"$v":1,"priority":9,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"2.0"}}}""", sortOrder: 2000),
            Line("""{"$v":1,"priority":9,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"3.0"}}}""", sortOrder: 3000));
        var evaluator = new BundleEvaluator(Bundle(item));

        // Two steps tie at priority 9; the earlier (lower SortOrder) one wins.
        Assert.Equal(2d, evaluator.Evaluate("R", input: null));
    }

    [Fact]
    public void Bind_step_populates_vars_for_later_steps()
    {
        var lookup = Item(
            "Spread",
            ItemType.Lookup,
            DataType.Number,
            """{"$v":1,"keys":[{"name":"k","type":"String"}],"match":"exact","value":{"type":"Number"}}""",
            Line("""{"$v":1,"key":{"k":"ELN"},"value":35}"""));
        var ruleList = Item(
            "R",
            ItemType.RuleList,
            DataType.Number,
            """{"$v":1,"strategy":"Chain","seed":0,"output":{"type":"Number"}}""",
            Line("""{"$v":1,"bind":"spread","lookup":"Spread","keys":["'ELN'"]}""", sortOrder: 1000),
            Line("""{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"prev + vars.spread"}}}""", sortOrder: 2000));
        var evaluator = new BundleEvaluator(Bundle(lookup, ruleList));

        Assert.Equal(35d, evaluator.Evaluate("R", input: null));
    }
}
