using L2Points.Domain.Activities;

namespace L2Points.Domain.Tests.Stats;

internal static class StatsTestCatalog
{
    /// <summary>Fresh default catalog with ids 1..14 assigned by sort order.</summary>
    public static List<Activity> Create()
    {
        var activities = DefaultCatalog.CreateActivities().OrderBy(a => a.SortOrder).ToList();
        for (var i = 0; i < activities.Count; i++)
        {
            var activity = activities[i];
            activity.Id = i + 1;
            foreach (var spawn in activity.Spawns)
                spawn.ActivityId = activity.Id;
            foreach (var slotPoints in activity.SlotPoints)
                slotPoints.ActivityId = activity.Id;
        }

        return activities;
    }

    public static Activity ByCode(this IEnumerable<Activity> activities, string code) =>
        activities.Single(a => a.Code == code);
}
