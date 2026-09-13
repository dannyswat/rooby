using Rooby.Engine.Evaluation;
using static Rooby.Engine.Tests.Evaluation.TestBundle;

namespace Rooby.Engine.Tests.Evaluation;

public sealed class DecisionTableHitPolicyTests
{
    private static BundleItem Table(string hitPolicy, params BundleLine[] rows) => Item(
        "T",
        ItemType.DecisionTable,
        ResolveOutputType(hitPolicy),
        $$"""
        {"$v":1,"hitPolicy":"{{hitPolicy}}","columns":[
          {"name":"tier","kind":"condition","expression":"input.tier"},
          {"name":"markup","kind":"output","type":"Number"}]}
        """,
        rows);

    private static DataType ResolveOutputType(string hitPolicy) => hitPolicy == "Collect" ? DataType.List : DataType.Number;

    [Fact]
    public void Unique_throws_when_more_than_one_row_matches()
    {
        var item = Table(
            "Unique",
            Line("""{"$v":1,"cells":{"tier":"-"},"output":{"markup":1}}"""),
            Line("""{"$v":1,"cells":{"tier":"-"},"output":{"markup":2}}"""));
        var evaluator = new BundleEvaluator(Bundle(item));

        var ex = Assert.Throws<RoobyEvaluationException>(() => evaluator.Evaluate("T", new Dictionary<string, object?> { ["tier"] = "Gold" }));
        Assert.Contains("Unique", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unique_succeeds_with_exactly_one_match()
    {
        var item = Table(
            "Unique",
            Line("""{"$v":1,"cells":{"tier":"'Gold'"},"output":{"markup":10}}"""),
            Line("""{"$v":1,"cells":{"tier":"'Silver'"},"output":{"markup":0}}"""));
        var evaluator = new BundleEvaluator(Bundle(item));

        Assert.Equal(10d, evaluator.Evaluate("T", new Dictionary<string, object?> { ["tier"] = "Gold" }));
    }

    [Fact]
    public void Priority_picks_the_highest_priority_row()
    {
        var item = Table(
            "Priority",
            Line("""{"$v":1,"cells":{"tier":"-"},"output":{"markup":1},"priority":1}"""),
            Line("""{"$v":1,"cells":{"tier":"-"},"output":{"markup":2},"priority":5}"""));
        var evaluator = new BundleEvaluator(Bundle(item));

        Assert.Equal(2d, evaluator.Evaluate("T", new Dictionary<string, object?> { ["tier"] = "Gold" }));
    }

    [Fact]
    public void Collect_returns_all_matching_outputs()
    {
        var item = Table(
            "Collect",
            Line("""{"$v":1,"cells":{"tier":"-"},"output":{"markup":1}}"""),
            Line("""{"$v":1,"cells":{"tier":"-"},"output":{"markup":2}}"""));
        var evaluator = new BundleEvaluator(Bundle(item));

        Assert.Equal(new List<object?> { 1.0, 2.0 }, evaluator.Evaluate("T", new Dictionary<string, object?> { ["tier"] = "Gold" }));
    }

    [Fact]
    public void CollectSum_adds_all_matching_outputs()
    {
        var item = Table(
            "CollectSum",
            Line("""{"$v":1,"cells":{"tier":"-"},"output":{"markup":1}}"""),
            Line("""{"$v":1,"cells":{"tier":"-"},"output":{"markup":2}}"""));
        var evaluator = new BundleEvaluator(Bundle(item));

        Assert.Equal(3d, evaluator.Evaluate("T", new Dictionary<string, object?> { ["tier"] = "Gold" }));
    }

    [Fact]
    public void CollectCount_counts_matching_rows()
    {
        var item = Table(
            "CollectCount",
            Line("""{"$v":1,"cells":{"tier":"-"},"output":{"markup":1}}"""),
            Line("""{"$v":1,"cells":{"tier":"-"},"output":{"markup":2}}"""));
        var evaluator = new BundleEvaluator(Bundle(item));

        Assert.Equal(2d, evaluator.Evaluate("T", new Dictionary<string, object?> { ["tier"] = "Gold" }));
    }
}
