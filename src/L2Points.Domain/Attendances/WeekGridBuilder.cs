using L2Points.Domain.Activities;
using L2Points.Domain.Schedule;
using L2Points.Domain.Stats;
using L2Points.Domain.Time;

namespace L2Points.Domain.Attendances;

/// <summary>
/// Builds the weekly attendance grid (rows = activities, columns = days) for one user.
/// <c>canEdit: false</c> locks every cell regardless of the edit window (read-only accounts).
/// </summary>
public static class WeekGridBuilder
{
    public static WeekGrid Build(
        IReadOnlyList<Activity> activities,
        Week week,
        IReadOnlyCollection<AttendanceEntry> attendances,
        DateOnly today,
        DateOnly trackingStart,
        bool isAdmin,
        bool canEdit = true)
    {
        var range = week.Range;
        var checkedEntries = attendances
            .Where(a => a.Date >= range.From && a.Date <= range.To)
            .ToHashSet();

        var occurrences = ScheduleCalculator.GetFixedOccurrences(activities, range)
            .ToLookup(o => o.Activity.Id);

        var rows = new List<WeekGridRow>();
        foreach (var activity in activities.OrderBy(a => a.SortOrder))
        {
            var scheduled = new Dictionary<(DateOnly Date, EventSlot Slot), TimeOnly?>();
            if (activity.IsActive && activity.ScheduleType == ScheduleType.Flexible)
            {
                foreach (var day in week.Days)
                    scheduled[(day, EventSlot.None)] = null;
            }
            else
            {
                foreach (var occurrence in occurrences[activity.Id])
                    scheduled[(occurrence.Date, occurrence.Slot)] = occurrence.TimeUtc;
            }

            var orphans = checkedEntries
                .Where(e => e.ActivityId == activity.Id && !scheduled.ContainsKey((e.Date, e.Slot)))
                .Select(e => (e.Date, e.Slot))
                .ToList();

            if (!activity.IsActive && orphans.Count == 0)
                continue;

            var cells = scheduled
                .Select(s => (s.Key.Date, s.Key.Slot, TimeUtc: s.Value, IsScheduled: true))
                .Concat(orphans.Select(o => (o.Date, o.Slot, TimeUtc: (TimeOnly?)null, IsScheduled: false)))
                .OrderBy(c => c.Date)
                .ThenBy(c => c.Slot)
                .Select(c =>
                {
                    var isChecked = checkedEntries.Contains(new AttendanceEntry(activity.Id, c.Date, c.Slot));
                    var editable = canEdit
                                   && AttendanceRules.CanEdit(c.Date, today, trackingStart, isAdmin)
                                   && (c.IsScheduled || isChecked);
                    return new WeekGridCell(c.Date, c.Slot, c.TimeUtc, c.IsScheduled, isChecked, editable, c.Date >= trackingStart);
                })
                .ToList();

            rows.Add(new WeekGridRow(activity, cells));
        }

        return new WeekGrid(week, rows);
    }
}
