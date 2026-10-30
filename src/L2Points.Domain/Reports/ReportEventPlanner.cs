using L2Points.Domain.Activities;
using L2Points.Domain.Schedule;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Domain.Reports;

/// <summary>An event a report can be attributed to, with its activity and spawn instant (UTC) when the time is known.</summary>
public sealed record ReportEventOption(AttendanceEntry Event, Activity Activity, DateTime? SpawnUtc);

/// <summary>Builds the selectable events of a report day and preselects the ones that just spawned.</summary>
public static class ReportEventPlanner
{
    /// <summary>How far back from the report time a spawn is still preselected.</summary>
    public static readonly TimeSpan PreselectWindow = TimeSpan.FromHours(6);

    /// <summary>Maximum number of selectable report days (today included).</summary>
    public const int DateOptionDays = 7;

    /// <summary>Selectable report days: today first, descending, at most <see cref="DateOptionDays"/>, none before the tracking start.</summary>
    public static IReadOnlyList<DateOnly> DateOptions(DateOnly todayPrague, DateOnly trackingStart)
    {
        var result = new List<DateOnly>();
        for (var i = 0; i < DateOptionDays; i++)
        {
            var date = todayPrague.AddDays(-i);
            if (date < trackingStart)
                break;
            result.Add(date);
        }

        return result;
    }

    /// <summary>
    /// Events of <paramref name="date"/>: fixed occurrences plus every active flexible activity,
    /// ordered by activity sort order, then slot.
    /// </summary>
    public static IReadOnlyList<ReportEventOption> EventOptions(IEnumerable<Activity> activities, DateOnly date)
    {
        var list = activities.ToList();
        var options = new List<ReportEventOption>();

        foreach (var occurrence in ScheduleCalculator.GetFixedOccurrences(list, new DateRange(date, date)))
        {
            DateTime? spawn = occurrence.TimeUtc is { } time
                ? DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Utc)
                : null;
            options.Add(new ReportEventOption(
                new AttendanceEntry(occurrence.Activity.Id, date, occurrence.Slot),
                occurrence.Activity,
                spawn));
        }

        foreach (var activity in list.Where(a => a.IsActive && a.ScheduleType == ScheduleType.Flexible))
        {
            options.Add(new ReportEventOption(
                new AttendanceEntry(activity.Id, date, EventSlot.None),
                activity,
                null));
        }

        return options
            .OrderBy(o => o.Activity.SortOrder)
            .ThenBy(o => o.Event.Slot)
            .ToList();
    }

    /// <summary>
    /// Events with a known spawn time in (<paramref name="reportTime"/> minus <see cref="PreselectWindow"/>, <paramref name="reportTime"/>]
    /// that are not yet <paramref name="recorded"/>, ordered like <see cref="EventOptions"/>, then by date.
    /// </summary>
    public static IReadOnlyList<AttendanceEntry> Preselect(
        IEnumerable<Activity> activities,
        DateTimeOffset reportTime,
        IReadOnlySet<AttendanceEntry> recorded)
    {
        var list = activities.ToList();
        var windowStart = (reportTime - PreselectWindow).UtcDateTime;
        var windowEnd = reportTime.UtcDateTime;

        var result = new List<ReportEventOption>();
        for (var date = DateOnly.FromDateTime(windowStart); date <= DateOnly.FromDateTime(windowEnd); date = date.AddDays(1))
        {
            result.AddRange(EventOptions(list, date).Where(o =>
                o.SpawnUtc is { } spawn
                && spawn > windowStart
                && spawn <= windowEnd
                && !recorded.Contains(o.Event)));
        }

        return result
            .OrderBy(o => o.Activity.SortOrder)
            .ThenBy(o => o.Event.Slot)
            .ThenBy(o => o.Event.Date)
            .Select(o => o.Event)
            .ToList();
    }

    /// <summary>Latest date among the preselected events, otherwise <paramref name="todayPrague"/>.</summary>
    public static DateOnly DefaultDate(IReadOnlyList<AttendanceEntry> preselected, DateOnly todayPrague) =>
        preselected.Count == 0 ? todayPrague : preselected.Max(e => e.Date);
}
