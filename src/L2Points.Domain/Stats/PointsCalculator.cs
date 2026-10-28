using L2Points.Domain.Activities;

namespace L2Points.Domain.Stats;

public static class PointsCalculator
{
    /// <summary>
    /// Sum of points over all attendances, uncapped. A slot override wins over <see cref="Activity.Points"/>;
    /// unknown activities give 0, inactive activities still give points.
    /// </summary>
    public static int Sum(IReadOnlyList<Activity> activities, IEnumerable<AttendanceEntry> attendances)
    {
        var byId = activities.ToDictionary(a => a.Id);
        var total = 0;

        foreach (var attendance in attendances)
        {
            if (!byId.TryGetValue(attendance.ActivityId, out var activity))
                continue;

            var slotOverride = activity.SlotPoints.FirstOrDefault(p => p.Slot == attendance.Slot);
            total += slotOverride?.Points ?? activity.Points;
        }

        return total;
    }
}
