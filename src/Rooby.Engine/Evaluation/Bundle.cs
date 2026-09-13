using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rooby.Engine.Evaluation;

/// <summary>An ItemLine's effective period (SPEC §5.6); null bound = unbounded. Independent of Matrix range headers.</summary>
public sealed record LineValidity(DateOnly? From, DateOnly? To);

/// <summary>One line of a bundled item (SPEC §10.4).</summary>
public sealed record BundleLine
{
    public required Guid Id { get; init; }

    public int SortOrder { get; init; }

    public LineValidity? Validity { get; init; }

    public Guid? SchemaId { get; init; }

    public required JsonElement Content { get; init; }
}

/// <summary>One bundled item (SPEC §10.4): header + lines, no schema document (informational id only).</summary>
public sealed record BundleItem
{
    public required Guid Id { get; init; }

    public required string Key { get; init; }

    public required ItemType ItemType { get; init; }

    public required DataType DataType { get; init; }

    public Guid? SchemaId { get; init; }

    public string Description { get; init; } = string.Empty;

    public required JsonElement Content { get; init; }

    public IReadOnlyList<BundleLine> Lines { get; init; } = [];
}

/// <summary>One saved test case (SPEC §3.10), included in a bundle only with <c>?includeTests=true</c>.</summary>
public sealed record BundleTestCase
{
    public required Guid Id { get; init; }

    public required string ItemKey { get; init; }

    public required JsonElement InputData { get; init; }

    public required JsonElement OutputValue { get; init; }
}

/// <summary>The self-contained delivery/runner document (SPEC §10.4).</summary>
public sealed record Bundle
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public required string Project { get; init; }

    public required string Profile { get; init; }

    public required string TimeZone { get; init; }

    public required int VersionId { get; init; }

    public DateTimeOffset PublishedAt { get; init; }

    public string Description { get; init; } = string.Empty;

    public required IReadOnlyList<BundleItem> Items { get; init; }

    public IReadOnlyList<BundleTestCase> TestCases { get; init; } = [];
}
