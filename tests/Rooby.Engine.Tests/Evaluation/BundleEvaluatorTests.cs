using Rooby.Engine.Evaluation;
using static Rooby.Engine.Tests.Evaluation.TestBundle;

namespace Rooby.Engine.Tests.Evaluation;

public sealed class BundleEvaluatorTests
{
    [Fact]
    public void Cycle_between_two_items_fails_at_bundle_construction()
    {
        var a = Item("A", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"ref.B"}""");
        var b = Item("B", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"ref.A"}""");

        Assert.Throws<RoobyCycleException>(() => new BundleEvaluator(Bundle(a, b)));
    }

    [Fact]
    public void Acyclic_dependency_graph_constructs_successfully()
    {
        var a = Item("A", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"ref.B + 1.0"}""");
        var b = Item("B", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"1.0"}""");
        var evaluator = new BundleEvaluator(Bundle(a, b));

        Assert.Equal(2d, evaluator.Evaluate("A", input: null));
    }

    [Fact]
    public void Evaluation_limit_hit_raises_RoobyEvaluationException()
    {
        // Chain of 5 ref hops, each charging a step; a limit of 2 must be exceeded.
        var items = new[]
        {
            Item("A0", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"ref.A1"}"""),
            Item("A1", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"ref.A2"}"""),
            Item("A2", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"ref.A3"}"""),
            Item("A3", ItemType.ExpressionRule, DataType.Number, """{"$v":1,"expression":"1.0"}"""),
        };
        var evaluator = new BundleEvaluator(Bundle(items));

        Assert.Throws<RoobyEvaluationException>(() => evaluator.Evaluate("A0", input: null, limits: new EvalLimits { MaxIterations = 2 }));
    }
}
