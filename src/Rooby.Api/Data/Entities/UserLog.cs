namespace Rooby.Api.Data.Entities;

/// <summary>Owned type mapped to a pair of columns: "&lt;name&gt;_at" / "&lt;name&gt;_by_user_id".</summary>
public sealed class UserLog
{
    public DateTimeOffset At { get; set; }

    public int ByUserId { get; set; }
}
