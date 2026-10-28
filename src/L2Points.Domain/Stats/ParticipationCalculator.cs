using L2Points.Domain.Activities;
using L2Points.Domain.Schedule;
using L2Points.Domain.Time;

namespace L2Points.Domain.Stats;

public static class ParticipationCalculator
{
    /// <summary>
    /// Participation per category: sum of min(attended, expected) over weekly (activity, slot) buckets
    /// versus the sum of expected occurrences. Partial weeks prorate flexible activities.
    /// </summary>
    public static ParticipationStats Calculate(
        IReadOnlyList<Activity> activities,
        IEnumerable<AttendanceEntry> attendances,
        DateRange? range)
    {
        if (range is not { } fullRange)
            return ParticipationStats.Empty;

        var activeById = activities.Where(a => a.IsActive).ToDictionary(a => a.Id);
        var attendanceList = attendances.Where(a => activeById.ContainsKey(a.ActivityId)).ToList();
        var attended = new Dictionary<StatCategory, int>();
        var expected = new Dictionary<StatCategory, int>();

        foreach (var week in fullRange.Weeks)
        {
            if (week.Range.Intersect(fullRange) is not { } subrange)
                continue;

            var expectedByBucket = new Dictionary<(int ActivityId, EventSlot Slot), int>();

            foreach (var occurrence in ScheduleCalculator.GetFixedOccurrences(activeById.Values, subrange))
            {
                var bucket = (occurrence.Activity.Id, occurrence.Slot);
                expectedByBucket[bucket] = expectedByBucket.GetValueOrDefault(bucket) + 1;
            }

            foreach (var flexible in activeById.Values.Where(a => a.ScheduleType == ScheduleType.Flexible))
            {
                var perWeek = flexible.ExpectedPerWeek ?? 0;
                var count = (perWeek * subrange.DayCount + 6) / 7;
                if (count > 0)
                    expectedByBucket[(flexible.Id, EventSlot.None)] = count;
            }

            var attendedByBucket = attendanceList
                .Where(a => subrange.Contains(a.Date))
                .GroupBy(a => (a.ActivityId, a.Slot))
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var ((activityId, slot), expectedCount) in expectedByBucket)
            {
                var category = StatCategories.Of(activeById[activityId].Category, slot);
                var counted = Math.Min(attendedByBucket.GetValueOrDefault((activityId, slot)), expectedCount);

                expected[category] = expected.GetValueOrDefault(category) + expectedCount;
                attended[category] = attended.GetValueOrDefault(category) + counted;
            }
        }

        var byCategory = expected.Keys.ToDictionary(
            category => category,
            category => new CategoryStat(attended.GetValueOrDefault(category), expected[category]));

        return new ParticipationStats(byCategory);
    }
}
