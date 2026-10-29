using L2Points.Domain.Activities;

namespace L2Points.Domain.Attendances;

/// <summary>Which dates a user may edit attendance for. Players are limited to a recent window, admins are not.</summary>
public static class AttendanceRules
{
    /// <summary>
    /// Player: max(trackingStart, today - <see cref="EditWindow.DaysBack"/>) &lt;= date &lt;= today.
    /// Admin: trackingStart &lt;= date &lt;= today.
    /// </summary>
    public static bool CanEdit(DateOnly date, DateOnly today, DateOnly trackingStart, bool isAdmin) =>
        date <= today && date >= EarliestEditableDate(today, trackingStart, isAdmin);

    public static DateOnly EarliestEditableDate(DateOnly today, DateOnly trackingStart, bool isAdmin)
    {
        if (isAdmin)
            return trackingStart;

        var windowStart = today.AddDays(-EditWindow.DaysBack);
        return windowStart > trackingStart ? windowStart : trackingStart;
    }

    /// <summary>
    /// Most attendances one player may record for the activity in a week (Monday to Sunday): a flexible boss
    /// spawns about <see cref="Activity.ExpectedPerWeek"/> times. Null when unlimited (scheduled activities
    /// are limited by their schedule instead).
    /// </summary>
    public static int? WeeklyLimit(Activity activity) =>
        activity.ScheduleType == ScheduleType.Flexible ? activity.ExpectedPerWeek : null;

    public static bool IsWeeklyLimitReached(Activity activity, int attendedInWeek) =>
        WeeklyLimit(activity) is { } limit && attendedInWeek >= limit;
}
