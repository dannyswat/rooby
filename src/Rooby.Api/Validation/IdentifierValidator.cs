using System.Text.RegularExpressions;

namespace Rooby.Api.Validation;

/// <summary>SPEC §2: Project.Code, Profile.Code, Item.Key, Schema.Code must be a valid CEL member
/// access (`ref.&lt;Key&gt;`) and URL-safe.</summary>
public static partial class IdentifierValidator
{
    public static bool IsValid(string? value) => !string.IsNullOrEmpty(value) && Pattern().IsMatch(value);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex Pattern();
}
