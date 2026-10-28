using L2Points.Domain.Activities;
using L2Points.Domain.Time;

namespace L2Points.Domain.Schedule;

public static class ScheduleCalculator
{
    /// <summary>
    /// Fixed (non-flexible) occurrences of active activities within <paramref name="range"/>,
    /// ordered by date, activity sort order, then slot.
    /// </summary>
    public static IReadOnlyList<ScheduledOccurrence> GetFixedOccurrences(IEnumerable<Activity> activities, DateRange range)
    {
        var active = activities.Where(a => a.IsActive && a.ScheduleType != ScheduleType.Flexible).ToList();
        var result = new List<ScheduledOccurrence>();

        for (var date = range.From; date <= range.To; date = date.AddDays(1))
        {
            foreach (var activity in active)
                result.AddRange(OccurrencesOn(activity, date));
        }

        return result
            .OrderBy(o => o.Date)
            .ThenBy(o => o.Activity.SortOrder)
            .ThenBy(o => o.Slot)
            .ToList();
    }

    public static bool IsValidSlot(Activity activity, DateOnly date, EventSlot slot)
    {
        if (!activity.IsActive)
            return false;

        if (activity.ScheduleType == ScheduleType.Flexible)
            return slot == EventSlot.None;

        return OccurrencesOn(activity, date).Any(o => o.Slot == slot);
    }

    private static IEnumerable<ScheduledOccurrence> OccurrencesOn(Activity activity, DateOnly date)
    {
        switch (activity.ScheduleType)
        {
            case ScheduleType.Weekly:
                if (date.DayOfWeek == activity.WeeklyDay)
                    yield return new ScheduledOccurrence(activity, date, EventSlot.None, activity.TimeUtc);
                break;

            case ScheduleType.Biweekly:
                if (date.DayOfWeek == activity.WeeklyDay && IsBiweeklyDate(activity, date))
                    yield return new ScheduledOccurrence(activity, date, EventSlot.None, activity.TimeUtc);
                break;

            case ScheduleType.Slotted:
                foreach (var spawn in activity.Spawns.Where(s => s.DayOfWeek == date.DayOfWeek))
                    yield return new ScheduledOccurrence(activity, date, spawn.Slot, spawn.TimeUtc);
                break;
        }
    }

    private static bool IsBiweeklyDate(Activity activity, DateOnly date)
    {
        if (activity.BiweeklyAnchorDate is not { } anchor)
            return false;

        var days = date.DayNumber - anchor.DayNumber;
        return ((days % 14) + 14) % 14 == 0;
    }
}
