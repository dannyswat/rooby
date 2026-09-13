using Rooby.Engine.Evaluation;
using static Rooby.Engine.Tests.Evaluation.TestBundle;

namespace Rooby.Engine.Tests.Evaluation;

public sealed class SubRuleTests
{
    [Fact]
    public void Ref_is_memoised_once_per_evaluation()
    {
        var shared = Item("Shared", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"1.0"}""");
        var ruleList = Item(
            "Consumer",
            ItemType.RuleList,
            DataType.List,
            """{"$v":1,"strategy":"All","output":{"type":"List"}}""",
            Line("""{"$v":1,"ref":"Shared"}""", sortOrder: 1000),
            Line("""{"$v":1,"ref":"Shared"}""", sortOrder: 2000));
        var evaluator = new BundleEvaluator(Bundle(shared, ruleList));

        var (value, session) = evaluator.EvaluateWithSession("Consumer", input: null);

        Assert.Equal(new List<object?> { 1.0, 1.0 }, value);
        Assert.Equal(1, session.GetEvalCount("Shared"));
    }

    [Fact]
    public void Inline_rule_bindings_do_not_leak_outside_the_sub_rule()
    {
        var outer = Item(
            "Outer",
            ItemType.RuleList,
            DataType.List,
            """{"$v":1,"strategy":"All","output":{"type":"List"}}""",
            Line(
                """
                {"$v":1,"rule":{"itemType":"ExpressionRule",
                  "content":{"$v":1,"bindings":[{"name":"spread","lookup":"Spread","keys":["'k'"]}],"expression":"vars.spread"}}}
                """,
                sortOrder: 1000),
            Line(
                """{"$v":1,"rule":{"itemType":"ExpressionRule","content":{"$v":1,"expression":"has(vars.spread) ? 999.0 : -1.0"}}}""",
                sortOrder: 2000));
        var spread = Item(
            "Spread",
            ItemType.Lookup,
            DataType.Number,
            """{"$v":1,"keys":[{"name":"k","type":"String"}],"match":"exact","value":{"type":"Number"}}""",
            Line("""{"$v":1,"key":{"k":"k"},"value":7}"""));
        var evaluator = new BundleEvaluator(Bundle(outer, spread));

        var result = evaluator.Evaluate("Outer", input: null);

        Assert.Equal(new List<object?> { 7.0, -1.0 }, result);
    }

    [Fact]
    public void Inline_rule_nesting_beyond_eight_levels_throws()
    {
        var outer = BuildNestedTreeItem(nestedInlineCount: 9);
        var evaluator = new BundleEvaluator(Bundle(outer));

        var ex = Assert.Throws<RoobyEvaluationException>(() => evaluator.Evaluate("Outer", input: null));
        Assert.Contains("inline rule nesting", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Inline_rule_nesting_within_eight_levels_succeeds()
    {
        var outer = BuildNestedTreeItem(nestedInlineCount: 5);
        var evaluator = new BundleEvaluator(Bundle(outer));

        Assert.Equal(1.0, evaluator.Evaluate("Outer", input: null));
    }

    /// <summary>Builds a DecisionTree whose leaf nests <paramref name="nestedInlineCount"/> further inline DecisionTree sub-rules.</summary>
    private static BundleItem BuildNestedTreeItem(int nestedInlineCount)
    {
        var resultSlotJson = "1.0";
        for (var i = 0; i < nestedInlineCount; i++)
        {
            resultSlotJson = "{\"rule\":{\"itemType\":\"DecisionTree\",\"content\":{\"$v\":1,\"root\":{\"result\":" + resultSlotJson + "}}}}";
        }

        var outerContent = "{\"$v\":1,\"root\":{\"result\":" + resultSlotJson + "}}";
        return new BundleItem
        {
            Id = Guid.NewGuid(),
            Key = "Outer",
            ItemType = ItemType.DecisionTree,
            DataType = DataType.Number,
            Content = ParseJson(outerContent),
            Lines = [],
        };
    }
}
