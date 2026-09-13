namespace Rooby.Api.Versioning;

public sealed class ConcurrencyConflictException(string message) : Exception(message);
