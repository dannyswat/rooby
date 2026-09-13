namespace Rooby.Engine.Evaluation;

/// <summary>
/// Raised for evaluation-time failures (SPEC §5.4): type errors, missing input fields, evaluation
/// limit hits. Carries the item key and, where known, a slot path (e.g. <c>line:3/cell:1Y</c>).
/// </summary>
public sealed class RoobyEvaluationException(string itemKey, string? slotPath, string message, Exception? innerException = null)
    : Exception(FormatMessage(itemKey, slotPath, message), innerException)
{
    public string ItemKey { get; } = itemKey;

    public string? SlotPath { get; } = slotPath;

    private static string FormatMessage(string itemKey, string? slotPath, string message) =>
        slotPath is null ? $"{itemKey}: {message}" : $"{itemKey}/{slotPath}: {message}";
}
