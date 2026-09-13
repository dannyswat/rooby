namespace Rooby.Engine.Evaluation;

/// <summary>
/// Mutable state shared by every item evaluated during one top-level <see cref="BundleEvaluator"/>
/// call: the caller's <c>input</c>/<c>now</c>/<c>tz</c>, the <c>ref</c> resolution memo (SPEC §5.2:
/// "resolved lazily and memoised per evaluation" — memoised per item key for the lifetime of one
/// session, not across separate top-level calls), the trace, and the step budget.
/// </summary>
public sealed class EvaluationSession
{
    private readonly Dictionary<string, object?> _refMemo = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _evalCounts = new(StringComparer.Ordinal);
    private long _steps;

    public required object? Input { get; init; }

    public required DateTimeOffset Now { get; init; }

    public required string TimeZone { get; init; }

    public EvalLimits Limits { get; init; } = new();

    public EvaluationTrace Trace { get; } = new();

    internal bool TryGetMemo(string itemKey, out object? value) => _refMemo.TryGetValue(itemKey, out value);

    internal void SetMemo(string itemKey, object? value) => _refMemo[itemKey] = value;

    /// <summary>Number of times an item's evaluator body actually ran (excludes memo hits) — used to verify memoisation.</summary>
    internal int GetEvalCount(string itemKey) => _evalCounts.GetValueOrDefault(itemKey);

    internal void RecordEvalStart(string itemKey) => _evalCounts[itemKey] = GetEvalCount(itemKey) + 1;

    internal void ChargeStep(string itemKey)
    {
        if (++_steps > Limits.MaxIterations)
        {
            throw new RoobyEvaluationException(itemKey, null, $"evaluation exceeded the step limit ({Limits.MaxIterations})");
        }

        Limits.CancellationToken.ThrowIfCancellationRequested();
    }
}
