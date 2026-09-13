namespace Rooby.Engine.Evaluation;

/// <summary>Evaluation budget for one top-level <see cref="BundleEvaluator"/> call (SPEC §5.1).</summary>
public sealed class EvalLimits
{
    public int MaxIterations { get; init; } = 10_000;

    public CancellationToken CancellationToken { get; init; }
}
