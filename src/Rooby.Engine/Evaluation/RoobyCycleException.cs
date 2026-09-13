namespace Rooby.Engine.Evaluation;

/// <summary>A dependency cycle detected at bundle-load time (SPEC §5.3, §8.2).</summary>
public sealed class RoobyCycleException(IReadOnlyList<string> cycle)
    : Exception($"dependency cycle detected: {string.Join(" -> ", cycle)}")
{
    public IReadOnlyList<string> Cycle { get; } = cycle;
}
