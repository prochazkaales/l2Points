namespace L2Points.Domain.Time;

/// <summary>Application time helpers. Server schedule is stored in UTC, everything shown to users is Europe/Prague.</summary>
public static class AppTime
{
    public static TimeZoneInfo Prague { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");

    /// <summary>Current calendar date in Prague.</summary>
    public static DateOnly TodayInPrague(TimeProvider time) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Prague).DateTime);

    /// <summary>Prague local time of an event that happens at UTC <paramref name="date"/> + <paramref name="utcTime"/>.</summary>
    public static DateTime ToPragueTime(DateOnly date, TimeOnly utcTime) =>
        TimeZoneInfo.ConvertTimeFromUtc(date.ToDateTime(utcTime, DateTimeKind.Utc), Prague);
}
