using System.Text.Json;
using Rooby.Engine.Content;

namespace Rooby.Engine.Dependencies;

/// <summary>
/// Extracts the set of item keys an item/line set depends on (SPEC §5.3): every value-slot <c>ref</c>,
/// binding <c>lookup</c>, RuleList step <c>ref</c>/<c>bind</c>, inline sub-rule (recursively), and
/// <c>ref.X</c> member access found inside a CEL expression. Used by publish validation (§8.2, cycle/
/// dangling-ref detection) and by the runner to evaluate in dependency order.
/// </summary>
public interface IDependencyExtractor
{
    IReadOnlySet<string> Extract(ItemType itemType, JsonElement content, IReadOnlyList<JsonElement> lines);
}
