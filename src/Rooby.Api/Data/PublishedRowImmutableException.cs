namespace Rooby.Api.Data;

public sealed class PublishedRowImmutableException(string entityName, object versionId)
    : InvalidOperationException($"{entityName} row at version {versionId} is published and immutable.")
{
    public string EntityName { get; } = entityName;

    public object VersionId { get; } = versionId;
}
