using System.Text.Json;
using NpgsqlTypes;

namespace Rooby.Api.Versioning;

/// <summary>One provided row for a bulk <c>ReplaceLines</c> call; null Id means "new line".</summary>
public sealed record LineInput(
    Guid? Id, Guid? SchemaId, bool InheritSchema, NpgsqlRange<DateOnly>? Validity, string Remarks, JsonDocument Content);
