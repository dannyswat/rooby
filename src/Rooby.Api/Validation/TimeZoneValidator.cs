namespace Rooby.Api.Validation;

/// <summary>SPEC §3.2: Profile.TimeZone must be a valid IANA time zone id.</summary>
public static class TimeZoneValidator
{
    public static bool IsValid(string? id) => !string.IsNullOrEmpty(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
