using System.Globalization;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Rooby.Api.Concurrency;

/// <summary>Maps the `xmin` shadow "RowVersion" property to an ETag / If-Match pair (SPEC §10, §15.3).</summary>
public static class ConcurrencyExtensions
{
    private const string RowVersionProperty = "RowVersion";

    public static string ToETag(this EntityEntry entry)
    {
        var value = (uint)entry.Property(RowVersionProperty).CurrentValue!;
        return $"\"{value:x8}\"";
    }

    /// <summary>Applies the client's If-Match value as EF's tracked original value, so SaveChanges
    /// throws DbUpdateConcurrencyException (→ 409) on mismatch. Returns false if the header is
    /// missing or malformed (caller should return 412).</summary>
    public static bool TryApplyIfMatch(this EntityEntry entry, string? ifMatchHeaderValue)
    {
        var trimmed = ifMatchHeaderValue?.Trim().Trim('"');
        if (string.IsNullOrEmpty(trimmed) || !uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var expected))
        {
            return false;
        }

        entry.Property(RowVersionProperty).OriginalValue = expected;
        return true;
    }
}
