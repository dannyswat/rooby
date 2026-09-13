using System.Text.Json.Serialization;

namespace Rooby.Engine.Content;

/// <summary>
/// ItemLine.Content shape for one RuleList step (SPEC §4.7): a reference to another item, an inline
/// ad-hoc rule, or a <c>bind</c> step that only populates <c>vars</c>. <see cref="When"/>/
/// <see cref="Priority"/>/<see cref="Enabled"/> apply to reference and inline steps.
/// </summary>
[JsonConverter(typeof(RuleListStepJsonConverter))]
public abstract record RuleListStep
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public string? When { get; init; }

    public int? Priority { get; init; }

    public bool Enabled { get; init; } = true;
}

public sealed record RuleListReferenceStep : RuleListStep
{
    public required string Ref { get; init; }
}

public sealed record RuleListInlineStep : RuleListStep
{
    public required InlineRuleSpec Rule { get; init; }
}

public sealed record RuleListBindStep : RuleListStep
{
    public required string Bind { get; init; }

    public required string Lookup { get; init; }

    public required IReadOnlyList<string> Keys { get; init; }
}
