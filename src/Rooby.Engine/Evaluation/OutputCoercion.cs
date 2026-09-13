using System.Globalization;

namespace Rooby.Engine.Evaluation;

/// <summary>Coerces an evaluated CEL/native value to its slot's/item's declared <see cref="DataType"/> (SPEC §5.5).</summary>
public static class OutputCoercion
{
    public static object? Coerce(object? value, DataType type, string itemKey, string? slotPath)
    {
        if (value is null)
        {
            return null;
        }

        return type switch
        {
            DataType.Boolean => value as bool? ?? throw Mismatch(itemKey, slotPath, type, value),
            DataType.Number => CoerceNumber(value) ?? throw Mismatch(itemKey, slotPath, type, value),
            DataType.String => value as string ?? throw Mismatch(itemKey, slotPath, type, value),
            DataType.Date => CoerceDate(value) ?? throw Mismatch(itemKey, slotPath, type, value),
            DataType.List => value is string ? throw Mismatch(itemKey, slotPath, type, value) : CoerceList(value, itemKey, slotPath),
            DataType.Object => value is IReadOnlyDictionary<string, object?> ? value : throw Mismatch(itemKey, slotPath, type, value),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    private static double? CoerceNumber(object value) => value switch
    {
        double d => d,
        long l => l,
        int i => i,
        float f => f,
        decimal m => (double)m,
        _ => null,
    };

    private static DateTimeOffset? CoerceDate(object value) => value switch
    {
        DateTimeOffset dt => dt,
        DateTime dt => new DateTimeOffset(dt),
        DateOnly d => new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
        string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
        _ => null,
    };

    private static List<object?> CoerceList(object value, string itemKey, string? slotPath) => value switch
    {
        List<object?> list => list,
        System.Collections.IEnumerable enumerable => enumerable.Cast<object?>().ToList(),
        _ => throw Mismatch(itemKey, slotPath, DataType.List, value),
    };

    private static RoobyEvaluationException Mismatch(string itemKey, string? slotPath, DataType type, object value) =>
        new(itemKey, slotPath, $"expected a {type} value but got '{value}' ({value.GetType().Name})");
}
