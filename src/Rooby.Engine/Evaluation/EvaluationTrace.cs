namespace Rooby.Engine.Evaluation;

/// <summary>One recorded step of an evaluation (§13 test panel trace: which step/row/cell matched).</summary>
public sealed record EvaluationTraceEntry(string ItemKey, string Detail);

/// <summary>Accumulates human-readable trace entries for one top-level evaluation.</summary>
public sealed class EvaluationTrace
{
    private readonly List<EvaluationTraceEntry> _entries = [];

    public IReadOnlyList<EvaluationTraceEntry> Entries => _entries;

    public void Record(string itemKey, string detail) => _entries.Add(new EvaluationTraceEntry(itemKey, detail));
}
