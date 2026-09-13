using System.Text.Json;
using System.Text.Json.Serialization;
using Rooby.Engine.Json;

namespace Rooby.Engine.Content;

/// <summary>Match kind for a Matrix axis (SPEC §4.8).</summary>
[JsonConverter(typeof(LowercaseEnumConverter<MatrixAxisMatch>))]
public enum MatrixAxisMatch
{
    Exact,
    Range,
}

/// <summary>
/// One axis header: <see cref="Key"/> for an exact-match axis, or <see cref="From"/>/<see cref="To"/>
/// (null = unbounded) for a range-match axis (SPEC §4.8).
/// </summary>
public sealed record MatrixHeader
{
    public JsonElement? Key { get; init; }

    public JsonElement? From { get; init; }

    public JsonElement? To { get; init; }
}

/// <summary>
/// One Matrix axis (SPEC §4.8). <see cref="Headers"/> is set for the column axis, whose headers live
/// in Item.Content; the row axis instead gets one header per ItemLine.
/// </summary>
public sealed record MatrixAxis
{
    public required string Name { get; init; }

    public required DataType Type { get; init; }

    public required MatrixAxisMatch Match { get; init; }

    /// <summary>Optional CEL expression producing this axis's key from <c>input</c>.</summary>
    public string? Probe { get; init; }

    public IReadOnlyList<MatrixHeader>? Headers { get; init; }
}

public sealed record MatrixCellSpec
{
    public required DataType Type { get; init; }
}

/// <summary>Item.Content shape for ItemType.Matrix (SPEC §4.8).</summary>
public sealed record MatrixContent
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public required MatrixAxis Rows { get; init; }

    public required MatrixAxis Cols { get; init; }

    public required MatrixCellSpec Cell { get; init; }

    public JsonElement? Default { get; init; }
}

/// <summary>ItemLine.Content shape for one Matrix row: its own header plus one slot per column key.</summary>
public sealed record MatrixRow
{
    [JsonPropertyName("$v")]
    public int Version { get; init; } = 1;

    public required MatrixHeader Header { get; init; }

    public required IReadOnlyDictionary<string, ValueSlot> Cells { get; init; }
}
