using L2Points.Domain.Activities;
using L2Points.Domain.Schedule;
using L2Points.Domain.Time;

namespace L2Points.Domain.Stats;

public static class WeekEventsCalculator
{
    /// <summary>
    /// Events of the week with at least one attendee, ordered by date and then by spawn time; events without
    /// a fixed time (flexible bosses) come first in their day, ties by activity sort order and slot.
    /// Inactive activities are included because this is history. Attendees are ordered by nick ignoring case.
    /// Attendances of unknown players or unknown activities are ignored.
    /// </summary>
    public static IReadOnlyList<WeekEvent> Build(
        IReadOnlyList<PlayerInfo> players,
        IReadOnlyList<Activity> activities,
        IReadOnlyCollection<UserAttendance> attendances,
        Week week)
    {
        var playersById = players.ToDictionary(p => p.UserId);
        var activitiesById = activities.ToDictionary(a => a.Id);
        var range = week.Range;
        var times = ScheduleCalculator.GetFixedOccurrences(activities, range)
            .ToDictionary(o => (o.Activity.Id, o.Date, o.Slot), o => o.TimeUtc);

        return attendances
            .Where(a => range.Contains(a.Entry.Date)
                        && playersById.ContainsKey(a.UserId)
                        && activitiesById.ContainsKey(a.Entry.ActivityId))
            .GroupBy(a => (a.Entry.ActivityId, a.Entry.Date, a.Entry.Slot))
            .Select(group => new WeekEvent(
                activitiesById[group.Key.ActivityId],
                group.Key.Date,
                group.Key.Slot,
                times.GetValueOrDefault(group.Key),
                group.Select(a => playersById[a.UserId])
                    .Distinct()
                    .OrderBy(p => p.Nick, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.UserId, StringComparer.Ordinal)
                    .ToList()))
            .OrderBy(e => e.Date)
            .ThenBy(e => e.TimeUtc.HasValue)
            .ThenBy(e => e.TimeUtc)
            .ThenBy(e => e.Activity.SortOrder)
            .ThenBy(e => e.Slot)
            .ToList();
    }

    /// <summary>
    /// Day shown first: today when it falls into the week, otherwise the last day of the week with an event,
    /// otherwise its Monday.
    /// </summary>
    public static DateOnly DefaultDay(Week week, DateOnly today, IReadOnlyCollection<WeekEvent> events)
    {
        if (week.Range.Contains(today))
        {
            return today;
        }

        return events.Count > 0 ? events.Max(e => e.Date) : week.Monday;
    }
}
