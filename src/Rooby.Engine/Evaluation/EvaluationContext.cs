namespace Rooby.Engine.Evaluation;

/// <summary>
/// Per-scope evaluation state (SPEC §5.2): shares <see cref="Session"/> (input/now/tz/memo/trace)
/// with every item reached from one top-level call, but carries its own <see cref="Vars"/> (read-only
/// snapshot propagated to sub rules, §4.0) and <see cref="Prev"/> (RuleList <c>Chain</c> accumulator,
/// reset — not inherited — whenever evaluation moves to a different item).
/// </summary>
public sealed record EvaluationContext
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyVars = new Dictionary<string, object?>(0);

    public required EvaluationSession Session { get; init; }

    public IReadOnlyDictionary<string, object?> Vars { get; init; } = EmptyVars;

    public object? Prev { get; init; }

    /// <summary>Inline ad-hoc rule nesting depth (§4.0, capped at 8) — distinct from item-to-item references.</summary>
    public int InlineDepth { get; init; }

    public object? Input => Session.Input;

    public DateTimeOffset Now => Session.Now;

    public string TimeZone => Session.TimeZone;

    public static EvaluationContext Create(object? input, DateTimeOffset now, string timeZone, EvalLimits? limits = null) =>
        new()
        {
            Session = new EvaluationSession { Input = input, Now = now, TimeZone = timeZone, Limits = limits ?? new EvalLimits() },
        };

    /// <summary>Context for evaluating a different item (ref/step target): vars propagate read-only, prev/inline-depth reset.</summary>
    public EvaluationContext ForReferencedItem() => this with { Prev = null, InlineDepth = 0 };
}
